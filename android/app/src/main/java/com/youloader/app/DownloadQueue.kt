package com.youloader.app

import android.annotation.SuppressLint
import android.content.Context
import com.youloader.app.core.DownloadItem
import com.youloader.app.core.DownloadProgress
import com.youloader.app.core.DownloadRequest
import com.youloader.app.core.ErrorMessages
import com.youloader.app.core.FileSaved
import com.youloader.app.core.ItemSnapshot
import com.youloader.app.core.LinkParser
import com.youloader.app.core.OutputParser
import com.youloader.app.core.ProcessingStep
import com.youloader.app.core.QueueOrder
import com.youloader.app.core.TechnicalLog
import com.youloader.app.core.TransferMeter
import com.youloader.app.core.YtDlpArguments
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withPermit
import java.io.File
import java.util.concurrent.atomic.AtomicLong

/**
 * The download list. Runs up to two downloads at a time, like the Windows app, for as long as the app's
 * process lives; [DownloadService] keeps the process alive while anything is downloading.
 */
@SuppressLint("StaticFieldLeak") // Holds the application context only, which lives as long as the process.
object DownloadQueue {
    const val MAX_PARALLEL_DOWNLOADS = 2
    const val MAX_ATTEMPTS = 3

    private lateinit var app: Context
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val slots = Semaphore(MAX_PARALLEL_DOWNLOADS)
    private val items = mutableListOf<DownloadItem>()
    private val jobs = mutableMapOf<Long, Job>()
    private val nextId = AtomicLong(1)

    private val _changes = MutableStateFlow(0L)
    /** Ticks whenever anything in the list changes; read [snapshot] to see what. */
    val changes: StateFlow<Long> = _changes

    fun init(context: Context) {
        app = context.applicationContext
    }

    /** The list in display order: downloading, then waiting, then finished (newest first). */
    fun snapshot(): List<ItemSnapshot> = synchronized(items) { QueueOrder.sort(items) }.map { it.snapshot() }

    fun hasActive(): Boolean = synchronized(items) { items.any { it.isActive } }

    data class AddResult(val added: Int, val alreadyListed: List<ItemSnapshot>, val notYouTube: List<String>)

    /** Adds every YouTube link in [urls] that isn't already downloading or done in the same format. */
    fun add(urls: List<String>, request: DownloadRequest): AddResult {
        val notYouTube = urls.filterNot(LinkParser::isYouTubeUrl)
        val alreadyListed = mutableListOf<DownloadItem>()
        var added = 0
        synchronized(items) {
            for (url in urls - notYouTube.toSet()) {
                val item = DownloadItem(nextId.getAndIncrement(), url, request)
                // The same video in the same format is already downloading or done: point at it instead of adding a copy.
                val existing = items.firstOrNull { it.key == item.key && (it.isActive || it.isDone) }
                if (existing != null) {
                    if (existing !in alreadyListed) alreadyListed += existing
                    continue
                }
                items += item
                start(item)
                added++
            }
        }
        changed()
        if (added > 0) DownloadService.start(app)
        return AddResult(added, alreadyListed.map { it.snapshot() }, notYouTube)
    }

    fun cancel(id: Long) {
        val item = find(id) ?: return
        if (!item.isActive) return
        item.requestCancel()
        if (!Engine.cancel(processId(item))) {
            // Still waiting for a slot: nothing is running yet, so it can stop right away.
            synchronized(items) { jobs.remove(id) }?.cancel()
            item.markCanceled()
        }
        changed()
    }

    fun retry(id: Long) {
        val item = find(id) ?: return
        if (!item.canRetry) return
        item.reset()
        synchronized(items) { start(item) }
        changed()
        DownloadService.start(app)
    }

    fun remove(id: Long) {
        synchronized(items) { items.removeAll { it.id == id && !it.isActive } }
        changed()
    }

    fun clearFinished() {
        synchronized(items) { items.removeAll { !it.isActive } }
        changed()
    }

