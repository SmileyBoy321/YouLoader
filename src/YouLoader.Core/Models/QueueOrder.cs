namespace YouLoader.Core.Models;

/// <summary>
/// How the download list is sorted, so what's happening right now is always at the top:
/// downloading first (in the order they started), then waiting (in the order they'll start),
/// then finished (newest first).
/// </summary>
public static class QueueOrder
{
    public static IReadOnlyList<DownloadItem> Sort(IEnumerable<DownloadItem> items) =>
        [.. items
            .OrderBy(Group)
            .ThenBy(i => Group(i) switch
            {
                0 => (i.StartedAt ?? i.QueuedAt).Ticks,
                1 => i.QueuedAt.Ticks,
                _ => -(i.FinishedAt ?? i.QueuedAt).Ticks,
            })];

    /// <summary>A one-line summary for the list header, e.g. "2 downloading · 3 waiting".</summary>
    public static string Summary(IEnumerable<DownloadItem> items)
    {
        var list = items.ToList();
        var downloading = list.Count(i => i.State is DownloadState.Downloading or DownloadState.Processing);
        var waiting = list.Count(i => i.State == DownloadState.Queued);
        List<string> parts = [];
        if (downloading > 0) parts.Add($"{downloading} downloading");
        if (waiting > 0) parts.Add($"{waiting} waiting");
        return string.Join(" · ", parts);
    }

    static int Group(DownloadItem item) => item.State switch
    {
        DownloadState.Downloading or DownloadState.Processing => 0,
        DownloadState.Queued => 1,
        _ => 2,
    };
}
