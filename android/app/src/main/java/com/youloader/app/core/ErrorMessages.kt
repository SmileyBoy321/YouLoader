package com.youloader.app.core

/**
 * What went wrong, in words a non-technical user can act on.
 * @param summary One line shown on the download itself.
 * @param cause Why it most likely happened.
 * @param suggestions What the user can do about it, most useful first.
 */
data class ErrorExplanation(val summary: String, val cause: String, val suggestions: List<String>)

/**
 * Turns yt-dlp and ffmpeg output into explanations. Port of the Windows app's ErrorMessages, reworded for phones.
 * The whole log is searched, not just the final error line, because the real reason often comes earlier:
 * ffmpeg explains a failure a few lines before yt-dlp reports a bare "Conversion failed!".
 */
object ErrorMessages {
    private const val RETRY = "Tap Retry."
    private const val UPDATE_ENGINE = "Tap ⋮ → Update download engine, then Retry."
    private const val REPORT = "If it keeps happening, tap “Report a problem” and include the technical details."

    private class Known(val id: String, val needles: List<String>, val explanation: ErrorExplanation)

    private val known = listOf(
        Known("storage-full", listOf("No space left", "ENOSPC"), ErrorExplanation(
            "Your phone's storage is full.",
            "There wasn't enough free space to finish. Converting needs room for the download and the finished file at the same time.",
            listOf("Free up some space, for example by deleting old videos or clearing app caches.", RETRY))),

        Known("storage-permission", listOf("Permission denied", "EACCES", "storage access"), ErrorExplanation(
            "YouLoader isn't allowed to save into Downloads.",
            "Android blocked writing the file, usually because storage access was turned off for YouLoader.",
            listOf("Open Settings → Apps → YouLoader → Permissions and allow Storage.", RETRY))),

        Known("name-too-long", listOf("File name too long", "ENAMETOOLONG"), ErrorExplanation(
            "The video's title is too long to use as a file name.",
            "Android limits how long a file name can be, and this title, often one in Japanese, Chinese or with many emoji, went over it.",
            listOf(REPORT))),

        Known("cannot-convert-format", listOf("Unsupported codec id", "Could not write header", "incorrect codec parameters", "codec not currently supported in container"), ErrorExplanation(
            "The file couldn't be converted to this format.",
            "The converter (ffmpeg) couldn't fit part of the download, usually the cover art or an unusual audio stream, into the chosen format.",
            listOf(RETRY, "If it fails again, turn off “Cover art & tags”, or pick another format.", REPORT))),

        Known("damaged-download", listOf("Invalid data found when processing input", "moov atom not found", "End of file", "partial file", "Truncating packet"), ErrorExplanation(
            "The download arrived damaged.",
            "The connection most likely dropped partway through, so the file was incomplete and couldn't be converted.",
            listOf(RETRY))),

        Known("ffmpeg-missing", listOf("ffmpeg not found", "ffprobe and ffmpeg not found", "ffmpeg is not installed"), ErrorExplanation(
            "The converter (ffmpeg) is missing.",
            "Part of YouLoader's installation is damaged, so it can't convert or merge files.",
            listOf("Reinstall YouLoader from the latest release. Your downloads are kept."))),

        Known("conversion-failed", listOf("Conversion failed", "Postprocessing"), ErrorExplanation(
            "Converting the file failed.",
            "The converter (ffmpeg) stopped without saying exactly why. The usual reason is a damaged download.",
            listOf(RETRY, "If it fails again, try another format.", REPORT))),

        Known("private-video", listOf("Private video"), ErrorExplanation(
            "This video is private.",
            "Only the uploader and people they've invited can watch it, so there's nothing YouLoader can download.",
            listOf("Open the link in YouTube. If you can't watch it there either, it can't be downloaded."))),

        Known("members-only", listOf("members-only", "Join this channel"), ErrorExplanation(
            "This video is for channel members only.",
            "The uploader made it available only to paying members of their channel. YouLoader doesn't sign in to YouTube, so it can't access it.",
            listOf("There's nothing to fix on your side; this video can't be downloaded."))),

        Known("age-restricted", listOf("confirm your age", "age-restricted", "inappropriate for some users"), ErrorExplanation(
            "This video is age-restricted.",
            "YouTube only shows it to signed-in adults, and YouLoader never signs in to your account.",
            listOf("There's nothing to fix on your side; age-restricted videos can't be downloaded."))),

        Known("bot-check", listOf("not a bot", "Sign in to confirm"), ErrorExplanation(
            "YouTube asked for a bot check.",
            "YouTube sometimes asks for this when it sees many downloads from one internet connection in a short time.",
            listOf("Wait a while (an hour is usually enough) before downloading more.", "Switching between Wi-Fi and mobile data can also help.", UPDATE_ENGINE))),

        Known("rate-limited", listOf("HTTP Error 429", "Too Many Requests"), ErrorExplanation(
            "YouTube is temporarily limiting your connection.",
            "Too many requests came from your internet connection in a short time, so YouTube is slowing it down for a while.",
            listOf("Wait 15–30 minutes, then tap Retry.", "Download fewer videos at once."))),

        Known("youtube-refused", listOf("HTTP Error 403"), ErrorExplanation(
            "YouTube refused the download.",
            "YouTube regularly changes how it delivers videos, and the download engine (yt-dlp) has to catch up. It can also happen briefly when YouTube limits a connection.",
            listOf(UPDATE_ENGINE, "If it still fails, wait a few minutes and try again."))),

        Known("blocked-in-country", listOf("not available in your country", "blocked it in your country", "not made this video available in your country", "geo restriction", "geo-restricted"), ErrorExplanation(
            "This video is blocked in your country.",
            "The uploader or YouTube limited which countries can watch it.",
            listOf("There's nothing to fix on your side; YouLoader can only download what you could watch in YouTube."))),

        Known("not-started", listOf("Premieres in", "live event will begin", "This live event", "Premiere will begin"), ErrorExplanation(
            "This video hasn't started yet.",
            "It's a scheduled premiere or live stream, so there's nothing to download until it has aired.",
            listOf("Try again after the premiere or stream has finished."))),

        Known("video-unavailable", listOf("Video unavailable", "video is unavailable", "This video has been removed", "This video is no longer available"), ErrorExplanation(
            "This video is unavailable.",
            "It was removed, made private, or the link is wrong.",
            listOf("Open the link in YouTube to check it still plays.", "Make sure the whole link was copied."))),

        Known("unsupported-link", listOf("Unsupported URL", "is not a valid URL"), ErrorExplanation(
            "YouLoader can't download from this link.",
            "The link isn't a YouTube video, playlist or channel, or part of it is missing.",
            listOf("Share the video from the YouTube app again, or copy the link from its Share button."))),

        Known("quality-unavailable", listOf("Requested format is not available"), ErrorExplanation(
            "This quality isn't available for this video.",
            "The video isn't offered in the format or quality you picked.",
            listOf("Pick another quality, such as “Best available”.", UPDATE_ENGINE))),

        Known("cannot-connect", listOf("getaddrinfo", "Unable to download webpage", "timed out", "Connection reset", "Connection refused", "Connection aborted", "Remote end closed", "IncompleteRead", "Network is unreachable", "No address associated with hostname"), ErrorExplanation(
            "Couldn't connect.",
            "Your internet connection dropped, or YouTube didn't respond in time.",
            listOf("Check that you're online, on Wi-Fi or mobile data.", RETRY))),
    )

