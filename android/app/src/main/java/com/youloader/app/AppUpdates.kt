package com.youloader.app

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL

/**
 * Checks GitHub Releases for a newer YouLoader. Port of the Windows app's UpdateChecker.
 * It only reads the public release feed and sends nothing about the user.
 */
object AppUpdates {
    const val SOURCE_URL = "https://github.com/SmileyBoy321/YouLoader"
    private const val RELEASES_API = "https://api.github.com/repos/SmileyBoy321/YouLoader/releases/latest"

    data class Update(val version: String, val url: String)

    suspend fun check(currentVersion: String): Update? = withContext(Dispatchers.IO) {
        try {
            val connection = URL(RELEASES_API).openConnection() as HttpURLConnection
            connection.connectTimeout = 10_000
            connection.readTimeout = 10_000
            connection.setRequestProperty("Accept", "application/vnd.github+json")
            val release = connection.inputStream.bufferedReader().use { JSONObject(it.readText()) }
            val latest = release.optString("tag_name")
            val assets = release.optJSONArray("assets")
            // Windows-only releases have no APK; only offer ones this phone can install.
            val hasApk = assets != null && (0 until assets.length()).any {
                assets.getJSONObject(it).optString("name").endsWith(".apk", ignoreCase = true)
            }
            if (hasApk && isNewer(latest, currentVersion)) Update(latest.trimStart('v', 'V'), release.getString("html_url")) else null
        } catch (e: Exception) {
            null
        }
    }

    /** Compares tags like "v2.1.0" and "2.1". */
    fun isNewer(tag: String, current: String): Boolean {
        val latest = parse(tag) ?: return false
        val mine = parse(current) ?: return false
        for (i in 0 until 3) if (latest[i] != mine[i]) return latest[i] > mine[i]
        return false
    }

    private fun parse(version: String): List<Int>? {
        val parts = version.trim().trimStart('v', 'V').split('.')
        if (parts.isEmpty() || parts.size > 4) return null
        val numbers = parts.map { it.toIntOrNull() ?: return null }
        return (numbers + listOf(0, 0, 0)).take(3)
    }
}
