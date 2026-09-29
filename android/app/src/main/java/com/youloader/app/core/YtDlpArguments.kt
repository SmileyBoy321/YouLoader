package com.youloader.app.core

/**
 * Turns a download request into yt-dlp options. Port of the Windows app's YtDlpArguments.
 * The URL itself and --ffmpeg-location / --js-runtimes are added by youtubedl-android.
 */
object YtDlpArguments {
    const val SINGLE_TEMPLATE = "%(title)s.%(ext)s"
    const val COLLECTION_TEMPLATE =
        "%(playlist_title,playlist_id|Playlist)s/%(playlist_index&{:03d} - |)s%(title)s.%(ext)s"

    // Machine-readable progress lines that OutputParser understands. The title goes last because it may contain '|'.
    const val DOWNLOAD_PROGRESS_TEMPLATE =
        "download:[dl]%(progress.downloaded_bytes|0)s|%(progress.total_bytes,progress.total_bytes_estimate|)s|%(info.vcodec)s|%(info.playlist_index|)s|%(info.n_entries|)s|%(info.title)s"
    const val PROCESSING_TEMPLATE = "postprocess:[pp]%(progress.postprocessor)s"
    const val SAVED_FILE_TEMPLATE = "after_move:[file]%(filepath)s"

    // Crops YouTube's 16:9 thumbnails to a centred square so cover art looks right in music players.
    const val SQUARE_COVER_ART =
        "ThumbnailsConvertor+ffmpeg_o:-c:v mjpeg -vf crop=\"'if(gt(ih,iw),iw,ih)':'if(gt(iw,ih),ih,iw)'\""

    /**
     * @param workDir where yt-dlp writes; finished files are moved to Downloads from there.
     * @param archiveFile remembers which playlist items were already fetched, so only new ones download.
     */
    fun build(url: String, request: DownloadRequest, workDir: String, archiveFile: String): List<String> {
        val args = mutableListOf(
            "--ignore-config",
            "--encoding", "utf-8",
            "--newline",
            "--no-colors",
            "--no-mtime",
            // Makes ffmpeg's own error messages appear in the log, so failures can be explained instead of just "Conversion failed!".
            "--verbose",
            "--concurrent-fragments", "4",
            // Android's shared storage (and PCs the files get copied to) can't hold names with : ? * " < > | in them.
            "--windows-filenames",
            "--paths", workDir,
            "--progress-template", DOWNLOAD_PROGRESS_TEMPLATE,
            "--progress-template", PROCESSING_TEMPLATE,
            "--print", SAVED_FILE_TEMPLATE,
            // --print switches on quiet mode, which also hides progress; turn progress back on.
            "--no-quiet",
            "--progress",
        )

        // Playlists remember what they already fetched, so running the same link again only grabs new items.
        // Single videos are always downloaded fresh: re-tagging an existing file with cover art fails in ffmpeg.
        args += if (LinkParser.isCollection(url, request.wholePlaylist)) {
            listOf("--yes-playlist", "--ignore-errors", "--download-archive", archiveFile, "--output", COLLECTION_TEMPLATE)
        } else {
            listOf("--no-playlist", "--force-overwrites", "--output", SINGLE_TEMPLATE)
        }

        args += formatArguments(request.format, request.quality)

        if (request.embedArt) {
            args += listOf("--embed-metadata", "--embed-thumbnail", "--convert-thumbnails", "jpg")
            if (request.format != OutputFormat.MP4) args += listOf("--postprocessor-args", SQUARE_COVER_ART)
        }
        return args
    }

    fun archiveFileName(format: OutputFormat) = ".youloader-${format.name.lowercase()}.archive"

    fun formatArguments(format: OutputFormat, quality: String): List<String> = when (format) {
        OutputFormat.MP3 ->
            listOf("--format", "bestaudio/best", "--extract-audio", "--audio-format", "mp3", "--audio-quality", mp3Quality(quality))
        OutputFormat.MP4 ->
            listOf("--format", "bv*+ba/b", "--format-sort", mp4Sort(quality), "--merge-output-format", "mp4")
    }

    private fun mp3Quality(quality: String) = when (quality) {
        "v0" -> "0"
        "192" -> "192K"
        else -> "320K"
    }

    // Capped presets prefer H.264 + AAC, which every player on every device can open.
    private fun mp4Sort(quality: String) = when (quality) {
        "1080", "720", "480" -> "res:$quality,vcodec:h264,acodec:aac"
        else -> "res,fps,acodec:aac"
    }
}
