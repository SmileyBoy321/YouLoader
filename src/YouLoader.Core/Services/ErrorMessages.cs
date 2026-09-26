namespace YouLoader.Core.Services;

/// <summary>What went wrong, in words a non-technical user can act on.</summary>
/// <param name="Summary">One line shown on the download itself.</param>
/// <param name="Cause">Why it most likely happened.</param>
/// <param name="Suggestions">What the user can do about it, most useful first.</param>
public sealed record ErrorExplanation(string Summary, string Cause, IReadOnlyList<string> Suggestions);

/// <summary>
/// Turns yt-dlp and ffmpeg output into explanations. The whole log is searched, not just the final
/// error line, because the real reason often comes earlier: ffmpeg explains a failure a few lines
/// before yt-dlp reports a bare "Conversion failed!".
/// </summary>
public static class ErrorMessages
{
    const string Retry = "Click Retry.";
    const string UpdateEngine = "Click “Update yt-dlp” at the bottom of the window, then Retry.";
    const string Report = "If it keeps happening, click “Report a problem” and paste the technical details.";

    static readonly (string[] Needles, ErrorExplanation Explanation)[] Known =
    [
        (["being used by another process", "WinError 32"], new(
            "The file is open in another program.",
            "Windows doesn't let YouLoader replace a file while another program, usually a music or video player, has it open.",
            ["Close the program that's playing or showing the file.", Retry])),

        (["Permission denied", "Access is denied", "WinError 5"], new(
            "YouLoader isn't allowed to save in this folder.",
            "Windows blocked writing to the folder. This happens with protected system folders, and when Windows Security's ransomware protection (“Controlled folder access”) doesn't know YouLoader yet.",
            ["Choose a different folder with “Change…”.", "Or allow YouLoader in Windows Security → Virus & threat protection → Ransomware protection → Allow an app through Controlled folder access."])),

        (["No space left", "WinError 112", "not enough space on the disk"], new(
            "Your disk is full.",
            "There wasn't enough free space to finish. Converting needs room for the download and the finished file at the same time.",
            ["Free up some space, or choose a folder on another drive with “Change…”.", Retry])),

        (["WinError 206", "File name too long", "filename or extension is too long"], new(
            "The file name or folder path is too long for Windows.",
            "Windows limits how long a file's full path can be, and this video's title plus your save folder went over it.",
            ["Choose a save folder closer to the top of the drive, such as D:\\Music.", Retry])),

        (["Unsupported codec id", "Could not write header", "incorrect codec parameters", "codec not currently supported in container"], new(
            "The file couldn't be converted to this format.",
            "The converter (ffmpeg) couldn't fit part of the download, usually the cover art or an unusual audio stream, into the chosen format.",
            [Retry, "If it fails again, turn off “Embed cover art & tags”, or pick another format.", Report])),

        (["Invalid data found when processing input", "moov atom not found", "End of file", "partial file", "Truncating packet"], new(
            "The download arrived damaged.",
            "The connection most likely dropped partway through, so the file was incomplete and couldn't be converted.",
            [Retry])),

        (["ffmpeg not found", "ffprobe and ffmpeg not found", "ffmpeg is not installed"], new(
            "The converter (ffmpeg) is missing.",
            "It was probably removed by antivirus software or a cleanup tool. YouLoader needs it to convert and merge files.",
            ["Restart YouLoader. It downloads ffmpeg again automatically.", "If your antivirus removed it, allow the folder %LocalAppData%\\YouLoader\\tools."])),

        (["Conversion failed", "Postprocessing"], new(
            "Converting the file failed.",
            "The converter (ffmpeg) stopped without saying exactly why. The usual reasons are a damaged download or a file that's open in another program.",
            [Retry, "If it fails again, try another format.", Report])),

        (["Private video"], new(
            "This video is private.",
            "Only the uploader and people they've invited can watch it, so there's nothing YouLoader can download.",
            ["Check the link in your browser. If you can't watch it there either, it can't be downloaded."])),

        (["members-only", "Join this channel"], new(
            "This video is for channel members only.",
            "The uploader made it available only to paying members of their channel. YouLoader doesn't sign in to YouTube, so it can't access it.",
            ["There's nothing to fix on your side; this video can't be downloaded."])),

        (["confirm your age", "age-restricted", "inappropriate for some users"], new(
            "This video is age-restricted.",
            "YouTube only shows it to signed-in adults, and YouLoader never signs in to your account.",
            ["There's nothing to fix on your side; age-restricted videos can't be downloaded."])),

        (["not a bot", "Sign in to confirm"], new(
            "YouTube asked for a bot check.",
            "YouTube sometimes asks for this when it sees many downloads from one internet connection in a short time.",
            ["Wait a while (an hour is usually enough) before downloading more.", UpdateEngine])),

        (["HTTP Error 429", "Too Many Requests"], new(
            "YouTube is temporarily limiting your connection.",
            "Too many requests came from your internet connection in a short time, so YouTube is slowing it down for a while.",
            ["Wait 15–30 minutes, then click Retry.", "Download fewer videos at once."])),

        (["HTTP Error 403"], new(
            "YouTube refused the download.",
            "YouTube regularly changes how it delivers videos, and the download engine (yt-dlp) has to catch up. It can also happen briefly when YouTube limits a connection.",
            [UpdateEngine, "If it still fails, wait a few minutes and try again."])),

        (["not available in your country", "blocked it in your country", "not made this video available in your country", "geo restriction", "geo-restricted"], new(
            "This video is blocked in your country.",
            "The uploader or YouTube limited which countries can watch it.",
            ["There's nothing to fix on your side; YouLoader can only download what you could watch in your browser."])),

        (["Premieres in", "live event will begin", "This live event", "Premiere will begin"], new(
            "This video hasn't started yet.",
            "It's a scheduled premiere or live stream, so there's nothing to download until it has aired.",
            ["Try again after the premiere or stream has finished."])),

        (["Video unavailable", "video is unavailable", "This video has been removed", "This video is no longer available"], new(
            "This video is unavailable.",
            "It was removed, made private, or the link is wrong.",
            ["Open the link in your browser to check it still plays.", "Make sure the whole link was copied."])),

        (["Unsupported URL", "is not a valid URL"], new(
            "YouLoader can't download from this link.",
            "The link isn't a video, playlist or channel on a supported site, or part of it is missing.",
            ["Copy the link again from the address bar or the Share button.", "YouTube and SoundCloud links work best."])),

        (["Requested format is not available"], new(
            "This quality isn't available for this video.",
            "The video isn't offered in the format or quality you picked.",
            ["Pick another quality, such as “Best available”.", UpdateEngine])),

        (["getaddrinfo", "Unable to download webpage", "timed out", "Connection reset", "Connection refused", "Connection aborted", "Remote end closed", "IncompleteRead", "Network is unreachable"], new(
            "Couldn't connect.",
            "Your internet connection dropped, or the site didn't respond in time.",
            ["Check that you're online.", Retry])),
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

    /// <summary>Explains a failed download from yt-dlp's error output.</summary>
    public static ErrorExplanation Explain(IEnumerable<string> log, int exitCode)
    {
        // yt-dlp's [debug] lines describe settings and URLs, not what went wrong; searching them only causes false matches.
        var lines = log.Where(l => !l.StartsWith("[debug]", StringComparison.Ordinal)).ToList();
        var text = string.Join('\n', lines);
        foreach (var (needles, explanation) in Known)
        {
            if (needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase))) return explanation;
        }

        var lastError = lines.LastOrDefault(l => l.StartsWith("ERROR:", StringComparison.Ordinal));
        return new ErrorExplanation(
            "Something went wrong.",
            lastError is null
                ? $"The download engine stopped (exit code {exitCode}) without saying why."
                : $"The download engine reported: {Shorten(lastError)}",
            [Retry, UpdateEngine, Report]);
    }

    /// <summary>For when yt-dlp itself couldn't be started.</summary>
    public static ErrorExplanation EngineDidNotStart(string reason) => new(
        "The download engine couldn't start.",
        $"yt-dlp.exe is missing or was blocked, often by antivirus software. Windows said: {reason}",
        ["Restart YouLoader. It downloads yt-dlp again automatically.", "If your antivirus removed it, allow the folder %LocalAppData%\\YouLoader\\tools."]);

    static string Shorten(string line)
    {
        var trimmed = line["ERROR:".Length..].Trim();
        return trimmed.Length <= 300 ? trimmed : trimmed[..299] + "…";
    }
}
