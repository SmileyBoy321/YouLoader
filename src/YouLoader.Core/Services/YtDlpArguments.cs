using YouLoader.Core.Models;

namespace YouLoader.Core.Services;

/// <summary>Turns a download request into a yt-dlp command line.</summary>
public static class YtDlpArguments
{
    public const string SingleTemplate = "%(title)s.%(ext)s";
    public const string CollectionTemplate = "%(playlist_title,playlist_id|Playlist)s/%(playlist_index&{:03d} - |)s%(title)s.%(ext)s";

    // Machine-readable progress lines that OutputParser understands. The title goes last because it may contain '|'.
    public const string DownloadProgressTemplate =
        "download:[dl]%(progress.downloaded_bytes|0)s|%(progress.total_bytes,progress.total_bytes_estimate|)s|%(info.vcodec)s|%(info.playlist_index|)s|%(info.n_entries|)s|%(info.title)s";
    public const string ProcessingTemplate = "postprocess:[pp]%(progress.postprocessor)s";
    public const string SavedFileTemplate = "after_move:[file]%(filepath)s";

    // Crops YouTube's 16:9 thumbnails to a centred square so cover art looks right in music players.
    public const string SquareCoverArt =
        "ThumbnailsConvertor+ffmpeg_o:-c:v mjpeg -vf crop=\"'if(gt(ih,iw),iw,ih)':'if(gt(iw,ih),ih,iw)'\"";

    public static List<string> Build(string url, DownloadRequest request, string ffmpegDir, string? jsRuntime)
    {
        List<string> args =
        [
            "--ignore-config",
            "--encoding", "utf-8",
            "--newline",
            "--no-colors",
            "--no-mtime",
            // Makes ffmpeg's own error messages appear in the log, so failures can be explained instead of just "Conversion failed!".
            "--verbose",
            "--concurrent-fragments", "4",
            "--ffmpeg-location", ffmpegDir,
            "--paths", request.OutputDir,
            "--progress-template", DownloadProgressTemplate,
            "--progress-template", ProcessingTemplate,
            "--print", SavedFileTemplate,
            // --print switches on quiet mode, which also hides progress; turn progress back on.
            "--no-quiet",
            "--progress",
        ];

        if (jsRuntime is not null) args.AddRange(["--js-runtimes", jsRuntime]);

        // Playlists remember what they already fetched, so running the same link again only grabs new items.
        // Single videos are always downloaded fresh: re-tagging an existing file with cover art fails in ffmpeg.
        args.AddRange(LinkParser.IsCollection(url, request.WholePlaylist)
            ?
            [
                "--yes-playlist", "--ignore-errors",
                "--download-archive", Path.Combine(request.OutputDir, ArchiveFileName(request.Format)),
                "--output", CollectionTemplate,
            ]
            : ["--no-playlist", "--force-overwrites", "--output", SingleTemplate]);

        args.AddRange(FormatArguments(request.Format, request.Quality));

        if (request.EmbedArt)
        {
            args.AddRange(["--embed-metadata", "--embed-thumbnail", "--convert-thumbnails", "jpg"]);
            if (request.Format != OutputFormat.Mp4) args.AddRange(["--postprocessor-args", SquareCoverArt]);
        }

        args.AddRange(["--", url]);
        return args;
    }

    public static string ArchiveFileName(OutputFormat format) => $".youloader-{format.ToString().ToLowerInvariant()}.archive";

    public static string[] FormatArguments(OutputFormat format, string quality) => format switch
    {
        OutputFormat.Mp3 =>
            ["--format", "bestaudio/best", "--extract-audio", "--audio-format", "mp3", "--audio-quality", Mp3Quality(quality)],
        OutputFormat.Mp4 =>
            ["--format", "bv*+ba/b", "--format-sort", Mp4Sort(quality), "--merge-output-format", "mp4"],
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    static string Mp3Quality(string quality) => quality switch
    {
        "v0" => "0",
        "192" => "192K",
        _ => "320K",
    };

    // Capped presets prefer H.264 + AAC, which every player on every device can open.
    static string Mp4Sort(string quality) => quality switch
    {
        "1080" or "720" or "480" => $"res:{quality},vcodec:h264,acodec:aac",
        _ => "res,fps,acodec:aac",
    };
}
