using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using YouLoader.Core.Models;

namespace YouLoader.Core.Services;

/// <summary>Runs one yt-dlp process per queue item and feeds its progress back into the item.</summary>
public sealed class DownloadService(ToolManager tools)
{
    public const int MaxAttempts = 3;

    /// <remarks>
    /// Call this from the UI thread: every await resumes there, so the item's
    /// property changes reach data bindings on the right thread.
    /// </remarks>
    public async Task RunAsync(DownloadItem item)
    {
        var token = item.Cancellation.Token;
        item.ReportStarting();

        for (var attempt = 1; ; attempt++)
        {
            if (token.IsCancellationRequested)
            {
                item.MarkCanceled();
                break;
            }

            var result = await RunOnceAsync(item, token);
            if (result is null) break;

            var (exitCode, lastError) = result.Value;
            if (token.IsCancellationRequested) item.MarkCanceled();
            else if (item.SavedFiles > 0) item.Complete(exitCode == 0 ? null : "some items failed");
            else if (exitCode == 0 && LinkParser.IsCollection(item.Url, item.Request.WholePlaylist)) item.Complete();
            else if (attempt < MaxAttempts && ErrorMessages.IsTransient(lastError))
            {
                // YouTube sometimes drops a connection or refuses a stream mid-download; a fresh attempt usually works.
                item.ReportRetrying(attempt + 1, MaxAttempts);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), token);
                }
                catch (OperationCanceledException)
                {
                    // Handled at the top of the loop.
                }
                continue;
            }
            else item.Fail(ErrorMessages.Friendly(lastError, exitCode));
            break;
        }

        HideArchiveFile(item.Request);
    }

    /// <summary>Runs yt-dlp once. Returns null when it couldn't even start (the item is already marked failed).</summary>
    async Task<(int ExitCode, string? LastError)?> RunOnceAsync(DownloadItem item, CancellationToken token)
    {
        Process process;
        try
        {
            Directory.CreateDirectory(item.Request.OutputDir);
            process = Process.Start(CreateStartInfo(item)) ?? throw new InvalidOperationException("Process.Start returned null.");
        }
        catch (Exception e) when (e is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            item.Fail($"Couldn't start the download: {e.Message}");
            return null;
        }

        using (process)
        {
            string? lastError = null;
            var meter = new TransferMeter();
            using (token.Register(() => Kill(process)))
            {
                var stdout = PumpAsync(process.StandardOutput, line => Apply(item, OutputParser.Parse(line), meter));
                var stderr = PumpAsync(process.StandardError, line =>
                {
                    if (line.StartsWith("ERROR:", StringComparison.Ordinal)) lastError = line;
                });
                await Task.WhenAll(stdout, stderr);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            return (process.ExitCode, lastError);
        }
    }

    // The playlist archive is bookkeeping, not something the user needs to see in their music folder.
    static void HideArchiveFile(DownloadRequest request)
    {
        var archive = Path.Combine(request.OutputDir, YtDlpArguments.ArchiveFileName(request.Format));
        try
        {
            if (File.Exists(archive)) File.SetAttributes(archive, File.GetAttributes(archive) | FileAttributes.Hidden);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Visible is fine too.
        }
    }

    ProcessStartInfo CreateStartInfo(DownloadItem item)
    {
        var psi = new ProcessStartInfo(tools.YtDlpPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in YtDlpArguments.Build(item.Url, item.Request, tools.FfmpegDir, tools.JsRuntime))
            psi.ArgumentList.Add(arg);
        return psi;
    }

    static void Apply(DownloadItem item, OutputEvent? output, TransferMeter meter)
    {
        switch (output)
        {
            case DownloadProgress progress:
                item.Title = progress.DisplayTitle;
                var reading = meter.Update(DateTime.UtcNow, progress.DownloadedBytes, progress.TotalBytes);
                item.ReportDownloading(progress.Percent, reading is null ? null : OutputParser.Describe(progress, item.Request.Format, reading));
                break;
            case ProcessingStep step:
                item.ReportProcessing(OutputParser.DescribeStep(step.Name, item.Request.Format));
                break;
            case FileSaved saved:
                item.ReportSavedFile(saved.Path);
                break;
        }
    }

    static async Task PumpAsync(StreamReader reader, Action<string> onLine)
    {
        while (await reader.ReadLineAsync() is { } line) onLine(line);
    }

    static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception)
        {
            // Already gone.
        }
    }
}
