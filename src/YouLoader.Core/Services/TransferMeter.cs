using static System.FormattableString;

namespace YouLoader.Core.Services;

/// <summary>Speed and time left, ready to show.</summary>
public sealed record TransferReading(double? BytesPerSecond, TimeSpan? TimeLeft);

/// <summary>
/// Smooths download speed and time left so they don't jump around.
/// yt-dlp downloads several chunks at once, so bytes arrive in bursts: its instant speed can swing
/// from 1 to 10 MB/s within a second, and time left with it. This measures the average speed over the
/// last few seconds instead, and only hands out a new reading once per second.
/// </summary>
public sealed class TransferMeter
{
    public static readonly TimeSpan DisplayInterval = TimeSpan.FromSeconds(1);

    static readonly TimeSpan Window = TimeSpan.FromSeconds(5);
    static readonly TimeSpan MinimumSpan = TimeSpan.FromSeconds(1);

    readonly Queue<(DateTime At, long Bytes)> samples = new();
    long lastBytes = -1;
    DateTime lastDisplay;

    /// <summary>
    /// Feeds one progress sample. Returns a reading when it's time to refresh what the user sees,
    /// or null when the previous reading should stay on screen.
    /// </summary>
    public TransferReading? Update(DateTime now, long downloadedBytes, long? totalBytes)
    {
        // Fewer bytes than before means a new file started (the audio after the video, or the next playlist item).
        if (downloadedBytes < lastBytes) Reset();
        lastBytes = downloadedBytes;

        samples.Enqueue((now, downloadedBytes));
        // Keep one sample at or just beyond the window's start, so the window always spans close to its full length.
        while (samples.Count > 2 && samples.ElementAt(1).At <= now - Window) samples.Dequeue();

        var firstReading = lastDisplay == default;
        if (!firstReading && now - lastDisplay < DisplayInterval) return null;
        lastDisplay = now;

        var (oldestAt, oldestBytes) = samples.Peek();
        var span = now - oldestAt;
        // With less than a second of history any estimate is wild (yt-dlp's first reading can claim 19 minutes left),
        // so show no speed or time left at all until there's something real to go on.
        double? speed = span >= MinimumSpan
            ? (downloadedBytes - oldestBytes) / span.TotalSeconds
            : null;

        TimeSpan? timeLeft = null;
        if (speed is > 1 && totalBytes is > 0 && totalBytes >= downloadedBytes)
            timeLeft = TimeSpan.FromSeconds(Math.Ceiling((totalBytes.Value - downloadedBytes) / speed.Value));

        return new TransferReading(speed is > 0 ? speed : null, timeLeft);
    }

    public void Reset()
    {
        samples.Clear();
        lastBytes = -1;
        lastDisplay = default;
    }

    public static string FormatBytes(double bytes) => bytes switch
    {
        >= 1024d * 1024 * 1024 => Invariant($"{bytes / (1024d * 1024 * 1024):0.00} GB"),
        >= 1024d * 1024 => Invariant($"{bytes / (1024d * 1024):0.0} MB"),
        >= 1024 => Invariant($"{bytes / 1024:0} KB"),
        _ => Invariant($"{bytes:0} B"),
    };

    /// <summary>"30.1 of 68.0 MB", or "812 KB of 1.2 GB" when the units differ.</summary>
    public static string FormatProgress(double done, double total)
    {
        var doneText = FormatBytes(done);
        var totalText = FormatBytes(total);
        var unit = totalText[(totalText.LastIndexOf(' ') + 1)..];
        return doneText.EndsWith(" " + unit, StringComparison.Ordinal)
            ? $"{doneText[..^(unit.Length + 1)]} of {totalText}"
            : $"{doneText} of {totalText}";
    }

    public static string FormatTimeLeft(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
        : $"{time.Minutes}:{time.Seconds:00}";
}
