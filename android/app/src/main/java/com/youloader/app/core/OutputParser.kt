package com.youloader.app.core

import kotlin.math.floor

sealed interface OutputEvent

data class DownloadProgress(
    val downloadedBytes: Long,
    val totalBytes: Long?,
    val isVideoStream: Boolean,
    val playlistIndex: Int?,
    val playlistCount: Int?,
    val title: String,
) : OutputEvent {
    val percent: Double
        get() = if (totalBytes != null && totalBytes > 0) (100.0 * downloadedBytes / totalBytes).coerceIn(0.0, 100.0) else 0.0

    val displayTitle: String
        get() = if (playlistIndex != null && playlistCount != null) "($playlistIndex/$playlistCount) $title" else title
}

data class ProcessingStep(val name: String) : OutputEvent

data class FileSaved(val path: String) : OutputEvent

/** Reads the lines produced by the templates in [YtDlpArguments]. Port of the Windows app's OutputParser. */
object OutputParser {
    fun parse(line: String): OutputEvent? = when {
        line.startsWith("[dl]") -> parseProgress(line.substring(4))
        line.startsWith("[pp]") -> ProcessingStep(line.substring(4).trim())
        line.startsWith("[file]") -> FileSaved(line.substring(6).trim())
        else -> null
    }

    // downloaded|total|vcodec|playlist index|playlist count|title
    private fun parseProgress(body: String): DownloadProgress? {
        val parts = body.split('|', limit = 6)
        if (parts.size < 6) return null

        val vcodec = parts[2].trim()
        return DownloadProgress(
            downloadedBytes = parseNumber(parts[0])?.toLong() ?: 0,
            totalBytes = parseNumber(parts[1])?.toLong(),
            isVideoStream = vcodec.isNotEmpty() && vcodec != "none" && vcodec != "NA",
            playlistIndex = parseNumber(parts[3])?.toInt(),
            playlistCount = parseNumber(parts[4])?.toInt(),
            title = parts[5].trim(),
        )
    }

    /** The status line under the progress bar, e.g. "Video · 45% · 30.1 of 68.0 MB · 4.4 MB/s · 0:12 left". */
    fun describe(progress: DownloadProgress, format: OutputFormat, reading: TransferReading): String {
        val pieces = mutableListOf<String>()
        if (format == OutputFormat.MP4) pieces += if (progress.isVideoStream) "Video" else "Audio"
        val total = progress.totalBytes
        if (total != null && total > 0) {
            pieces += "${floor(progress.percent).toInt()}%"
            pieces += TransferMeter.formatProgress(progress.downloadedBytes.toDouble(), total.toDouble())
        } else {
            pieces += TransferMeter.formatBytes(progress.downloadedBytes.toDouble())
        }
        reading.bytesPerSecond?.let { pieces += "${TransferMeter.formatBytes(it)}/s" }
        reading.secondsLeft?.let { pieces += "${TransferMeter.formatTimeLeft(it)} left" }
        return pieces.joinToString(" · ")
    }

    fun describeStep(step: String): String = when (step) {
        "ExtractAudio" -> "Converting to MP3…"
        "Merger" -> "Merging video and audio…"
        "ThumbnailsConvertor" -> "Preparing cover art…"
        "EmbedThumbnail" -> "Adding cover art…"
        "Metadata" -> "Writing tags…"
        "MoveFiles" -> "Finishing…"
        else -> "Processing…"
    }

    // yt-dlp prints NA (or nothing, with our "|" defaults) when a number isn't known yet.
    private fun parseNumber(value: String): Double? = value.trim().toDoubleOrNull()?.takeIf { it.isFinite() }
}
