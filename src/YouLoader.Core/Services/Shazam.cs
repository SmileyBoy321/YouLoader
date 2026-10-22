using System.Net;
using System.Text.Json;

namespace YouLoader.Core.Services;

/// <summary>A song Shazam recognised. YouLoader finds it on YouTube by name.</summary>
public sealed record ShazamSong(string Title, string Artist)
{
    public string DisplayName => Artist.Length == 0 ? Title : $"{Artist} – {Title}";

    /// <summary>What yt-dlp is given instead of a link: the best YouTube match, which is usually the official upload.</summary>
    public string SearchQuery => "ytsearch1:" + (Artist.Length == 0 ? Title : $"{Artist} - {Title}");
}

/// <summary>
/// Looks up the song behind a Shazam link. A Shazam link only names a song, it doesn't contain one,
/// so this asks Shazam's public song feed for the title and artist. Nothing about the user is sent.
/// </summary>
public static class Shazam
{
    public const string TrackApi = "https://cdn.shazam.com/discovery/v5/en-US/US/web/-/track/";

    static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <returns>The song, or null when Shazam doesn't know that song number.</returns>
    /// <exception cref="HttpRequestException">Shazam couldn't be reached.</exception>
    public static async Task<ShazamSong?> LookUpAsync(string trackId, CancellationToken ct, HttpClient? http = null)
    {
        using var response = await (http ?? SharedHttp).GetAsync(TrackApi + trackId, ct);
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return ParseTrack(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Reads the title and artist ("subtitle") from Shazam's track feed.</summary>
    public static ShazamSong? ParseTrack(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var title = Text(root, "title");
            return title.Length == 0 ? null : new ShazamSong(title, Text(root, "subtitle"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string Text(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!.Trim()
            : "";

    public static readonly ErrorExplanation UnknownSong = new(
        "Shazam doesn't know this song link.",
        "Shazam didn't return a song for this link. It may have been copied incompletely, or Shazam removed the song.",
        ["Open the song in Shazam again, tap Share, and copy the link once more.", "Or search for the song on YouTube and download that link instead."]);

    public static readonly ErrorExplanation LookupFailed = new(
        "Couldn't reach Shazam to look up the song.",
        "YouLoader asks Shazam for the song's name before finding it on YouTube, and Shazam didn't answer.",
        ["Check that you're connected to the internet.", "Click Retry."]);
}
