namespace YouLoader.Core.Models;

public enum OutputFormat
{
    Mp3,
    Mp4,
}

public sealed record QualityPreset(string Key, string Label);

public static class QualityPresets
{
    static readonly QualityPreset[] Mp3 =
    [
        new("320", "320 kbps (best)"),
        new("v0", "~245 kbps VBR (smaller)"),
        new("192", "192 kbps (smallest)"),
    ];

    static readonly QualityPreset[] Mp4 =
    [
        new("best", "Best available (up to 4K/8K)"),
        new("1080", "1080p (plays everywhere)"),
        new("720", "720p"),
        new("480", "480p"),
    ];

    public static IReadOnlyList<QualityPreset> For(OutputFormat format) => format switch
    {
        OutputFormat.Mp3 => Mp3,
        OutputFormat.Mp4 => Mp4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>Returns the preset with this key, or the format's default when the key is unknown.</summary>
    public static QualityPreset Find(OutputFormat format, string? key) =>
        For(format).FirstOrDefault(p => p.Key == key) ?? For(format)[0];
}
