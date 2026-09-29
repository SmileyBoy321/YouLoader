package com.youloader.app

import android.Manifest
import android.content.ActivityNotFoundException
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings as AndroidSettings
import android.view.View
import android.widget.ArrayAdapter
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.core.widget.doAfterTextChanged
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.recyclerview.widget.LinearLayoutManager
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import com.google.android.material.snackbar.Snackbar
import com.youloader.app.core.DownloadRequest
import com.youloader.app.core.ItemSnapshot
import com.youloader.app.core.LinkParser
import com.youloader.app.core.OutputFormat
import com.youloader.app.core.QualityPreset
import com.youloader.app.core.QualityPresets
import com.youloader.app.core.QueueOrder
import com.youloader.app.databinding.ActivityMainBinding
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity(), ItemAdapter.Actions {

    private lateinit var b: ActivityMainBinding
    private lateinit var settings: Settings
    private lateinit var adapter: ItemAdapter
    private var format = OutputFormat.MP3
    private var presets: List<QualityPreset> = emptyList()
    private var quality: QualityPreset? = null

    // Clipboard auto-paste, as on Windows: the box text YouLoader wrote itself (the last link it picked up is in settings).
    private var autoPastedText: String? = null

    private var appUpdate: AppUpdates.Update? = null
    private var scrollToListOnNextUpdate = false

    private val storagePermission =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
            if (granted) startDownloads()
            else if (!shouldShowRequestPermissionRationale(Manifest.permission.WRITE_EXTERNAL_STORAGE)) {
                showNotice("YouLoader needs storage access to save into Downloads. Android won't ask again, so turn it on in Settings → Permissions → Storage.", "Open settings") {
                    openAppSettings()
                }
            } else {
                showNotice("YouLoader needs storage access to save into Downloads. Tap Download again to allow it.")
            }
        }

    // Nice to have (it shows download progress), so a "no" doesn't stop anything.
    private val notificationPermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) { }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        b = ActivityMainBinding.inflate(layoutInflater)
        setContentView(b.root)
        settings = (application as YouLoaderApp).settings

        b.toolbar.setOnMenuItemClickListener { onMenuItem(it.itemId) }

        adapter = ItemAdapter(this)
        b.itemsList.layoutManager = LinearLayoutManager(this)
        b.itemsList.adapter = adapter
        b.itemsList.itemAnimator = null

        setupForm()
        watchQueue()
        watchEngine()
        checkForAppUpdate()
        handleShare(intent)
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handleShare(intent)
    }

    override fun onStart() {
        super.onStart()
        isVisible = true
    }

    override fun onStop() {
        isVisible = false
        super.onStop()
    }

    // Android only lets an app read the clipboard while one of its windows has focus.
    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) autoPasteFromClipboard()
    }

    // ---- Form ----

    private fun setupForm() {
        format = settings.format
        b.formatGroup.check(if (format == OutputFormat.MP3) R.id.mp3Button else R.id.mp4Button)
        b.formatGroup.addOnButtonCheckedListener { _, id, checked ->
            if (!checked) return@addOnButtonCheckedListener
            format = if (id == R.id.mp3Button) OutputFormat.MP3 else OutputFormat.MP4
            settings.format = format
            showQualities()
        }
        showQualities()

        b.qualityInput.setOnItemClickListener { _, _, position, _ ->
            quality = presets[position]
            settings.setQuality(format, presets[position].key)
            b.qualityHint.text = presets[position].hint
        }

        b.playlistCheck.isChecked = settings.wholePlaylist
        b.playlistCheck.setOnCheckedChangeListener { _, checked -> settings.wholePlaylist = checked }
        b.embedArtCheck.isChecked = settings.embedArt
        b.embedArtCheck.setOnCheckedChangeListener { _, checked -> settings.embedArt = checked }

        b.linkInput.doAfterTextChanged { showLinkCount() }
        b.pasteButton.setOnClickListener { pasteFromClipboard() }
        b.downloadButton.setOnClickListener { onDownloadClicked() }
        b.clearFinishedButton.setOnClickListener { DownloadQueue.clearFinished() }
        b.noticeDismiss.setOnClickListener { hideNotice() }
        b.updateButton.setOnClickListener { appUpdate?.let { openUrl(it.url) } }
        showLinkCount()
    }

    private fun showQualities() {
        presets = QualityPresets.forFormat(format)
        val selected = QualityPresets.find(format, settings.qualityFor(format))
        quality = selected
        b.qualityInput.setAdapter(ArrayAdapter(this, android.R.layout.simple_list_item_1, presets.map { it.label }))
        b.qualityInput.setText(selected.label, false)
        b.qualityHint.text = selected.hint
    }

    private fun showLinkCount() {
        val (links, duplicates) = LinkParser.countLinks(boxText())
        b.linkCount.text = if (links == 0) "" else
            (if (links == 1) "1 link" else "$links links") +
                when (duplicates) {
                    0 -> ""
                    1 -> " · 1 duplicate will be skipped"
                    else -> " · $duplicates duplicates will be skipped"
                }
    }

    private fun boxText() = b.linkInput.text?.toString().orEmpty()

    private fun setBoxText(text: String) {
        b.linkInput.setText(text)
        b.linkInput.setSelection(text.length)
    }

    // ---- Getting links in ----

    /** "Share → YouLoader" from the YouTube app sends "Title https://youtu.be/…". */
    private fun handleShare(intent: Intent?) {
        if (intent?.action != Intent.ACTION_SEND) return
        val text = intent.getStringExtra(Intent.EXTRA_TEXT) ?: return
        intent.action = null // Don't add it again when the screen is recreated.
        if (LinkParser.extractUrls(text).isEmpty()) {
            showNotice("That didn't include a link. Share a video from the YouTube app, or copy its link.")
            return
        }
        insertLinks(text)
        b.scroll.smoothScrollTo(0, 0)
    }

    private fun pasteFromClipboard() {
        val text = clipboardText()
        if (text == null || LinkParser.extractUrls(text).isEmpty()) {
            showNotice("There's no link on the clipboard. In YouTube, tap Share → Copy link, then Paste.")
            return
        }
        insertLinks(text)
    }

    /** Adds links one per line, without repeating any that are already in the box. */
    private fun insertLinks(pasted: String) {
        val merge = LinkParser.mergeLinks(boxText(), pasted)
        setBoxText(merge.text)
        autoPastedText = null
        when {
            merge.duplicates == 1 -> showNotice("That link is already in the box, so it wasn't added again.")
            merge.duplicates > 1 -> showNotice("${merge.duplicates} of those links were already in the box, so they weren't added again.")
        }
    }

    // Saves a tap: a freshly copied YouTube link is waiting in the box when you switch back.
    // A newly copied link replaces one YouLoader pasted itself, but never overwrites what the user typed:
    // then it's added on a new line instead, so copying links one after another builds a list.
    private fun autoPasteFromClipboard() {
        val text = clipboardText()?.trim() ?: return
        if (text == settings.lastClipboardLink || text.length > 2000) return
        val urls = LinkParser.extractUrls(text)
        if (urls.isEmpty() || !urls.all(LinkParser::isYouTubeUrl)) return
        settings.lastClipboardLink = text

        if (boxText().isEmpty() || boxText() == autoPastedText) {
            setBoxText(LinkParser.mergeLinks("", text).text)
            autoPastedText = boxText()
        } else {
            // Quietly skip a link that's already in the box: the user didn't ask to paste it.
            val merge = LinkParser.mergeLinks(boxText(), text)
            if (merge.added == 0) return
            setBoxText(merge.text)
            autoPastedText = null
        }
    }

    private fun clipboardText(): String? = try {
        val clip = getSystemService(ClipboardManager::class.java).primaryClip
        clip?.takeIf { it.itemCount > 0 }?.getItemAt(0)?.coerceToText(this)?.toString()
    } catch (e: SecurityException) {
        null
    }

    // ---- Downloads ----

    private fun onDownloadClicked() {
        if (PublicDownloads.needsStoragePermission &&
            ContextCompat.checkSelfPermission(this, Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED
        ) {
            storagePermission.launch(Manifest.permission.WRITE_EXTERNAL_STORAGE)
            return
        }
        startDownloads()
    }

    private fun startDownloads() {
        val urls = LinkParser.extractUrls(boxText())
        if (urls.isEmpty()) {
            showNotice("Paste at least one YouTube link that starts with https://")
            return
        }
        askForNotifications()

        val request = DownloadRequest(
            format,
            quality?.key ?: QualityPresets.forFormat(format)[0].key,
            b.playlistCheck.isChecked,
            b.embedArtCheck.isChecked,
        )
        val result = DownloadQueue.add(urls, request)
        setBoxText("")
        autoPastedText = null

        val notes = mutableListOf<String>()
        if (result.notYouTube.isNotEmpty()) {
            notes += if (result.notYouTube.size == 1) "YouLoader only downloads from YouTube, so the ${hostOf(result.notYouTube[0])} link was skipped."
            else "YouLoader only downloads from YouTube, so ${result.notYouTube.size} other links were skipped."
        }
        val listed = result.alreadyListed
        if (listed.isNotEmpty()) {
            notes += if (listed.size == 1) "“${listed[0].title}” is already in your list as ${listed[0].format.label}, so it wasn't added again. It's highlighted below."
            else "${listed.size} links are already in your list in this format, so they weren't added again. They're highlighted below."
        }
        if (notes.isEmpty()) hideNotice() else showNotice(notes.joinToString(" "))

        listed.forEach { highlight(it.id) }
        scrollToListOnNextUpdate = true
    }

    private fun askForNotifications() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
            ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) notificationPermission.launch(Manifest.permission.POST_NOTIFICATIONS)
    }

    private fun highlight(id: Long) {
        adapter.highlight(id, true)
        lifecycleScope.launch {
            delay(2500)
            adapter.highlight(id, false)
        }
    }

    private fun watchQueue() {
        lifecycleScope.launch {
            repeatOnLifecycle(Lifecycle.State.STARTED) {
                DownloadQueue.changes.collect {
                    val items = DownloadQueue.snapshot()
                    adapter.submitList(items) {
                        // After Download, show the list once the new rows exist, so it can scroll all the way.
                        if (scrollToListOnNextUpdate) {
                            scrollToListOnNextUpdate = false
                            b.scroll.post { b.scroll.smoothScrollTo(0, b.downloadsHeader.top) }
                        }
                    }
                    b.emptyText.visibility = if (items.isEmpty()) View.VISIBLE else View.GONE
                    val summary = QueueOrder.summary(items)
                    b.queueSummary.text = if (summary.isEmpty()) "" else "· $summary"
                    b.clearFinishedButton.visibility = if (items.any { !it.isActive }) View.VISIBLE else View.INVISIBLE
                    // Redrawing a few times a second is plenty for progress bars.
                    delay(200)
                }
            }
        }
    }

    // ---- Engine and app updates ----

    private fun watchEngine() {
        lifecycleScope.launch {
            repeatOnLifecycle(Lifecycle.State.STARTED) {
                Engine.state.combine(Engine.version) { state, version -> state to version }.collect { (state, version) ->
                    b.downloadButton.isEnabled = state != Engine.State.FAILED
                    b.footerText.text = when (state) {
                        Engine.State.STARTING -> "Getting ready… (the first start takes a few seconds)"
                        Engine.State.UPDATING -> "Updating the download engine…"
                        Engine.State.FAILED -> "The download engine couldn't start. Close YouLoader and open it again."
                        Engine.State.READY -> "Saves to ${PublicDownloads.displayFolder} · yt-dlp ${version ?: "(built in)"}"
                    }
                }
            }
        }
    }

    private fun updateEngine() {
        val app = application as YouLoaderApp
        app.scope.launch {
            val message = when (val result = Engine.update(app, settings)) {
                is Engine.UpdateResult.Updated -> "The download engine was updated to ${result.version ?: "the latest version"}."
                Engine.UpdateResult.AlreadyUpToDate -> "The download engine is already up to date (${Engine.version.value})."
                Engine.UpdateResult.Busy ->
                    if (DownloadQueue.hasActive()) "Updating has to wait until your downloads finish. Try again then."
                    else "The download engine is busy. Try again in a moment."
                is Engine.UpdateResult.Failed -> "Couldn't update the download engine. Check that you're online, then try again."
            }
            // Started from the menu, so answer where the user is looking, not in the notice below the form.
            if (isVisible) Snackbar.make(b.root, message, Snackbar.LENGTH_LONG).show()
        }
    }

    private fun checkForAppUpdate() {
        lifecycleScope.launch {
            val update = AppUpdates.check(BuildConfig.VERSION_NAME) ?: return@launch
            appUpdate = update
            b.updateText.text = "YouLoader ${update.version} is out. You have ${BuildConfig.VERSION_NAME}."
            b.updateBanner.visibility = View.VISIBLE
        }
    }

    private fun onMenuItem(id: Int): Boolean {
        when (id) {
            R.id.menu_update_engine -> updateEngine()
            R.id.menu_privacy -> openUrl("https://youloader.pages.dev/privacy/")
            R.id.menu_source -> openUrl(AppUpdates.SOURCE_URL)
            R.id.menu_about -> showAbout()
            else -> return false
        }
        return true
    }

    private fun showAbout() {
        MaterialAlertDialogBuilder(this)
            .setTitle("YouLoader ${BuildConfig.VERSION_NAME}")
            .setMessage(
                "Downloads. Nothing else.\n\n" +
                    "Free forever, with no ads, no sign-up and no tracking. The code is public under the MIT license.\n\n" +
                    "Downloading is done by yt-dlp ${Engine.version.value ?: "(built in)"}, ffmpeg and QuickJS, " +
                    "bundled by youtubedl-android.",
            )
            .setPositiveButton("OK", null)
            .setNeutralButton(R.string.menu_source) { _, _ -> openUrl(AppUpdates.SOURCE_URL) }
            .show()
    }

    // ---- Item actions ----

    override fun cancel(item: ItemSnapshot) = DownloadQueue.cancel(item.id)

    override fun retry(item: ItemSnapshot) {
        if (PublicDownloads.needsStoragePermission &&
            ContextCompat.checkSelfPermission(this, Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED
        ) {
            showNotice("YouLoader needs storage access to save into Downloads. Tap Download to allow it.")
            return
        }
        DownloadQueue.retry(item.id)
    }

    override fun remove(item: ItemSnapshot) = DownloadQueue.remove(item.id)

    override fun play(item: ItemSnapshot) {
        val file = item.savedFiles.firstOrNull() ?: return
        val intent = Intent(Intent.ACTION_VIEW)
            .setDataAndType(Uri.parse(file.uri), file.mimeType)
            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        try {
            startActivity(intent)
        } catch (e: ActivityNotFoundException) {
            val type = file.displayPath.substringAfterLast('.').uppercase()
            showNotice("No app on this phone can play $type files. It's saved in ${file.displayPath.substringBeforeLast('/')}; a free player such as VLC can open it.")
        }
    }

    override fun share(item: ItemSnapshot) {
        val files = item.savedFiles
        if (files.isEmpty()) return
        val intent = if (files.size == 1) {
            Intent(Intent.ACTION_SEND).setType(files[0].mimeType).putExtra(Intent.EXTRA_STREAM, Uri.parse(files[0].uri))
        } else {
            Intent(Intent.ACTION_SEND_MULTIPLE).setType(if (files.all { it.mimeType == files[0].mimeType }) files[0].mimeType else "*/*")
                .putParcelableArrayListExtra(Intent.EXTRA_STREAM, ArrayList(files.map { Uri.parse(it.uri) }))
        }
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        startActivity(Intent.createChooser(intent, null))
    }

    override fun copyDetails(item: ItemSnapshot) {
        if (copyToClipboard(item)) showNotice("Technical details copied. They don't include anything about you, so they're safe to share.")
    }

    // Opens a new GitHub issue with the basics filled in. The log is copied first, so the user only has to paste it.
    override fun report(item: ItemSnapshot) {
        val copied = copyToClipboard(item)
        val body =
            "**What happened:** ${item.error?.summary}\n" +
                "**Format:** ${item.format.label}\n" +
                "**YouLoader:** Android ${BuildConfig.VERSION_NAME}, yt-dlp ${Engine.version.value ?: "built in"}\n" +
                "**Phone:** ${Build.MANUFACTURER} ${Build.MODEL}, Android ${Build.VERSION.RELEASE}\n\n" +
                (if (copied) "**Technical details** (already copied, paste them between the lines below):\n\n```\n\n```\n\n" else "") +
                "**Anything else?** For example the link, if you're happy to share it.\n"
        val title = Uri.encode(item.error?.summary ?: "Download failed")
        openUrl("${AppUpdates.SOURCE_URL}/issues/new?title=$title&body=${Uri.encode(body)}")
    }

    private fun copyToClipboard(item: ItemSnapshot): Boolean {
        val details = item.technicalDetails ?: return false
        getSystemService(ClipboardManager::class.java).setPrimaryClip(ClipData.newPlainText("YouLoader details", details))
        // Don't let our own copy look like a freshly copied link.
        settings.lastClipboardLink = details.trim()
        return true
    }

    // ---- Helpers ----

    private fun showNotice(text: String, action: String? = null, onAction: (() -> Unit)? = null) {
        b.noticeText.text = text
        b.noticeAction.visibility = if (action == null) View.GONE else View.VISIBLE
        b.noticeAction.text = action
        b.noticeAction.setOnClickListener { onAction?.invoke() }
        b.noticeCard.visibility = View.VISIBLE
    }

    private fun hideNotice() {
        b.noticeCard.visibility = View.GONE
    }

    private fun openUrl(url: String) {
        try {
            startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
        } catch (e: ActivityNotFoundException) {
            Toast.makeText(this, url, Toast.LENGTH_LONG).show()
        }
    }

    private fun openAppSettings() {
        startActivity(Intent(AndroidSettings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.fromParts("package", packageName, null)))
    }

    private fun hostOf(url: String) = Uri.parse(url).host?.removePrefix("www.") ?: url

    companion object {
        /** Whether the list is on screen, so a finished-downloads notification isn't needed. */
        @Volatile var isVisible = false
            private set
    }
}
