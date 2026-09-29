package com.youloader.app.core

import java.util.Locale
import kotlin.math.ceil

/** Speed and time left, ready to show. */
data class TransferReading(val bytesPerSecond: Double?, val secondsLeft: Long?)

/**
 * Smooths download speed and time left so they don't jump around. Port of the Windows app's TransferMeter.
 * yt-dlp downloads several chunks at once, so bytes arrive in bursts: its instant speed can swing
 * from 1 to 10 MB/s within a second, and time left with it. This measures the average speed over the
 * last few seconds instead, and only hands out a new reading once per second.
 */
class TransferMeter {
    private val samples = ArrayDeque<Pair<Long, Long>>() // (at millis, bytes)
    private var lastBytes = -1L
    private var lastDisplay = 0L

    /**
     * Feeds one progress sample. Returns a reading when it's time to refresh what the user sees,
     * or null when the previous reading should stay on screen.
     */
    fun update(nowMillis: Long, downloadedBytes: Long, totalBytes: Long?): TransferReading? {
        // Fewer bytes than before means a new file started (the audio after the video, or the next playlist item).
        if (downloadedBytes < lastBytes) reset()
        lastBytes = downloadedBytes

        samples.addLast(nowMillis to downloadedBytes)
        // Keep one sample at or just beyond the window's start, so the window always spans close to its full length.
        while (samples.size > 2 && samples[1].first <= nowMillis - WINDOW_MS) samples.removeFirst()

        val firstReading = lastDisplay == 0L
        if (!firstReading && nowMillis - lastDisplay < DISPLAY_INTERVAL_MS) return null
        lastDisplay = nowMillis

        val (oldestAt, oldestBytes) = samples.first()
        val span = nowMillis - oldestAt
        // With less than a second of history any estimate is wild (yt-dlp's first reading can claim 19 minutes left),
        // so show no speed or time left at all until there's something real to go on.
        val speed = if (span >= MINIMUM_SPAN_MS) (downloadedBytes - oldestBytes) / (span / 1000.0) else null

        var secondsLeft: Long? = null
        if (speed != null && speed > 1 && totalBytes != null && totalBytes > 0 && totalBytes >= downloadedBytes)
            secondsLeft = ceil((totalBytes - downloadedBytes) / speed).toLong()

        return TransferReading(speed?.takeIf { it > 0 }, secondsLeft)
    }

    fun reset() {
        samples.clear()
        lastBytes = -1
        lastDisplay = 0
    }

    companion object {
        const val DISPLAY_INTERVAL_MS = 1000L
        private const val WINDOW_MS = 5000L
        private const val MINIMUM_SPAN_MS = 1000L

        fun formatBytes(bytes: Double): String = when {
            bytes >= 1024.0 * 1024 * 1024 -> String.format(Locale.ROOT, "%.2f GB", bytes / (1024.0 * 1024 * 1024))
            bytes >= 1024.0 * 1024 -> String.format(Locale.ROOT, "%.1f MB", bytes / (1024.0 * 1024))
            bytes >= 1024 -> String.format(Locale.ROOT, "%.0f KB", bytes / 1024)
            else -> String.format(Locale.ROOT, "%.0f B", bytes)
        }

        /** "30.1 of 68.0 MB", or "812 KB of 1.2 GB" when the units differ. */
        fun formatProgress(done: Double, total: Double): String {
            val doneText = formatBytes(done)
            val totalText = formatBytes(total)
            val unit = totalText.substringAfterLast(' ')
            return if (doneText.endsWith(" $unit")) "${doneText.dropLast(unit.length + 1)} of $totalText"
            else "$doneText of $totalText"
        }

        fun formatTimeLeft(seconds: Long): String {
            val h = seconds / 3600
            val m = (seconds % 3600) / 60
            val s = seconds % 60
            return if (h >= 1) String.format(Locale.ROOT, "%d:%02d:%02d", h, m, s)
            else String.format(Locale.ROOT, "%d:%02d", m, s)
        }
    }
}
