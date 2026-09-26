using System.ComponentModel;
using System.Runtime.CompilerServices;
using YouLoader.Core.Services;

namespace YouLoader.Core.Models;

public enum DownloadState
{
    Queued,
    Downloading,
    Processing,
    Done,
    Failed,
    Canceled,
}

/// <summary>One link in the download queue. The UI binds straight to it.</summary>
public sealed class DownloadItem(string url, DownloadRequest request) : INotifyPropertyChanged
{
    string title = url;
    string status = "Waiting…";
    double progress;
    DownloadState state = DownloadState.Queued;
    string? filePath;
    bool isHighlighted;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Url { get; } = url;
    public DownloadRequest Request { get; } = request;
    public string FormatLabel => Request.Format.ToString().ToUpperInvariant();

    /// <summary>Same key = same download: the same video (however the link is written) in the same format.</summary>
    public string Key { get; } = $"{LinkParser.DownloadKey(url, request.WholePlaylist)}|{request.Format}";

    /// <summary>Briefly true to draw attention to this item, e.g. when a duplicate of it was skipped.</summary>
    public bool IsHighlighted
    {
        get => isHighlighted;
        set => Set(ref isHighlighted, value);
    }
    public CancellationTokenSource Cancellation { get; private set; } = new();
    public int SavedFiles { get; private set; }

    public string Title
    {
        get => title;
        set => Set(ref title, value);
    }

    public string Status
    {
        get => status;
        private set => Set(ref status, value);
    }

    public double Progress
    {
        get => progress;
        private set => Set(ref progress, value);
    }

    public string? FilePath
    {
        get => filePath;
        private set => Set(ref filePath, value);
    }

    public DownloadState State
    {
        get => state;
        private set
        {
            if (!Set(ref state, value)) return;
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(IsDone));
            OnPropertyChanged(nameof(CanRetry));
        }
    }

    public bool IsActive => State is DownloadState.Queued or DownloadState.Downloading or DownloadState.Processing;
    public bool IsDone => State == DownloadState.Done;
    public bool CanRetry => State is DownloadState.Failed or DownloadState.Canceled;

    public void ReportStarting()
    {
        State = DownloadState.Downloading;
        Progress = 0;
        Status = "Starting…";
    }

    /// <param name="text">New status text, or null to keep the current one.</param>
    public void ReportDownloading(double percent, string? text)
    {
        State = DownloadState.Downloading;
        Progress = Math.Clamp(percent, 0, 100);
        if (text is not null) Status = text;
    }

    public void ReportRetrying(int attempt, int maxAttempts)
    {
        State = DownloadState.Downloading;
        Progress = 0;
        Status = $"Connection hiccup. Retrying ({attempt}/{maxAttempts})…";
    }

    public void ReportProcessing(string text)
    {
        State = DownloadState.Processing;
        Progress = 100;
        Status = text;
    }

    public void ReportSavedFile(string path)
    {
        FilePath = path;
        SavedFiles++;
    }

    public void Complete(string? warning = null)
    {
        State = DownloadState.Done;
        Progress = 100;
        var saved = SavedFiles switch
        {
            0 => "Up to date · nothing new to download",
            1 => $"Saved · {Path.GetFileName(FilePath)}",
            _ => $"Saved {SavedFiles} files",
        };
        Status = warning is null ? saved : $"{saved} · {warning}";
    }

    public void Fail(string message)
    {
        State = DownloadState.Failed;
        Status = message;
    }

    public void MarkCanceled()
    {
        State = DownloadState.Canceled;
        Status = "Canceled";
    }

    public void Cancel() => Cancellation.Cancel();

    /// <summary>Puts a failed or canceled item back in the queue.</summary>
    public void Reset()
    {
        Cancellation = new CancellationTokenSource();
        SavedFiles = 0;
        FilePath = null;
        Progress = 0;
        Status = "Waiting…";
        State = DownloadState.Queued;
    }

    bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
