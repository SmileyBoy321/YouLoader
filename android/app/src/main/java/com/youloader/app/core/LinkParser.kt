package com.youloader.app.core

import java.net.URI
import java.net.URLDecoder

/** Port of the Windows app's LinkParser: finds, compares and classifies YouTube links. */
object LinkParser {
    private val separators = Regex("""[ \t\r\n,]+""")

    /** Pulls every http(s) link out of pasted text, whatever separates them. */
    fun extractUrls(text: String): List<String> = allUrls(text).distinct()

    /** Every link in the text, repeats included. */
    private fun allUrls(text: String): List<String> =
        text.split(separators)
            .map { it.trim('<', '>', '"', '\'') }
            .filter { it.isNotEmpty() && isHttpUrl(it) }

    data class Merge(val text: String, val added: Int, val duplicates: Int)

    /**
     * Adds pasted links to what's already in the link box, one per line, skipping any that are
     * already there (however they're written) or repeated within the paste.
     */
    fun mergeLinks(existing: String, pasted: String): Merge {
        val seen = extractUrls(existing).map { downloadKey(it, wholePlaylist = false) }.toHashSet()
        val added = mutableListOf<String>()
        var duplicates = 0
        for (url in allUrls(pasted)) {
            if (seen.add(downloadKey(url, wholePlaylist = false))) added += url else duplicates++
        }

        if (added.isEmpty()) return Merge(existing, 0, duplicates)
        val start = existing.trimEnd()
        val text = if (start.isEmpty()) added.joinToString("\n") else start + "\n" + added.joinToString("\n")
        return Merge(text, added.size, duplicates)
    }

    /** Counts the links in the box and how many of them repeat an earlier one. */
    fun countLinks(text: String): Pair<Int, Int> {
        val urls = allUrls(text)
        val unique = urls.map { downloadKey(it, wholePlaylist = false) }.distinct().size
        return urls.size to urls.size - unique
    }

    /** YouLoader only downloads from YouTube (including YouTube Music and youtu.be short links). */
    fun isYouTubeUrl(url: String): Boolean = parse(url)?.host?.lowercase()?.let(::isYouTube) ?: false

    fun isHttpUrl(text: String): Boolean {
        val uri = parse(text) ?: return false
        return (uri.scheme == "http" || uri.scheme == "https") && !uri.host.isNullOrEmpty()
    }

    /**
     * True when the link should download many files into their own folder: playlists and channels.
     * A video opened from inside a playlist only counts when the user asked for the whole playlist.
     */
    fun isCollection(url: String, wholePlaylist: Boolean): Boolean {
        val uri = parse(url) ?: return false
        val host = uri.host?.lowercase() ?: return false
        if (!isYouTube(host)) return false

        val segments = segments(uri)
        val list = queryValue(uri, "list")
        if (segments == listOf("playlist")) return list != null
        if (segments.isNotEmpty() && (segments[0].startsWith("@") || segments[0] in setOf("channel", "c", "user"))) return true
        // A Mix (list=RD...) is YouTube's endless auto-generated radio. Nobody means "download all of it"
        // when they tick the playlist box, so a video opened from a Mix is always just that video.
        return list != null && wholePlaylist && !isMix(list)
    }

    /**
     * Identifies what a link downloads, however it's written: youtu.be/ID, youtube.com/watch?v=ID&t=30
     * and music.youtube.com/watch?v=ID are all the same video.
     */
    fun downloadKey(url: String, wholePlaylist: Boolean): String {
        val trimmed = url.trim()
        val uri = parse(trimmed) ?: return trimmed
        val host = uri.host?.lowercase() ?: return trimmed

        if (isYouTube(host)) {
            val segments = segments(uri)
            val list = queryValue(uri, "list")
            if (list != null && isCollection(trimmed, wholePlaylist)) return "youtube:list:$list"

            val id = when {
                host == "youtu.be" -> segments.firstOrNull()
                segments.size >= 2 && segments[0] in setOf("shorts", "live", "embed") -> segments[1]
                else -> queryValue(uri, "v")
            }
            if (id != null) return "youtube:$id"
        }

        val path = (uri.rawPath ?: "").trimEnd('/')
        val cleanHost = if (host.startsWith("www.") || host.startsWith("m.")) host.substringAfter('.') else host
        return cleanHost + path + (uri.rawQuery?.let { "?$it" } ?: "")
    }

    private fun isMix(list: String) = list.startsWith("RD")

    private fun segments(uri: URI) = (uri.rawPath ?: "").split('/').filter { it.isNotEmpty() }

    private fun queryValue(uri: URI, key: String): String? =
        (uri.rawQuery ?: "").split('&')
            .firstOrNull { it.startsWith("$key=") && it.length > key.length + 1 }
            ?.let { URLDecoder.decode(it.substring(key.length + 1), "UTF-8") }

    private fun isYouTube(host: String) =
        host == "youtube.com" || host == "youtu.be" || host == "youtube-nocookie.com" || host.endsWith(".youtube.com")

    private fun parse(text: String): URI? =
        try {
            URI(text.trim()).takeIf { it.isAbsolute }
        } catch (e: Exception) {
            null
        }
}
