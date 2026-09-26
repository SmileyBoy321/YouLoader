namespace YouLoader.Core.Services;

/// <summary>Keeps the end of yt-dlp's log, for explaining failures and for bug reports.</summary>
public sealed class TechnicalLog
{
    const int MaxLines = 120;
    const int ShownLines = 40;
    const int MaxLineLength = 400;

    readonly Queue<string> lines = new();

    public IReadOnlyList<string> Lines => [.. lines];

    public void Add(string line)
    {
        lines.Enqueue(line);
        if (lines.Count > MaxLines) lines.Dequeue();
    }

    /// <summary>The last lines of the log, ready to copy into a bug report, with the user's home folder hidden.</summary>
    public static string Format(IReadOnlyList<string> log, string? homeFolder = null)
    {
        var text = string.Join(Environment.NewLine, log
            .Skip(Math.Max(0, log.Count - ShownLines))
            .Select(l => l.Length <= MaxLineLength ? l : l[..MaxLineLength] + "…"));
        return HideHomeFolder(text, homeFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    /// <summary>
    /// Replaces C:\Users\name with %USERPROFILE%, however it's written (yt-dlp prints Python-style paths with
    /// doubled backslashes), so sharing the log doesn't reveal the Windows user name.
    /// </summary>
    public static string HideHomeFolder(string text, string homeFolder)
    {
        if (string.IsNullOrEmpty(homeFolder)) return text;
        foreach (var variant in new[] { homeFolder.Replace("\\", "\\\\"), homeFolder, homeFolder.Replace('\\', '/') })
            text = text.Replace(variant, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return text;
    }
}