    private val transientNeedles = listOf(
        "HTTP Error 403",
        "HTTP Error 5",
        "timed out",
        "Connection reset",
        "Remote end closed",
        "IncompleteRead",
        "Connection aborted",
    )

    /** True for network hiccups that usually succeed on a second try. */
    fun isTransient(error: String?): Boolean =
        error != null && transientNeedles.any { error.contains(it, ignoreCase = true) }

    /** Explains a failed download from yt-dlp's output. */
    fun explain(log: List<String>, exitCode: Int?): ErrorExplanation {
        // yt-dlp's [debug] lines describe settings and URLs, not what went wrong; searching them only causes false matches.
        val lines = log.filterNot { it.startsWith("[debug]") }
        val text = lines.joinToString("\n")
        known.firstOrNull { k -> k.needles.any { text.contains(it, ignoreCase = true) } }?.let { return it.explanation }

        val lastError = lines.lastOrNull { it.startsWith("ERROR:") }
        return ErrorExplanation(
            "Something went wrong.",
            if (lastError == null) "The download engine stopped" + (exitCode?.let { " (exit code $it)" } ?: "") + " without saying why."
            else "The download engine reported: ${shorten(lastError)}",
            listOf(RETRY, UPDATE_ENGINE, REPORT),
        )
    }

    /** Every explanation the app can show, keyed by a stable id. */
    val catalogue: List<Pair<String, ErrorExplanation>>
        get() = known.map { it.id to it.explanation } + ("engine-did-not-start" to engineDidNotStart(null))

    /** For when the download engine itself couldn't be started. */
    fun engineDidNotStart(reason: String?) = ErrorExplanation(
        "The download engine couldn't start.",
        "Part of YouLoader's installation is missing or damaged." + (reason?.let { " Android said: $it" } ?: ""),
        listOf("Close YouLoader completely and open it again.", "If it still fails, reinstall YouLoader from the latest release. Your downloads are kept."),
    )

    private fun shorten(line: String): String {
        val trimmed = line.removePrefix("ERROR:").trim()
        return if (trimmed.length <= 300) trimmed else trimmed.take(299) + "…"
    }
}
