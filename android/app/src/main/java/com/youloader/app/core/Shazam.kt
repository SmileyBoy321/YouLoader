package com.youloader.app.core

import org.json.JSONException
import org.json.JSONObject
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL

/** A song Shazam recognised. YouLoader finds it on YouTube by name. */
data class ShazamSong(val title: String, val artist: String) {
    val displayName get() = if (artist.isEmpty()) title else "$artist – $title"

    /** What yt-dlp is given instead of a link: the best YouTube match, which is usually the official upload. */
    val searchQuery get() = "ytsearch1:" + if (artist.isEmpty()) title else "$artist - $title"
}

/**
 * Looks up the song behind a Shazam link. Port of the Windows app's Shazam. A Shazam link only names
 * a song, it doesn't contain one, so this asks Shazam's public song feed for the title and artist.
 * Nothing about the user is sent.
 */
object Shazam {
    const val TRACK_API = "https://cdn.shazam.com/discovery/v5/en-US/US/web/-/track/"

    /**
     * Blocks until Shazam answers.
     * @return the song, or null when Shazam doesn't know that song number.
     * @throws IOException when Shazam couldn't be reached.
     */
    fun lookUp(trackId: String): ShazamSong? {
        val connection = URL(TRACK_API + trackId).openConnection() as HttpURLConnection
        connection.connectTimeout = 15_000
        connection.readTimeout = 15_000
        try {
            val code = connection.responseCode
            if (code == HttpURLConnection.HTTP_NO_CONTENT || code == HttpURLConnection.HTTP_NOT_FOUND) return null
            if (code !in 200..299) throw IOException("Shazam answered HTTP $code")
            return parseTrack(connection.inputStream.bufferedReader().use { it.readText() })
        } finally {
            connection.disconnect()
        }
    }

    /** Reads the title and artist ("subtitle") from Shazam's track feed. */
    fun parseTrack(json: String): ShazamSong? {
        val root = try {
            JSONObject(json)
        } catch (e: JSONException) {
            return null
        }
        val title = root.optString("title").trim()
        return if (title.isEmpty()) null else ShazamSong(title, root.optString("subtitle").trim())
    }

    val unknownSong = ErrorExplanation(
        "Shazam doesn't know this song link.",
        "Shazam didn't return a song for this link. It may have been copied incompletely, or Shazam removed the song.",
        listOf("Open the song in Shazam again, tap Share, and share it to YouLoader once more.", "Or search for the song on YouTube and download that link instead."),
    )

    val lookupFailed = ErrorExplanation(
        "Couldn't reach Shazam to look up the song.",
        "YouLoader asks Shazam for the song's name before finding it on YouTube, and Shazam didn't answer.",
        listOf("Check that you're connected to the internet.", "Tap Retry."),
    )
}
