namespace YouLoader.Core.Models;

public enum OutputFormat
{
    Mp3,
    Mp4,
}

/// <param name="Hint">One plain-language sentence shown under the quality buttons when this preset is picked.</param>
public sealed record QualityPreset(string Key, string Label, string Hint);

public static class QualityPresets
{
    static readonly QualityPreset[] Mp3 =
    [
        new("320", "320 kbps (best)",
            "kbps is how much sound is kept per second: more means more detail and bigger files. 320 is the most MP3 can hold and sounds the same as YouTube. About 2.4 MB per minute."),
        new("v0", "~245 kbps VBR (smaller)",
            "VBR (variable bitrate) spends more data on busy parts of a song and less on quiet ones. Sounds the same to almost everyone, with files about 25% smaller than 320 kbps."),
        new("192", "192 kbps (smallest)",
            "The smallest files. Fine for podcasts, talk and older players with little space; some detail is lost in music."),
    ];

    static readonly QualityPreset[] Mp4 =
    [
        new("best", "Best available (up to 4K/8K)",
            "The highest resolution the video has. 4K and 8K usually use the AV1 format, which needs Windows 11 or VLC on older PCs."),
        new("1080", "1080p (plays everywhere)",
            "Full HD in H.264, which opens on every phone, TV and video editor."),
        new("720", "720p",
            "HD at about half the size of 1080p."),
        new("480", "480p",
            "Small files for phones and slow connections."),
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
