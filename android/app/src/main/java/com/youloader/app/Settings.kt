package com.youloader.app

import android.content.Context
import android.content.SharedPreferences
import com.youloader.app.core.OutputFormat
import com.youloader.app.core.QualityPresets

/** The choices YouLoader remembers between launches. */
class Settings(context: Context) {
    private val prefs: SharedPreferences = context.getSharedPreferences("settings", Context.MODE_PRIVATE)

    var format: OutputFormat
        get() = OutputFormat.entries.firstOrNull { it.name == prefs.getString("format", null) } ?: OutputFormat.MP3
        set(value) = prefs.edit().putString("format", value.name).apply()

    /** Last chosen quality for [format], or its default. */
    fun qualityFor(format: OutputFormat): String =
        QualityPresets.find(format, prefs.getString("quality.${format.name}", null)).key

    fun setQuality(format: OutputFormat, key: String) = prefs.edit().putString("quality.${format.name}", key).apply()

    var wholePlaylist: Boolean
        get() = prefs.getBoolean("wholePlaylist", false)
        set(value) = prefs.edit().putBoolean("wholePlaylist", value).apply()

    var embedArt: Boolean
        get() = prefs.getBoolean("embedArt", true)
        set(value) = prefs.edit().putBoolean("embedArt", value).apply()

    /**
     * The clipboard text YouLoader last picked up by itself. Kept across restarts: Android often closes apps
     * in the background, and the same old link shouldn't reappear in the box every time.
     */
    var lastClipboardLink: String?
        get() = prefs.getString("lastClipboardLink", null)
        set(value) = prefs.edit().putString("lastClipboardLink", value).apply()

    var lastEngineUpdate: Long
        get() = prefs.getLong("lastEngineUpdate", 0)
        set(value) = prefs.edit().putLong("lastEngineUpdate", value).apply()
}
