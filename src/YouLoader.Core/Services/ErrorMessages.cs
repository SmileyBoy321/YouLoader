using System.Text.RegularExpressions;

namespace YouLoader.Core.Services;

/// <summary>Turns yt-dlp's error lines into something a non-technical user can act on.</summary>
public static partial class ErrorMessages
{
    const int MaxLength = 200;

    static readonly (string Needle, string Message)[] Known =
    [
        ("Private video", "This video is private."),
        ("confirm your age", "This video is age-restricted, so YouTube requires signing in."),
        ("not a bot", "YouTube asked for a bot check. Wait a few minutes, click “Update yt-dlp” and retry."),
        ("HTTP Error 403", "YouTube refused the download. Click “Update yt-dlp” and retry."),
        ("Video unavailable", "This video is unavailable. It may be removed or blocked in your country."),
        ("Unsupported URL", "That link isn't supported. Paste a YouTube or SoundCloud link."),
        ("is not a valid URL", "That doesn't look like a valid link."),
        ("Requested format is not available", "That quality isn't available for this video. Try another quality."),
        ("No space left", "Your disk is full."),
        ("Conversion failed", "The download worked, but converting it failed. Click Retry, or try another format."),
        ("ffmpeg not found", "A helper tool (ffmpeg) is missing. Click “Retry setup” at the bottom of the window."),
        ("getaddrinfo", "Couldn't reach the site. Check your internet connection."),
        ("Unable to download webpage", "Couldn't reach the site. Check your internet connection."),
    ];

    static readonly string[] TransientNeedles =
    [
        "HTTP Error 403",
        "HTTP Error 5",
        "timed out",
        "Connection reset",
        "Remote end closed",
        "IncompleteRead",
        "Connection aborted",
    ];

    /// <summary>True for network hiccups that usually succeed on a second try.</summary>
    public static bool IsTransient(string? error) =>
        error is not null && TransientNeedles.Any(n => error.Contains(n, StringComparison.OrdinalIgnoreCase));

    public static string Friendly(string? error, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(error)) return $"Download failed (yt-dlp exit code {exitCode}).";

        foreach (var (needle, message) in Known)
        {
            if (error.Contains(needle, StringComparison.OrdinalIgnoreCase)) return message;
        }

        var cleaned = ErrorPrefix().Replace(error.Trim(), "");
        return cleaned.Length <= MaxLength ? cleaned : cleaned[..(MaxLength - 1)] + "…";
    }

    // "ERROR: [youtube] dQw4w9WgXcQ: Something" -> "Something"
    [GeneratedRegex(@"^ERROR:\s*(\[[^\]]+\]\s*)?([A-Za-z0-9_-]{6,}:\s+)?")]
    private static partial Regex ErrorPrefix();
}
