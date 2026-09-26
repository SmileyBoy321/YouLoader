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
    string status = WaitingText;
    double progress;
    DownloadState state = DownloadState.Queued;
    string? filePath;
    bool isHighlighted;
    ErrorExplanation? error;
    string? technicalDetails;
    bool showExplanation;
    bool showTechnicalDetails;

    const string WaitingText = "Waiting for a free slot…";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>When the item joined the line. Waiting items start in this order.</summary>
    public DateTime QueuedAt { get; private set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

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
    /// <summary>Why the download failed, for the “Why?” panel. Null unless the item failed.</summary>
    public ErrorExplanation? Error
    {
        get => error;
        private set
        {
            if (Set(ref error, value)) OnPropertyChanged(nameof(HasExplanation));
        }
    }

    public bool HasExplanation => Error is not null;

    /// <summary>The end of yt-dlp's log, with the user's home folder hidden, for bug reports.</summary>
    public string? TechnicalDetails
    {
        get => technicalDetails;
        private set
        {
            if (Set(ref technicalDetails, value)) OnPropertyChanged(nameof(HasTechnicalDetails));
        }
    }

    public bool HasTechnicalDetails => TechnicalDetails is not null;

    public bool ShowExplanation
    {
        get => showExplanation;
        set => Set(ref showExplanation, value);
    }

    public bool ShowTechnicalDetails
    {
        get => showTechnicalDetails;
        set => Set(ref showTechnicalDetails, value);
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
        StartedAt ??= DateTime.UtcNow;
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
        FinishedAt = DateTime.UtcNow;
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

    public void Fail(ErrorExplanation explanation, string? technicalDetails = null)
    {
        FinishedAt = DateTime.UtcNow;
        Error = explanation;
        TechnicalDetails = string.IsNullOrWhiteSpace(technicalDetails) ? null : technicalDetails;
        State = DownloadState.Failed;
        Status = explanation.Summary;
    }

    public void MarkCanceled()
    {
        FinishedAt = DateTime.UtcNow;
        State = DownloadState.Canceled;
        Status = "Canceled";
    }

    public void Cancel() => Cancellation.Cancel();

    /// <summary>Puts a failed or canceled item back in the queue.</summary>
    public void Reset()
    {
        Cancellation = new CancellationTokenSource();
        Error = null;
        TechnicalDetails = null;
        ShowExplanation = false;
        ShowTechnicalDetails = false;
        SavedFiles = 0;
        FilePath = null;
        Progress = 0;
        QueuedAt = DateTime.UtcNow;
        StartedAt = null;
        FinishedAt = null;
        Status = WaitingText;
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
