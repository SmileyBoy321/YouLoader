package com.youloader.app

import android.content.ContentValues
import android.content.Context
import android.media.MediaScannerConnection
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import android.webkit.MimeTypeMap
import androidx.annotation.RequiresApi
import androidx.core.content.FileProvider
import com.youloader.app.core.SavedFile
import java.io.File
import java.io.IOException

/**
 * Moves finished files into the phone's shared Download/YouLoader folder, where every app can see them.
 * yt-dlp works in the app's private cache, because shared storage can't be written directly on Android 10+.
 */
object PublicDownloads {
    const val FOLDER = "YouLoader"
    val displayFolder = "${Environment.DIRECTORY_DOWNLOADS}/$FOLDER"

    /** True when saving needs the storage permission first (Android 9 and older). */
    val needsStoragePermission get() = Build.VERSION.SDK_INT < Build.VERSION_CODES.Q

    /**
     * Moves [file] from [workDir] into Download/YouLoader, keeping its sub-folder (a playlist's folder).
     * A file with the same name is replaced, like on Windows: downloading a video again gives a fresh copy.
     */
    fun save(context: Context, file: File, workDir: File): SavedFile {
        val subFolder = file.parentFile?.relativeTo(workDir)?.path?.takeIf { it.isNotEmpty() && it != "." }
        val relativeDir = listOfNotNull(displayFolder, subFolder).joinToString("/")
        val mime = mimeTypeOf(file.name)
        val saved =
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) saveWithMediaStore(context, file, relativeDir, mime)
            else saveDirectly(context, file, relativeDir, mime)
        file.delete()
        return saved
    }

    fun mimeTypeOf(name: String): String = when (val ext = name.substringAfterLast('.', "").lowercase()) {
        "mp3" -> "audio/mpeg"
        "mp4" -> "video/mp4"
        else -> MimeTypeMap.getSingleton().getMimeTypeFromExtension(ext) ?: "application/octet-stream"
    }

    @RequiresApi(Build.VERSION_CODES.Q)
    private fun saveWithMediaStore(context: Context, file: File, relativeDir: String, mime: String): SavedFile {
        val resolver = context.contentResolver
        val collection = MediaStore.Downloads.EXTERNAL_CONTENT_URI
        removeExisting(context, relativeDir, file.name)

        val values = ContentValues().apply {
            put(MediaStore.MediaColumns.DISPLAY_NAME, file.name)
            put(MediaStore.MediaColumns.MIME_TYPE, mime)
            put(MediaStore.MediaColumns.RELATIVE_PATH, "$relativeDir/")
            put(MediaStore.MediaColumns.IS_PENDING, 1)
        }
        val uri = resolver.insert(collection, values)
            ?: throw IOException("Android refused to create ${file.name} in $relativeDir")
        try {
            resolver.openOutputStream(uri)!!.use { out -> file.inputStream().use { it.copyTo(out) } }
            resolver.update(uri, ContentValues().apply { put(MediaStore.MediaColumns.IS_PENDING, 0) }, null, null)
        } catch (e: Exception) {
            resolver.delete(uri, null, null)
            throw e
        }
        // If an older copy couldn't be removed (it belongs to a previous install), MediaStore adds " (1)" itself.
        val name = resolver.query(uri, arrayOf(MediaStore.MediaColumns.DISPLAY_NAME), null, null, null)?.use {
            if (it.moveToFirst()) it.getString(0) else null
        } ?: file.name
        return SavedFile(uri.toString(), "$relativeDir/$name", mime)
    }

    @RequiresApi(Build.VERSION_CODES.Q)
    private fun removeExisting(context: Context, relativeDir: String, name: String) {
        try {
            context.contentResolver.delete(
                MediaStore.Downloads.EXTERNAL_CONTENT_URI,
                "${MediaStore.MediaColumns.RELATIVE_PATH}=? AND ${MediaStore.MediaColumns.DISPLAY_NAME}=?",
                arrayOf("$relativeDir/", name),
            )
        } catch (e: SecurityException) {
            // Made by a previous install of YouLoader, so Android won't let this one delete it. Keep both.
        }
    }

    @Suppress("DEPRECATION")
    private fun saveDirectly(context: Context, file: File, relativeDir: String, mime: String): SavedFile {
        val folder = File(Environment.getExternalStorageDirectory(), relativeDir)
        if (!folder.isDirectory && !folder.mkdirs()) throw IOException("Couldn't create $relativeDir (Permission denied)")
        val target = File(folder, file.name)
        file.copyTo(target, overwrite = true)
        MediaScannerConnection.scanFile(context, arrayOf(target.absolutePath), arrayOf(mime), null)
        val uri = FileProvider.getUriForFile(context, "${context.packageName}.files", target)
        return SavedFile(uri.toString(), "$relativeDir/${file.name}", mime)
    }
}
