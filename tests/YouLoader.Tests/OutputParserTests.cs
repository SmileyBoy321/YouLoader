using YouLoader.Core.Models;
using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class OutputParserTests
{
    [Fact]
    public void ParsesProgressLine()
    {
        var output = OutputParser.Parse("[dl]10198052|71283827|none|||Me at the zoo");

        var progress = Assert.IsType<DownloadProgress>(output);
        Assert.Equal(10198052, progress.DownloadedBytes);
        Assert.Equal(71283827, progress.TotalBytes);
        Assert.Equal(14.3, progress.Percent, 1);
        Assert.False(progress.IsVideoStream);
        Assert.Null(progress.PlaylistIndex);
        Assert.Equal("Me at the zoo", progress.DisplayTitle);
    }

    [Fact]
    public void KeepsPipesThatArePartOfTheTitle()
    {
        var progress = Assert.IsType<DownloadProgress>(OutputParser.Parse("[dl]1|10|avc1|||Song | Live | 2024"));

        Assert.Equal("Song | Live | 2024", progress.Title);
        Assert.True(progress.IsVideoStream);
    }

    [Fact]
    public void ShowsPlaylistPositionInTitle()
    {
        var progress = Assert.IsType<DownloadProgress>(OutputParser.Parse("[dl]100|100|none|3|12|Track"));

        Assert.Equal(3, progress.PlaylistIndex);
        Assert.Equal(12, progress.PlaylistCount);
        Assert.Equal("(3/12) Track", progress.DisplayTitle);
    }

    [Theory]
    [InlineData("NA")]
    [InlineData("")]
    [InlineData("  ")]
    public void UnknownTotalIsMissingNotZero(string unknown)
    {
        var progress = Assert.IsType<DownloadProgress>(OutputParser.Parse($"[dl]1024|{unknown}|none|||x"));

        Assert.Null(progress.TotalBytes);
        Assert.Equal(0, progress.Percent);
    }

    [Fact]
    public void TotalEstimatesWithDecimalsAreAccepted()
    {
        var progress = Assert.IsType<DownloadProgress>(OutputParser.Parse("[dl]500|1000.7|none|||x"));

        Assert.Equal(1000, progress.TotalBytes);
    }

    [Fact]
    public void PercentNeverGoesAbove100WhenTheEstimateWasTooLow()
    {
        var progress = Assert.IsType<DownloadProgress>(OutputParser.Parse("[dl]1200|1000|none|||x"));

        Assert.Equal(100, progress.Percent);
    }

    [Fact]
    public void BrokenProgressLineIsIgnored()
    {
        Assert.Null(OutputParser.Parse("[dl]45|only|three"));
    }

    [Fact]
    public void ParsesProcessingStep()
    {
        var step = Assert.IsType<ProcessingStep>(OutputParser.Parse("[pp]ExtractAudio"));

        Assert.Equal("ExtractAudio", step.Name);
    }

    [Fact]
    public void ParsesSavedFilePath()
    {
        var saved = Assert.IsType<FileSaved>(OutputParser.Parse(@"[file]C:\Users\me\Downloads\YouLoader\Árcangel – ÿ.mp3"));

        Assert.Equal(@"C:\Users\me\Downloads\YouLoader\Árcangel – ÿ.mp3", saved.Path);
    }

    [Theory]
    [InlineData("[youtube] Extracting URL: https://youtu.be/x")]
    [InlineData("[download] Destination: x.webm")]
    [InlineData("")]
    [InlineData("WARNING: something")]
    public void OtherLinesAreIgnored(string line)
    {
        Assert.Null(OutputParser.Parse(line));
    }

    [Fact]
    public void DescribeShowsWholePercentSizeSpeedAndTimeLeft()
    {
        var progress = new DownloadProgress(31_562_137, 71_303_987, false, null, null, "x");
        var reading = new TransferReading(4.4 * 1024 * 1024, TimeSpan.FromSeconds(12));

        Assert.Equal("44% · 30.1 of 68.0 MB · 4.4 MB/s · 0:12 left", OutputParser.Describe(progress, OutputFormat.Mp3, reading));
    }

    [Fact]
    public void DescribeVideoProgressSaysWhichStreamIsDownloading()
    {
        var reading = new TransferReading(null, null);
        var video = new DownloadProgress(10, 100, true, null, null, "x");
        var audio = new DownloadProgress(10, 100, false, null, null, "x");

        Assert.StartsWith("Video · 10%", OutputParser.Describe(video, OutputFormat.Mp4, reading));
        Assert.StartsWith("Audio · 10%", OutputParser.Describe(audio, OutputFormat.Mp4, reading));
    }

    [Fact]
    public void DescribeWithoutKnownSizeShowsBytesSoFar()
    {
        var progress = new DownloadProgress(2 * 1024 * 1024, null, false, null, null, "x");

        Assert.Equal("2.0 MB", OutputParser.Describe(progress, OutputFormat.Mp3, new TransferReading(null, null)));
    }

    [Theory]
    [InlineData("ExtractAudio", OutputFormat.Mp3, "Converting to MP3…")]
    [InlineData("Merger", OutputFormat.Mp4, "Merging video and audio…")]
    [InlineData("EmbedThumbnail", OutputFormat.Mp3, "Adding cover art…")]
    [InlineData("SomethingNew", OutputFormat.Mp3, "Processing…")]
    public void DescribeStep(string step, OutputFormat format, string expected)
    {
        Assert.Equal(expected, OutputParser.DescribeStep(step, format));
    }
}