    private fun find(id: Long) = synchronized(items) { items.firstOrNull { it.id == id } }

    private fun changed() {
        _changes.value = _changes.value + 1
    }

    private fun processId(item: DownloadItem) = "item-${item.id}"

    // Called with the items lock held.
    private fun start(item: DownloadItem) {
        jobs[item.id] = scope.launch {
            try {
                slots.withPermit {
                    if (item.cancelRequested) return@withPermit
                    try {
                        Engine.useForDownload { run(item) }
                    } catch (e: Engine.EngineUnavailable) {
                        item.fail(ErrorMessages.engineDidNotStart(e.message))
                    }
                }
            } finally {
                synchronized(items) { if (jobs[item.id] == coroutineContext[Job]) jobs.remove(item.id) }
                changed()
            }
        }
    }

    private suspend fun run(item: DownloadItem) {
        item.reportStarting()
        changed()

        var attempt = 1
        while (true) {
            if (item.cancelRequested) {
                item.markCanceled()
                return
            }

            val (result, log) = runOnce(item)
            val lastError = log.lastOrNull { it.startsWith("ERROR:") }
            val savedCount = item.savedFiles.size
            when {
                result.canceled || item.cancelRequested -> item.markCanceled()
                savedCount > 0 -> item.complete(if (result.exitCode == 0) null else "some items failed")
                result.exitCode == 0 && LinkParser.isCollection(item.url, item.request.wholePlaylist) -> item.complete()
                attempt < MAX_ATTEMPTS && ErrorMessages.isTransient(lastError) -> {
                    // YouTube sometimes drops a connection or refuses a stream mid-download; a fresh attempt usually works.
                    attempt++
                    item.reportRetrying(attempt, MAX_ATTEMPTS)
                    changed()
                    delay(2000L * (attempt - 1))
                    continue
                }
                else -> item.fail(
                    ErrorMessages.explain(log, result.exitCode),
                    TechnicalLog.format(log, listOf(app.cacheDir.parent.orEmpty())),
                )
            }
            return
        }
    }

    private fun runOnce(item: DownloadItem): Pair<Engine.RunResult, List<String>> {
        val workDir = File(app.cacheDir, "work/${item.id}").apply {
            deleteRecursively()
            mkdirs()
        }
        val archive = File(app.filesDir, "archives/${YtDlpArguments.archiveFileName(item.request.format)}")
        archive.parentFile?.mkdirs()

        val log = TechnicalLog()
        val meter = TransferMeter()
        var lastUiUpdate = 0L
        val args = YtDlpArguments.build(item.url, item.request, workDir.absolutePath, archive.absolutePath)
        val isCollection = LinkParser.isCollection(item.url, item.request.wholePlaylist)

        val result = Engine.run(item.url, args, processId(item)) { line ->
            val event = OutputParser.parse(line)
            when (event) {
                is DownloadProgress -> {
                    item.title = event.displayTitle
                    val reading = meter.update(System.currentTimeMillis(), event.downloadedBytes, event.totalBytes)
                    item.reportDownloading(event.percent, reading?.let { OutputParser.describe(event, item.request.format, it) })
                }
                is ProcessingStep -> item.reportProcessing(OutputParser.describeStep(event.name))
                is FileSaved -> {
                    // Move each file as soon as it's finished, so a long playlist never piles up in the cache.
                    try {
                        item.reportSavedFile(PublicDownloads.save(app, File(event.path), workDir))
                        if (isCollection) item.reportNextInCollection()
                    } catch (e: Exception) {
                        log.add("ERROR: Couldn't save ${File(event.path).name}: ${e.message}")
                    }
                }
                null -> log.add(line)
            }
            // Progress lines arrive many times a second; the screen only needs a few.
            val now = System.currentTimeMillis()
            if (event !is DownloadProgress || now - lastUiUpdate >= 250) {
                lastUiUpdate = now
                changed()
            }
        }
        workDir.deleteRecursively()
        return result to log.snapshot()
    }
}
