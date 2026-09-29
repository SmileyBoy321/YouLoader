package com.youloader.app.core

enum class DownloadState { QUEUED, DOWNLOADING, PROCESSING, DONE, FAILED, CANCELED }

/** A finished file in shared storage. [uri] opens it; [displayPath] is what the user sees, e.g. "Download/YouLoader/Song.mp3". */
data class SavedFile(val uri: String, val displayPath: String, val mimeType: String)

/**
 * One link in the download queue. Port of the Windows app's DownloadItem.
 * The queue changes it from a worker thread; the screen reads [snapshot]s.
 */
class DownloadItem(val id: Long, val url: String, val request: DownloadRequest, private val clock: () -> Long = System::currentTimeMillis) {

    /** Same key = same download: the same video (however the link is written) in the same format. */
    val key = "${LinkParser.downloadKey(url, request.wholePlaylist)}|${request.format}"

    @Volatile var title: String = url
    @Volatile var status: String = WAITING_TEXT; private set
    @Volatile var progress: Double = 0.0; private set
    @Volatile var state: DownloadState = DownloadState.QUEUED; private set
    @Volatile var error: ErrorExplanation? = null; private set
    @Volatile var technicalDetails: String? = null; private set

    /** When the item joined the line. Waiting items start in this order. */
    @Volatile var queuedAt: Long = clock(); private set
    @Volatile var startedAt: Long? = null; private set
    @Volatile var finishedAt: Long? = null; private set

    private val saved = mutableListOf<SavedFile>()
    val savedFiles: List<SavedFile> get() = synchronized(saved) { saved.toList() }

    val isActive get() = state == DownloadState.QUEUED || state == DownloadState.DOWNLOADING || state == DownloadState.PROCESSING
    val isDone get() = state == DownloadState.DONE
    val canRetry get() = state == DownloadState.FAILED || state == DownloadState.CANCELED

    @Volatile var cancelRequested = false; private set

    fun reportStarting() {
        if (startedAt == null) startedAt = clock()
        state = DownloadState.DOWNLOADING
        progress = 0.0
        status = "Starting…"
    }

    /** @param text new status text, or null to keep the current one. */
    fun reportDownloading(percent: Double, text: String?) {
        state = DownloadState.DOWNLOADING
        progress = percent.coerceIn(0.0, 100.0)
        if (text != null) status = text
    }

    fun reportRetrying(attempt: Int, maxAttempts: Int) {
        state = DownloadState.DOWNLOADING
        progress = 0.0
        status = "Connection hiccup. Retrying ($attempt/$maxAttempts)…"
    }

    fun reportProcessing(text: String) {
        state = DownloadState.PROCESSING
        progress = 100.0
        status = text
    }

    fun reportSavedFile(file: SavedFile) = synchronized(saved) { saved += file }

    fun complete(warning: String? = null) {
        finishedAt = clock()
        state = DownloadState.DONE
        progress = 100.0
        val files = savedFiles
        val text = when (files.size) {
            0 -> "Up to date · nothing new to download"
            1 -> "Saved · ${files[0].displayPath.substringAfterLast('/')}"
            else -> "Saved ${files.size} files"
        }
        status = if (warning == null) text else "$text · $warning"
    }

    fun fail(explanation: ErrorExplanation, details: String? = null) {
        finishedAt = clock()
        error = explanation
        technicalDetails = details?.takeIf { it.isNotBlank() }
        state = DownloadState.FAILED
        status = explanation.summary
    }

    fun markCanceled() {
        finishedAt = clock()
        state = DownloadState.CANCELED
        status = when (val saved = savedFiles.size) {
            0 -> "Canceled"
            1 -> "Canceled · 1 file saved"
            else -> "Canceled · $saved files saved"
        }
    }

    /** Between playlist items, while yt-dlp looks up the next video. */
    fun reportNextInCollection() {
        state = DownloadState.DOWNLOADING
        progress = 0.0
        status = "Saved ${savedFiles.size} so far · getting the next video…"
    }

    fun requestCancel() {
        cancelRequested = true
    }

    /** Puts a failed or canceled item back in the queue. */
    fun reset() {
        cancelRequested = false
        error = null
        technicalDetails = null
        synchronized(saved) { saved.clear() }
        progress = 0.0
        queuedAt = clock()
        startedAt = null
        finishedAt = null
        status = WAITING_TEXT
        state = DownloadState.QUEUED
    }

    fun snapshot() = ItemSnapshot(
        id, url, request.format, title, status, progress, state, error, technicalDetails, savedFiles,
        LinkParser.isCollection(url, request.wholePlaylist), finishedAt,
    )

    companion object {
        const val WAITING_TEXT = "Waiting for a free slot…"
    }
}

/** An unchanging copy of an item, for the screen and the notification. */
data class ItemSnapshot(
    val id: Long,
    val url: String,
    val format: OutputFormat,
    val title: String,
    val status: String,
    val progress: Double,
    val state: DownloadState,
    val error: ErrorExplanation?,
    val technicalDetails: String?,
    val savedFiles: List<SavedFile>,
    val isCollection: Boolean,
    val finishedAt: Long?,
) {
    val isActive get() = state == DownloadState.QUEUED || state == DownloadState.DOWNLOADING || state == DownloadState.PROCESSING
    val canRetry get() = state == DownloadState.FAILED || state == DownloadState.CANCELED
}

/**
 * How the download list is sorted, so what's happening right now is always at the top:
 * downloading first (in the order they started), then waiting (in the order they'll start),
 * then finished (newest first).
 */
object QueueOrder {
    fun sort(items: List<DownloadItem>): List<DownloadItem> =
        items.sortedWith(compareBy<DownloadItem> { group(it) }.thenBy {
            when (group(it)) {
                0 -> it.startedAt ?: it.queuedAt
                1 -> it.queuedAt
                else -> -(it.finishedAt ?: it.queuedAt)
            }
        })

    /** A one-line summary for the list header, e.g. "2 downloading · 3 waiting". */
    fun summary(items: List<ItemSnapshot>): String {
        val downloading = items.count { it.state == DownloadState.DOWNLOADING || it.state == DownloadState.PROCESSING }
        val waiting = items.count { it.state == DownloadState.QUEUED }
        return listOfNotNull(
            if (downloading > 0) "$downloading downloading" else null,
            if (waiting > 0) "$waiting waiting" else null,
        ).joinToString(" · ")
    }

    private fun group(item: DownloadItem) = when (item.state) {
        DownloadState.DOWNLOADING, DownloadState.PROCESSING -> 0
        DownloadState.QUEUED -> 1
        else -> 2
    }
}
