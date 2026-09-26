namespace YouLoader.Core.Services;

public static class LinkParser
{
    static readonly char[] Separators = [' ', '\t', '\r', '\n', ','];

    /// <summary>Pulls every http(s) link out of pasted text, whatever separates them.</summary>
    public static IReadOnlyList<string> ExtractUrls(string text) =>
        AllUrls(text).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Every link in the text, repeats included.</summary>
    static IEnumerable<string> AllUrls(string text) =>
        text.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim('<', '>', '"', '\''))
            .Where(IsHttpUrl);

    /// <summary>
    /// Adds pasted links to what's already in the link box, one per line, skipping any that are
    /// already there (however they're written) or repeated within the paste.
    /// </summary>
    public static (string Text, int Added, int Duplicates) MergeLinks(string existing, string pasted)
    {
        var seen = ExtractUrls(existing).Select(u => DownloadKey(u, wholePlaylist: false)).ToHashSet(StringComparer.Ordinal);
        List<string> added = [];
        var duplicates = 0;
        foreach (var url in AllUrls(pasted))
        {
            if (seen.Add(DownloadKey(url, wholePlaylist: false))) added.Add(url);
            else duplicates++;
        }

        if (added.Count == 0) return (existing, 0, duplicates);
        var start = existing.TrimEnd();
        var text = start.Length == 0 ? string.Join(Environment.NewLine, added) : start + Environment.NewLine + string.Join(Environment.NewLine, added);
        return (text, added.Count, duplicates);
    }

    /// <summary>Counts the links in the box and how many of them repeat an earlier one.</summary>
    public static (int Links, int Duplicates) CountLinks(string text)
    {
        var urls = AllUrls(text).ToList();
        var unique = urls.Select(u => DownloadKey(u, wholePlaylist: false)).Distinct(StringComparer.Ordinal).Count();
        return (urls.Count, urls.Count - unique);
    }

    /// <summary>YouLoader only downloads from YouTube (including YouTube Music and youtu.be short links).</summary>
    public static bool IsYouTubeUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsYouTube(uri.Host.ToLowerInvariant());

    public static bool IsHttpUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// True when the link should download many files into their own folder: playlists, channels,
    /// A video opened from inside a playlist only counts when the
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
