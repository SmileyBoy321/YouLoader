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
            var list = QueryValue(uri, "list");
            var hasList = list is not null;
            if (segments is ["playlist"]) return hasList;
            if (segments.Length > 0 && (segments[0].StartsWith('@') || segments[0] is "channel" or "c" or "user")) return true;
            // A Mix (list=RD...) is YouTube's endless auto-generated radio. Nobody means "download all of it"
            // when they tick the playlist box, so a video opened from a Mix is always just that video.
            return hasList && wholePlaylist && !IsMix(list!);
        }

        if (host == "soundcloud.com" || host.EndsWith(".soundcloud.com", StringComparison.Ordinal))
        {
            if (segments.Length == 1) return true;
            return segments.Length >= 2 && SoundCloudCollections.Contains(segments[1]);
        }

        return false;
    }

    /// <summary>
    /// Identifies what a link downloads, however it's written: youtu.be/ID, youtube.com/watch?v=ID&amp;t=30
    /// and music.youtube.com/watch?v=ID are all the same video.
    /// </summary>
    public static string DownloadKey(string url, bool wholePlaylist)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return url.Trim();

        var host = uri.Host.ToLowerInvariant();
        if (IsYouTube(host))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var list = QueryValue(uri, "list");
            if (list is not null && IsCollection(url, wholePlaylist)) return "youtube:list:" + list;

            var id = host == "youtu.be"
                ? segments.FirstOrDefault()
                : segments.Length >= 2 && segments[0] is "shorts" or "live" or "embed" ? segments[1]
                : QueryValue(uri, "v");
            if (id is not null) return "youtube:" + id;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        var cleanHost = host.StartsWith("www.", StringComparison.Ordinal) || host.StartsWith("m.", StringComparison.Ordinal)
            ? host[(host.IndexOf('.') + 1)..]
            : host;
        return cleanHost + path + uri.Query;
    }

    static bool IsMix(string list) => list.StartsWith("RD", StringComparison.Ordinal);

    static string? QueryValue(Uri uri, string key) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => pair.StartsWith(key + "=", StringComparison.Ordinal) && pair.Length > key.Length + 1)
            .Select(pair => Uri.UnescapeDataString(pair[(key.Length + 1)..]))
            .FirstOrDefault();

    static bool IsYouTube(string host) =>
        host is "youtube.com" or "youtu.be" or "youtube-nocookie.com"
        || host.EndsWith(".youtube.com", StringComparison.Ordinal);
}
