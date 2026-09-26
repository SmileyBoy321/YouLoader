using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace YouLoader.Core.Services;

public sealed record AppUpdate(Version Version, string Url);

/// <summary>Checks GitHub Releases for a newer YouLoader. It only reads the public release feed and sends nothing about the user.</summary>
public static class UpdateChecker
{
    public const string ReleasesApi = "https://api.github.com/repos/SmileyBoy321/YouLoader/releases/latest";

    public static async Task<AppUpdate?> CheckAsync(Version current, HttpClient http, CancellationToken ct = default)
    {
        try
        {
            var release = await http.GetFromJsonAsync<Release>(ReleasesApi, ct);
            if (release is null || !TryParseTag(release.TagName, out var latest)) return null;
            return latest > Normalize(current) ? new AppUpdate(latest, release.HtmlUrl) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Parses tags like "v2.1.0" or "2.1".</summary>
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        if (!Version.TryParse(tag.Trim().TrimStart('v', 'V'), out var parsed)) return false;
        version = Normalize(parsed);
        return true;
    }

    // Treat 2.1 and 2.1.0.0 as equal by dropping undefined parts.
    static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    sealed record Release(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl);
}
