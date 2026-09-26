using System.ComponentModel;
using YouLoader.Core.Models;
using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class ModelTests
{
    static DownloadItem NewItem() =>
        new("https://youtu.be/x", new DownloadRequest(OutputFormat.Opus, "original", false, true, @"C:\Music"));

    [Fact]
    public void NewItemIsQueuedAndShowsItsUrlAsTitle()
    {
        var item = NewItem();

        Assert.Equal(DownloadState.Queued, item.State);
        Assert.True(item.IsActive);
        Assert.Equal("https://youtu.be/x", item.Title);
        Assert.Equal("OPUS", item.FormatLabel);
    }

    [Fact]
    public void ProgressIsClampedTo0To100()
    {
        var item = NewItem();

        item.ReportDownloading(140, "x");
        Assert.Equal(100, item.Progress);

        item.ReportDownloading(-5, "x");
        Assert.Equal(0, item.Progress);
    }

    [Fact]
    public void CompleteWithOneFileShowsItsName()
    {
        var item = NewItem();
        item.ReportSavedFile(@"C:\Music\Song.opus");

        item.Complete();

        Assert.Equal(DownloadState.Done, item.State);
        Assert.True(item.IsDone);
        Assert.False(item.IsActive);
        Assert.Equal("Saved · Song.opus", item.Status);
    }

    [Fact]
    public void CompleteWithManyFilesShowsCountAndWarning()
    {
        var item = NewItem();
        item.ReportSavedFile("a.opus");
        item.ReportSavedFile("b.opus");

        item.Complete("some items failed");

        Assert.Equal("Saved 2 files · some items failed", item.Status);
        Assert.Equal("b.opus", item.FilePath);
    }

    [Fact]
    public void RetryingResetsProgressAndSaysWhichAttempt()
    {
        var item = NewItem();
        item.ReportDownloading(40, "x");

        item.ReportRetrying(2, 3);

        Assert.Equal(DownloadState.Downloading, item.State);
        Assert.Equal(0, item.Progress);
        Assert.Equal("Connection hiccup. Retrying (2/3)…", item.Status);
    }

    [Fact]
    public void SameVideoAndFormatAreDuplicatesButOtherFormatsAreNot()
    {
        var request = new DownloadRequest(OutputFormat.Opus, "original", false, true, @"C:\Music");
        var a = new DownloadItem("https://youtu.be/abc123", request);
        var b = new DownloadItem("https://www.youtube.com/watch?v=abc123&t=5", request);
        var asMp3 = new DownloadItem("https://youtu.be/abc123", request with { Format = OutputFormat.Mp3 });

        Assert.Equal(a.Key, b.Key);
        Assert.NotEqual(a.Key, asMp3.Key);
    }

    [Fact]
    public void DownloadingWithoutNewTextKeepsTheLastStatus()
    {
        var item = NewItem();
        item.ReportDownloading(10, "10% · 1.0 MB/s");

        item.ReportDownloading(11, null);

        Assert.Equal(11, item.Progress);
        Assert.Equal("10% · 1.0 MB/s", item.Status);
    }

    [Fact]
    public void CompleteWithNoNewFilesSaysUpToDate()
    {
        var item = NewItem();

        item.Complete();

        Assert.Equal(DownloadState.Done, item.State);
        Assert.Equal("Up to date · nothing new to download", item.Status);
    }

    [Fact]
    public void StateChangeNotifiesDerivedProperties()
    {
        var item = NewItem();
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.Fail("boom");

        Assert.Contains(nameof(DownloadItem.State), changed);
        Assert.Contains(nameof(DownloadItem.IsActive), changed);
        Assert.Contains(nameof(DownloadItem.CanRetry), changed);
        Assert.Contains(nameof(DownloadItem.Status), changed);
    }

    [Fact]
    public void SettingSameValueDoesNotNotify()
    {
        var item = NewItem();
        var count = 0;
        item.PropertyChanged += (_, _) => count++;

        item.Title = item.Title;

        Assert.Equal(0, count);
    }

    [Fact]
    public void ResetGivesAFreshCancellationTokenAndClearsResults()
    {
        var item = NewItem();
        var oldToken = item.Cancellation.Token;
        item.ReportSavedFile("a.opus");
        item.Cancel();
        item.MarkCanceled();
        Assert.True(item.CanRetry);

        item.Reset();

        Assert.True(oldToken.IsCancellationRequested);
        Assert.False(item.Cancellation.Token.IsCancellationRequested);
        Assert.Equal(DownloadState.Queued, item.State);
        Assert.Equal(0, item.SavedFiles);
        Assert.Null(item.FilePath);
    }

    [Fact]
    public void EveryFormatHasAtLeastOnePresetWithUniqueKeys()
    {
        foreach (var format in Enum.GetValues<OutputFormat>())
        {
            var presets = QualityPresets.For(format);
            Assert.NotEmpty(presets);
            Assert.Equal(presets.Count, presets.Select(p => p.Key).Distinct().Count());
        }
    }

    [Fact]
    public void UnknownPresetFallsBackToFormatDefault()
    {
        Assert.Equal("320", QualityPresets.Find(OutputFormat.Mp3, "nope").Key);
        Assert.Equal("720", QualityPresets.Find(OutputFormat.Mp4, "720").Key);
        Assert.Equal("original", QualityPresets.Find(OutputFormat.Opus, null).Key);
    }
}

