using System.Globalization;
using YouLoader.Core.Models;

namespace YouLoader.Core.Services;

public abstract record OutputEvent;

public sealed record DownloadProgress(
    double Percent,
    string? Speed,
    string? Eta,
    bool IsVideoStream,
    int? PlaylistIndex,
    int? PlaylistCount,
    string Title) : OutputEvent
{
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

    static DownloadProgress? ParseProgress(string body)
    {
        var parts = body.Split('|', 7);
        if (parts.Length < 7) return null;

        double.TryParse(parts[0].Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent);
        var vcodec = parts[3].Trim();

        return new DownloadProgress(
            percent,
            Known(parts[1]),
            Known(parts[2]),
            IsVideoStream: vcodec.Length > 0 && vcodec is not ("none" or "NA"),
            ParseInt(parts[4]),
            ParseInt(parts[5]),
            parts[6].Trim());
    }

    public static string Describe(DownloadProgress progress, OutputFormat format)
    {
        List<string> pieces = [];
        if (format == OutputFormat.Mp4) pieces.Add(progress.IsVideoStream ? "Video" : "Audio");
        pieces.Add($"{progress.Percent.ToString("0.0", CultureInfo.InvariantCulture)}%");
        if (progress.Speed is { } speed) pieces.Add(speed);
        if (progress.Eta is { } eta) pieces.Add($"{eta} left");
        return string.Join(" · ", pieces);
    }

    public static string DescribeStep(string step, OutputFormat format) => step switch
    {
        "ExtractAudio" => format == OutputFormat.Mp3 ? "Converting to MP3…" : "Extracting audio…",
        "Merger" => "Merging video and audio…",
        "ThumbnailsConvertor" => "Preparing cover art…",
        "EmbedThumbnail" => "Adding cover art…",
        "Metadata" => "Writing tags…",
        "MoveFiles" => "Finishing…",
        _ => "Processing…",
    };

    // yt-dlp prints NA or Unknown when it can't estimate speed or time left yet.
    static string? Known(string value)
    {
        var trimmed = value.Trim();
        return trimmed is "" or "NA" or "N/A" or "Unknown" or "Unknown B/s" ? null : trimmed;
    }

    static int? ParseInt(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
}
