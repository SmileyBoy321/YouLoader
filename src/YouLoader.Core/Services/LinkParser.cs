namespace YouLoader.Core.Services;

public static class LinkParser
{
    static readonly char[] Separators = [' ', '\t', '\r', '\n', ','];

    static readonly string[] SoundCloudCollections = ["sets", "tracks", "likes", "albums", "reposts", "popular-tracks"];

    /// <summary>Pulls every http(s) link out of pasted text, whatever separates them.</summary>
    public static IReadOnlyList<string> ExtractUrls(string text) =>
        text.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim('<', '>', '"', '\''))
            .Where(IsHttpUrl)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    public static bool IsHttpUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// True when the link should download many files into their own folder: playlists, channels,
    /// SoundCloud sets and artist pages. A video opened from inside a playlist only counts when the
    /// user asked for the whole playlist.
    /// </summary>
    public static bool IsCollection(string url, bool wholePlaylist)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (IsYouTube(host))
        {
            var hasList = HasQueryValue(uri, "list");
            if (segments is ["playlist"]) return hasList;
            if (segments.Length > 0 && (segments[0].StartsWith('@') || segments[0] is "channel" or "c" or "user")) return true;
            return hasList && wholePlaylist;
        }

        if (host == "soundcloud.com" || host.EndsWith(".soundcloud.com", StringComparison.Ordinal))
        {
            if (segments.Length == 1) return true;
            return segments.Length >= 2 && SoundCloudCollections.Contains(segments[1]);
        }

        return false;
    }

    static bool IsYouTube(string host) =>
        host is "youtube.com" or "youtu.be" or "youtube-nocookie.com"
        || host.EndsWith(".youtube.com", StringComparison.Ordinal);

    static bool HasQueryValue(Uri uri, string key) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Any(pair => pair.StartsWith(key + "=", StringComparison.Ordinal) && pair.Length > key.Length + 1);
}
