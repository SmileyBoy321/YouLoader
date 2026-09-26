using YouLoader.Core.Models;
using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class OutputParserTests
{
    [Fact]
    public void ParsesProgressLine()
    {
        var output = OutputParser.Parse("[dl] 45.3%|   2.10MiB/s|00:12|none|||Me at the zoo");

        var progress = Assert.IsType<DownloadProgress>(output);
        Assert.Equal(45.3, progress.Percent, 3);
        Assert.Equal("2.10MiB/s", progress.Speed);
        Assert.Equal("00:12", progress.Eta);
        Assert.False(progress.IsVideoStream);
        Assert.Null(progress.PlaylistIndex);
        Assert.Equal("Me at the zoo", progress.Title);
        Assert.Equal("Me at the zoo", progress.DisplayTitle);
    }

    [Fact]
    public void KeepsPipesThatArePartOfTheTitle()
    {
        var progress = Assert.IsType<DownloadProgress>(OutputParser.Parse("[dl]10.0%|1MiB/s|00:05|avc1|||Song | Live | 2024"));

        Assert.Equal("Song | Live | 2024", progress.Title);
        Assert.True(progress.IsVideoStream);
    }

    [Fact]
    public void ShowsPlaylistPositionInTitle()
    {
        var progress = Assert.IsType<DownloadProgress>(OutputParser.Parse("[dl]100.0%|3MiB/s|00:00|none|3|12|Track"));

        Assert.Equal(3, progress.PlaylistIndex);
        Assert.Equal(12, progress.PlaylistCount);
        Assert.Equal("(3/12) Track", progress.DisplayTitle);
    }

    [Theory]
    [InlineData("NA")]
    [InlineData("Unknown")]
    [InlineData("Unknown B/s")]
    [InlineData("  ")]
    public void TreatsUnknownSpeedAndEtaAsMissing(string unknown)
    {
        var progress = Assert.IsType<DownloadProgress>(OutputParser.Parse($"[dl]  0.4%|{unknown}|{unknown}|none|||x"));

        Assert.Null(progress.Speed);
        Assert.Null(progress.Eta);
    }

    [Fact]
    public void BrokenProgressLineIsIgnored()
    {
        Assert.Null(OutputParser.Parse("[dl]45%|only|three"));
    }

    [Fact]
    public void UnparseablePercentBecomesZero()
    {
        var progress = Assert.IsType<DownloadProgress>(OutputParser.Parse("[dl]N/A|NA|NA|none|||x"));

        Assert.Equal(0, progress.Percent);
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
        var saved = Assert.IsType<FileSaved>(OutputParser.Parse(@"[file]C:\Users\me\Downloads\YouLoader\Árcangel – ÿ.opus"));

        Assert.Equal(@"C:\Users\me\Downloads\YouLoader\Árcangel – ÿ.opus", saved.Path);
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
    public void DescribeAudioProgress()
    {
        var progress = new DownloadProgress(45.25, "2MiB/s", "00:10", false, null, null, "x");

        Assert.Equal("45.3% · 2MiB/s · 00:10 left", OutputParser.Describe(progress, OutputFormat.Opus));
    }

    [Fact]
    public void DescribeVideoProgressSaysWhichStreamIsDownloading()
    {
        var video = new DownloadProgress(10, null, null, true, null, null, "x");
        var audio = new DownloadProgress(10, null, null, false, null, null, "x");

        Assert.Equal("Video · 10.0%", OutputParser.Describe(video, OutputFormat.Mp4));
        Assert.Equal("Audio · 10.0%", OutputParser.Describe(audio, OutputFormat.Mp4));
    }

    [Theory]
    [InlineData("ExtractAudio", OutputFormat.Mp3, "Converting to MP3…")]
    [InlineData("ExtractAudio", OutputFormat.Opus, "Extracting audio…")]
    [InlineData("Merger", OutputFormat.Mp4, "Merging video and audio…")]
    [InlineData("EmbedThumbnail", OutputFormat.Opus, "Adding cover art…")]
    [InlineData("SomethingNew", OutputFormat.Opus, "Processing…")]
    public void DescribeStep(string step, OutputFormat format, string expected)
    {
        Assert.Equal(expected, OutputParser.DescribeStep(step, format));
    }
}
