using System.Globalization;
using YouLoader.Core.Models;

namespace YouLoader.Core.Services;

public abstract record OutputEvent;

public sealed record DownloadProgress(
    long DownloadedBytes,
    long? TotalBytes,
    bool IsVideoStream,
    int? PlaylistIndex,
    int? PlaylistCount,
    string Title) : OutputEvent
{
    public double Percent => TotalBytes is > 0 ? Math.Clamp(100.0 * DownloadedBytes / TotalBytes.Value, 0, 100) : 0;

    public string DisplayTitle => PlaylistIndex is { } index && PlaylistCount is { } count
        ? $"({index}/{count}) {Title}"
        : Title;
}

public sealed record ProcessingStep(string Name) : OutputEvent;

public sealed record FileSaved(string Path) : OutputEvent;

/// <summary>Reads the lines produced by the templates in <see cref="YtDlpArguments"/>.</summary>
public static class OutputParser
{
    public static OutputEvent? Parse(string line)
    {
        if (line.StartsWith("[dl]", StringComparison.Ordinal)) return ParseProgress(line[4..]);
        if (line.StartsWith("[pp]", StringComparison.Ordinal)) return new ProcessingStep(line[4..].Trim());
        if (line.StartsWith("[file]", StringComparison.Ordinal)) return new FileSaved(line[6..].Trim());
        return null;
    }

    // downloaded|total|vcodec|playlist index|playlist count|title
    static DownloadProgress? ParseProgress(string body)
    {
        var parts = body.Split('|', 6);
        if (parts.Length < 6) return null;

        var vcodec = parts[2].Trim();
        return new DownloadProgress(
            (long)(ParseNumber(parts[0]) ?? 0),
            ParseNumber(parts[1]) is { } total ? (long)total : null,
            IsVideoStream: vcodec.Length > 0 && vcodec is not ("none" or "NA"),
            (int?)ParseNumber(parts[3]),
            (int?)ParseNumber(parts[4]),
            parts[5].Trim());
    }

    /// <summary>The status line under the progress bar, e.g. "Video · 45% · 30.1 of 68.0 MB · 4.4 MB/s · 0:12 left".</summary>
    public static string Describe(DownloadProgress progress, OutputFormat format, TransferReading reading)
    {
        List<string> pieces = [];
        if (format == OutputFormat.Mp4) pieces.Add(progress.IsVideoStream ? "Video" : "Audio");
        if (progress.TotalBytes is > 0)
        {
            pieces.Add($"{Math.Floor(progress.Percent).ToString(CultureInfo.InvariantCulture)}%");
            pieces.Add(TransferMeter.FormatProgress(progress.DownloadedBytes, progress.TotalBytes.Value));
        }
        else
        {
            pieces.Add(TransferMeter.FormatBytes(progress.DownloadedBytes));
        }
        if (reading.BytesPerSecond is { } speed) pieces.Add($"{TransferMeter.FormatBytes(speed)}/s");
        if (reading.TimeLeft is { } left) pieces.Add($"{TransferMeter.FormatTimeLeft(left)} left");
        return string.Join(" · ", pieces);
    }

    public static string DescribeStep(string step, OutputFormat format) => step switch
    {
        "ExtractAudio" => "Converting to MP3…",
        "Merger" => "Merging video and audio…",
        "ThumbnailsConvertor" => "Preparing cover art…",
        "EmbedThumbnail" => "Adding cover art…",
        "Metadata" => "Writing tags…",
        "MoveFiles" => "Finishing…",
        _ => "Processing…",
    };

    // yt-dlp prints NA (or nothing, with our "|" defaults) when a number isn't known yet.
    static double? ParseNumber(string value) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