public class ErrorMessagesTests
{
    [Theory]
    [InlineData("ERROR: [youtube] abcdefghijk: Private video. Sign in if you've been granted access", "This video is private.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Video unavailable", "This video is unavailable. It may be removed or blocked in your country.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Sign in to confirm you’re not a bot", "YouTube asked for a bot check. Wait a few minutes, click “Update yt-dlp” and retry.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Sign in to confirm your age", "This video is age-restricted, so YouTube requires signing in.")]
    [InlineData("ERROR: unable to download video data: HTTP Error 403: Forbidden", "YouTube refused the download. Click “Update yt-dlp” and retry.")]
    [InlineData("ERROR: Unsupported URL: https://example.com", "That link isn't supported. Paste a YouTube or SoundCloud link.")]
    [InlineData("ERROR: [Errno 28] No space left on device", "Your disk is full.")]
    [InlineData("ERROR: Postprocessing: Conversion failed!", "The download worked, but converting it failed. Click Retry, or try another format.")]
    public void KnownErrorsGetFriendlyMessages(string raw, string expected)
    {
        Assert.Equal(expected, ErrorMessages.Friendly(raw, 1));
    }

    [Fact]
    public void UnknownErrorsLoseTheirTechnicalPrefix()
    {
        Assert.Equal("Something odd happened", ErrorMessages.Friendly("ERROR: [youtube] abcdefghijk: Something odd happened", 1));
    }

    [Fact]
    public void LongErrorsAreShortened()
    {
        var message = ErrorMessages.Friendly("ERROR: " + new string('x', 500), 1);

        Assert.Equal(200, message.Length);
        Assert.EndsWith("…", message);
    }

    [Theory]
    [InlineData("ERROR: unable to download video data: HTTP Error 403: Forbidden", true)]
    [InlineData("ERROR: unable to download video data: HTTP Error 503: Service Unavailable", true)]
    [InlineData("ERROR: [Errno 10054] Connection reset by peer", true)]
    [InlineData("ERROR: The read operation timed out", true)]
    [InlineData("ERROR: [youtube] abcdefghijk: Video unavailable", false)]
    [InlineData("ERROR: [youtube] abcdefghijk: Private video", false)]
    [InlineData("ERROR: HTTP Error 404: Not Found", false)]
    [InlineData(null, false)]
    public void TransientErrorsAreRetriedOthersAreNot(string? error, bool expected)
    {
        Assert.Equal(expected, ErrorMessages.IsTransient(error));
    }

    [Fact]
    public void MissingErrorMentionsExitCode()
    {
        Assert.Equal("Download failed (yt-dlp exit code 2).", ErrorMessages.Friendly(null, 2));
    }
}

public class AppSettingsTests : IDisposable
{
    readonly string path = Path.Combine(Path.GetTempPath(), $"youloader-test-{Guid.NewGuid():N}", "settings.json");

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(path)!;
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void MissingFileGivesDefaults()
    {
        var settings = AppSettings.Load(path);

        Assert.Equal(OutputFormat.Opus, settings.Format);
        Assert.True(settings.EmbedArt);
        Assert.Equal(AppSettings.DefaultOutputDir, settings.OutputDir);
    }

    [Fact]
    public void SaveAndLoadRoundTrips()
    {
        var settings = new AppSettings { OutputDir = @"D:\Music", Format = OutputFormat.Mp4, WholePlaylist = true, EmbedArt = false };
        settings.Quality["Mp4"] = "720";

        settings.Save(path);
        var loaded = AppSettings.Load(path);

        Assert.Equal(@"D:\Music", loaded.OutputDir);
        Assert.Equal(OutputFormat.Mp4, loaded.Format);
        Assert.True(loaded.WholePlaylist);
        Assert.False(loaded.EmbedArt);
        Assert.Equal("720", loaded.QualityFor(OutputFormat.Mp4));
        Assert.Contains("\"Mp4\"", File.ReadAllText(path));
    }

    [Fact]
    public void CorruptFileGivesDefaultsInsteadOfCrashing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this is not json");

        var settings = AppSettings.Load(path);

        Assert.Equal(OutputFormat.Opus, settings.Format);
    }

    [Fact]
    public void QualityForUnknownValueFallsBackToDefault()
    {
        var settings = new AppSettings();
        settings.Quality["Mp3"] = "999";

        Assert.Equal("320", settings.QualityFor(OutputFormat.Mp3));
    }
}

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v2.1.0", 2, 1, 0)]
    [InlineData("V3.0", 3, 0, 0)]
    [InlineData(" 2.0.5 ", 2, 0, 5)]
    public void ParsesReleaseTags(string tag, int major, int minor, int build)
    {
        Assert.True(UpdateChecker.TryParseTag(tag, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v2")]
    public void RejectsTagsThatAreNotVersions(string? tag)
    {
        Assert.False(UpdateChecker.TryParseTag(tag, out _));
    }
}
