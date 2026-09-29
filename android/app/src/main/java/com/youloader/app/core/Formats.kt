package com.youloader.app.core

enum class OutputFormat {
    MP3,
    MP4;

    val label get() = name
}

/** @param hint One plain-language sentence shown under the quality picker when this preset is chosen. */
data class QualityPreset(val key: String, val label: String, val hint: String)

/** Same presets as the Windows app, with the hints reworded for phones. */
object QualityPresets {
    private val mp3 = listOf(
        QualityPreset(
            "320", "320 kbps (best)",
            "kbps is how much sound is kept per second: more means more detail and bigger files. " +
                "320 is the most MP3 can hold and sounds the same as YouTube. About 2.4 MB per minute.",
        ),
        QualityPreset(
            "v0", "~245 kbps VBR (smaller)",
            "VBR (variable bitrate) spends more data on busy parts of a song and less on quiet ones. " +
                "Sounds the same to almost everyone, with files about 25% smaller than 320 kbps.",
        ),
        QualityPreset(
            "192", "192 kbps (smallest)",
            "The smallest files. Fine for podcasts and talk; some detail is lost in music.",
        ),
    )

    private val mp4 = listOf(
        QualityPreset(
            "1080", "1080p (plays everywhere)",
            "Full HD in H.264, which plays on every phone, TV and video editor.",
        ),
        QualityPreset(
            "best", "Best available (up to 4K/8K)",
            "The highest resolution the video has. 4K and 8K usually use the AV1 format, " +
                "which older phones can't play smoothly. Files are much bigger.",
        ),
        QualityPreset("720", "720p", "HD at about half the size of 1080p."),
        QualityPreset("480", "480p", "Small files for slow connections and phones with little space."),
    )

    fun forFormat(format: OutputFormat): List<QualityPreset> = when (format) {
        OutputFormat.MP3 -> mp3
        OutputFormat.MP4 -> mp4
    }

    /** Returns the preset with this key, or the format's default when the key is unknown. */
    fun find(format: OutputFormat, key: String?): QualityPreset =
        forFormat(format).firstOrNull { it.key == key } ?: forFormat(format)[0]
}

/** What the user asked for when they pressed Download. */
data class DownloadRequest(
    val format: OutputFormat,
    val quality: String,
    val wholePlaylist: Boolean,
    val embedArt: Boolean,
)
