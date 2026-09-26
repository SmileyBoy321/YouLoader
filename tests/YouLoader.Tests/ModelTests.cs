using System.ComponentModel;
using YouLoader.Core.Models;
using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class ModelTests
{
    static DownloadItem NewItem() =>
        new("https://youtu.be/x", new DownloadRequest(OutputFormat.Mp3, "320", false, true, @"C:\Music"));

    [Fact]
    public void NewItemIsQueuedAndShowsItsUrlAsTitle()
    {
        var item = NewItem();

        Assert.Equal(DownloadState.Queued, item.State);
        Assert.True(item.IsActive);
        Assert.Equal("https://youtu.be/x", item.Title);
        Assert.Equal("MP3", item.FormatLabel);
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
        item.ReportSavedFile(@"C:\Music\Song.mp3");

        item.Complete();

        Assert.Equal(DownloadState.Done, item.State);
        Assert.True(item.IsDone);
        Assert.False(item.IsActive);
        Assert.Equal("Saved · Song.mp3", item.Status);
    }

    [Fact]
    public void CompleteWithManyFilesShowsCountAndWarning()
    {
        var item = NewItem();
        item.ReportSavedFile("a.mp3");
        item.ReportSavedFile("b.mp3");

        item.Complete("some items failed");

        Assert.Equal("Saved 2 files · some items failed", item.Status);
        Assert.Equal("b.mp3", item.FilePath);
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
        var request = new DownloadRequest(OutputFormat.Mp3, "320", false, true, @"C:\Music");
        var a = new DownloadItem("https://youtu.be/abc123", request);
        var b = new DownloadItem("https://www.youtube.com/watch?v=abc123&t=5", request);
        var asMp4 = new DownloadItem("https://youtu.be/abc123", request with { Format = OutputFormat.Mp4 });

        Assert.Equal(a.Key, b.Key);
        Assert.NotEqual(a.Key, asMp4.Key);
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

        item.Fail(new ErrorExplanation("Boom.", "Because.", ["Retry."]));

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
        item.ReportSavedFile("a.mp3");
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
        Assert.Equal("320", QualityPresets.Find(OutputFormat.Mp3, null).Key);
    }
}

public class TransientErrorTests
{
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

        Assert.Equal(OutputFormat.Mp3, settings.Format);
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
    public void OldSettingsWithOpusSwitchToMp3AndKeepTheirFolder()
    {
        // Opus was removed in 2.0; settings saved by an earlier build must still load.
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "OutputDir": "D:\\Music", "Format": "Opus", "EmbedArt": false }""");

        var settings = AppSettings.Load(path);

        Assert.Equal(OutputFormat.Mp3, settings.Format);
        Assert.Equal(@"D:\Music", settings.OutputDir);
        Assert.False(settings.EmbedArt);
    }

    [Fact]
    public void CorruptFileGivesDefaultsInsteadOfCrashing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this is not json");

        var settings = AppSettings.Load(path);

        Assert.Equal(OutputFormat.Mp3, settings.Format);
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
