using System.Diagnostics;
using YouLoader.Core.Models;
using YouLoader.Core.Services;

namespace YouLoader.Tests;

/// <summary>Runs only when YOULOADER_INTEGRATION=1, because it downloads real files from YouTube.</summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("YOULOADER_INTEGRATION") != "1")
            Skip = "Set YOULOADER_INTEGRATION=1 to run real downloads.";
    }
}

public sealed class ToolsFixture : IAsyncLifetime
{
    public ToolManager Tools { get; } = new();

    public Task InitializeAsync() => Tools.EnsureAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}

[Trait("Category", "Integration")]
public class IntegrationTests(ToolsFixture fixture) : IClassFixture<ToolsFixture>, IDisposable
{
    // "Me at the zoo": the first YouTube video. 19 seconds long, so tests stay quick.
    const string ShortVideo = "https://www.youtube.com/watch?v=jNQXAC9IVRw";
    const string LongVideo = "https://www.youtube.com/watch?v=aqz-KE-bpKQ";

    readonly string outputDir = Path.Combine(Path.GetTempPath(), $"youloader-it-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
    }

    DownloadItem Item(OutputFormat format, string quality, string url = ShortVideo, bool embedArt = true) =>
        new(url, new DownloadRequest(format, quality, false, embedArt, outputDir));

    async Task<DownloadItem> DownloadAsync(DownloadItem item)
    {
        await new DownloadService(fixture.Tools).RunAsync(item);
        return item;
    }

    string Probe(string file, string entries)
    {
        var psi = new ProcessStartInfo(Path.Combine(fixture.Tools.FfmpegDir, "ffprobe.exe"))
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in new[] { "-v", "error", "-show_entries", entries, "-of", "default=noprint_wrappers=1", file })
            psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    [IntegrationFact]
    public async Task Opus_IsSavedWithSquareCoverArt()
    {
        var item = await DownloadAsync(Item(OutputFormat.Opus, "original"));

        Assert.Equal(DownloadState.Done, item.State);
        Assert.Equal("Me at the zoo", item.Title);
        Assert.EndsWith(".opus", item.FilePath);
        Assert.True(File.Exists(item.FilePath));

        var streams = Probe(item.FilePath!, "stream=codec_name,width,height");
        Assert.Contains("codec_name=opus", streams);
        Assert.Contains("codec_name=mjpeg", streams);
        Assert.Contains("width=360", streams);
        Assert.Contains("height=360", streams);
    }

    [IntegrationFact]
    public async Task Mp3_IsSavedAt320Kbps()
    {
        var item = await DownloadAsync(Item(OutputFormat.Mp3, "320"));

        Assert.Equal(DownloadState.Done, item.State);
        Assert.EndsWith(".mp3", item.FilePath);
        var streams = Probe(item.FilePath!, "stream=codec_name,bit_rate");
        Assert.Contains("codec_name=mp3", streams);
        Assert.Contains("bit_rate=320000", streams);
    }

    [IntegrationFact]
    public async Task Mp4_IsSavedAsH264AndAac()
    {
        var item = await DownloadAsync(Item(OutputFormat.Mp4, "1080"));

        Assert.Equal(DownloadState.Done, item.State);
        Assert.EndsWith(".mp4", item.FilePath);
        var streams = Probe(item.FilePath!, "stream=codec_name");
        Assert.Contains("codec_name=h264", streams);
        Assert.Contains("codec_name=aac", streams);
    }

    [IntegrationFact]
    public async Task DownloadingTheSameVideoTwice_WithCoverArt_StillWorks()
    {
        // Regression: re-tagging an existing Opus file that already has cover art used to fail with "Conversion failed!".
        var first = await DownloadAsync(Item(OutputFormat.Opus, "original"));
        var second = await DownloadAsync(Item(OutputFormat.Opus, "original"));

        Assert.Equal(DownloadState.Done, second.State);
        Assert.Equal(first.FilePath, second.FilePath);
    }

    [IntegrationFact]
    public async Task UnavailableVideo_FailsWithFriendlyMessage()
    {
        var item = await DownloadAsync(Item(OutputFormat.Opus, "original", url: "https://www.youtube.com/watch?v=xxxxxxxxxxx"));

        Assert.Equal(DownloadState.Failed, item.State);
        Assert.DoesNotContain("ERROR:", item.Status);
        Assert.False(string.IsNullOrWhiteSpace(item.Status));
    }

    [IntegrationFact]
    public async Task Cancel_StopsTheDownloadAndKillsYtDlp()
    {
        var item = Item(OutputFormat.Mp4, "best", url: LongVideo);
        var run = new DownloadService(fixture.Tools).RunAsync(item);

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (item.State != DownloadState.Downloading || item.Progress <= 0)
        {
            Assert.True(DateTime.UtcNow < deadline, "download never started");
            await Task.Delay(200);
        }
        item.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(DownloadState.Canceled, item.State);
        Assert.Empty(Directory.EnumerateFiles(outputDir, "*.mp4"));
    }

    [IntegrationFact]
    public async Task ToolManager_ReportsAVersionAndAJsRuntime()
    {
        var version = await fixture.Tools.GetYtDlpVersionAsync();

        Assert.Matches(@"^\d{4}\.\d{2}\.\d{2}", version);
        Assert.NotNull(fixture.Tools.JsRuntime);
        Assert.True(File.Exists(Path.Combine(fixture.Tools.FfmpegDir, "ffmpeg.exe")));
    }
}
