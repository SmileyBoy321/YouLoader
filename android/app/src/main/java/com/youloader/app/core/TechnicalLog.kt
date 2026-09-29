package com.youloader.app.core

/** Keeps the end of yt-dlp's log, for explaining failures and for bug reports. */
class TechnicalLog {
    private val lines = ArrayDeque<String>()

    @Synchronized
    fun add(line: String) {
        lines.addLast(line)
        if (lines.size > MAX_LINES) lines.removeFirst()
    }

    @Synchronized
    fun snapshot(): List<String> = lines.toList()

    companion object {
        private const val MAX_LINES = 120
        private const val SHOWN_LINES = 40
        private const val MAX_LINE_LENGTH = 400

        /** The last lines of the log, ready to copy into a bug report, with the app's private folders shortened. */
        fun format(log: List<String>, privateDirs: List<String> = emptyList()): String {
            var text = log.takeLast(SHOWN_LINES)
                .joinToString("\n") { if (it.length <= MAX_LINE_LENGTH) it else it.take(MAX_LINE_LENGTH) + "…" }
            for (dir in privateDirs.filter { it.isNotEmpty() }) text = text.replace(dir, "<app>")
            return text
        }
    }
}
