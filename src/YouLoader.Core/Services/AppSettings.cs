using System.Text.Json;
using System.Text.Json.Serialization;
using YouLoader.Core.Models;

namespace YouLoader.Core.Services;

public sealed class AppSettings
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new FormatConverter() },
    };

    public string OutputDir { get; set; } = DefaultOutputDir;
    public OutputFormat Format { get; set; } = OutputFormat.Mp3;

    /// <summary>Last chosen quality per format, keyed by format name.</summary>
    public Dictionary<string, string> Quality { get; set; } = [];

    public bool WholePlaylist { get; set; }
    public bool EmbedArt { get; set; } = true;
    public DateTime LastToolUpdateUtc { get; set; }

    public static string DefaultOutputDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "YouLoader");

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YouLoader", "settings.json");

    /// <summary>
    /// Reads a format by name, falling back to MP3 for one this version doesn't have (Opus was removed),
    /// so an old settings file keeps its save folder and other choices instead of failing to load.
    /// </summary>
    sealed class FormatConverter : JsonConverter<OutputFormat>
    {
        public override OutputFormat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Enum.TryParse<OutputFormat>(reader.GetString(), ignoreCase: true, out var format) && Enum.IsDefined(format)
                ? format
                : OutputFormat.Mp3;

        public override void Write(Utf8JsonWriter writer, OutputFormat value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    public string QualityFor(OutputFormat format) =>
        QualityPresets.Find(format, Quality.GetValueOrDefault(format.ToString())).Key;

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A broken settings file shouldn't stop the app from opening; fall back to defaults.
        }
        return new AppSettings();
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember settings is not worth interrupting the user for.
        }
    }
}
