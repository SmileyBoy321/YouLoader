using YouLoader.Core.Models;
using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class YtDlpArgumentsTests
{
    const string Video = "https://www.youtube.com/watch?v=jNQXAC9IVRw";
    const string Playlist = "https://www.youtube.com/playlist?list=PL123";

    static DownloadRequest Request(OutputFormat format, string quality = "", bool embedArt = true, bool wholePlaylist = false) =>
        new(format, quality, wholePlaylist, embedArt, @"C:\Music");

    static List<string> Build(DownloadRequest request, string url = Video, string? js = "node:C:\\node.exe") =>
        YtDlpArguments.Build(url, request, @"C:\ffmpeg\bin", js);

    static string ValueAfter(List<string> args, string flag)
    {
        var index = args.IndexOf(flag);
        Assert.True(index >= 0, $"{flag} is missing");
        return args[index + 1];
    }

    [Theory]
    [InlineData("320", "320K")]
    [InlineData("v0", "0")]
    [InlineData("192", "192K")]
    [InlineData("unknown", "320K")]
    public void Mp3_MapsQualityPresetToAudioQuality(string preset, string expected)
    {
        var args = Build(Request(OutputFormat.Mp3, preset));

        Assert.Equal("mp3", ValueAfter(args, "--audio-format"));
        Assert.Equal(expected, ValueAfter(args, "--audio-quality"));
    }

    [Theory]
    [InlineData("best", "res,fps,acodec:aac")]
    [InlineData("1080", "res:1080,vcodec:h264,acodec:aac")]
    [InlineData("720", "res:720,vcodec:h264,acodec:aac")]
    [InlineData("480", "res:480,vcodec:h264,acodec:aac")]
    public void Mp4_SortsFormatsByPresetAndMergesToMp4(string preset, string expectedSort)
    {
        var args = Build(Request(OutputFormat.Mp4, preset));

        Assert.Equal("bv*+ba/b", ValueAfter(args, "--format"));
        Assert.Equal(expectedSort, ValueAfter(args, "--format-sort"));
        Assert.Equal("mp4", ValueAfter(args, "--merge-output-format"));
        Assert.DoesNotContain("--extract-audio", args);
    }

    [Fact]
    public void AlwaysIgnoresUserConfigAndForcesUtf8Output()
    {
        var args = Build(Request(OutputFormat.Mp3));

        Assert.Contains("--ignore-config", args);
        Assert.Equal("utf-8", ValueAfter(args, "--encoding"));
    }

    [Fact]
    public void PassesToolLocationsAndOutputFolder()
    {
        var args = Build(Request(OutputFormat.Mp3));

        Assert.Equal(@"C:\ffmpeg\bin", ValueAfter(args, "--ffmpeg-location"));
        Assert.Equal(@"C:\Music", ValueAfter(args, "--paths"));
        Assert.Equal("node:C:\\node.exe", ValueAfter(args, "--js-runtimes"));
    }

    [Fact]
    public void OmitsJsRuntimeWhenNoneIsKnown()
    {
        var args = Build(Request(OutputFormat.Mp3), js: null);

        Assert.DoesNotContain("--js-runtimes", args);
    }

    [Fact]
    public void UrlComesLastAfterDoubleDash_SoItCantBeReadAsAnOption()
    {
        var args = Build(Request(OutputFormat.Mp3), url: "-https://evil");

        Assert.Equal("--", args[^2]);
        Assert.Equal("-https://evil", args[^1]);
    }

    [Fact]
    public void SingleVideo_DownloadsJustThatVideoIntoTheOutputFolder()
    {
        var args = Build(Request(OutputFormat.Mp3));

        Assert.Contains("--no-playlist", args);
        Assert.Equal(YtDlpArguments.SingleTemplate, ValueAfter(args, "--output"));
    }

    [Fact]
    public void SingleVideo_IsAlwaysDownloadedFresh()
    {
        var args = Build(Request(OutputFormat.Mp3));

        Assert.Contains("--force-overwrites", args);
        Assert.DoesNotContain("--download-archive", args);
    }

    [Fact]
    public void Playlist_DownloadsEverythingIntoItsOwnNumberedFolder()
    {
        var args = Build(Request(OutputFormat.Mp3), url: Playlist);

        Assert.Contains("--yes-playlist", args);
        Assert.Contains("--ignore-errors", args);
        Assert.Equal(YtDlpArguments.CollectionTemplate, ValueAfter(args, "--output"));
        Assert.DoesNotContain("--force-overwrites", args);
    }

    [Theory]
    [InlineData(OutputFormat.Mp3, @"C:\Music\.youloader-mp3.archive")]
    [InlineData(OutputFormat.Mp4, @"C:\Music\.youloader-mp4.archive")]
    public void Playlist_KeepsAPerFormatArchiveSoReRunsOnlyFetchNewItems(OutputFormat format, string archive)
    {
        var args = Build(Request(format, "320"), url: Playlist);

        Assert.Equal(archive, ValueAfter(args, "--download-archive"));
    }

    [Fact]
    public void AudioWithCoverArt_EmbedsTagsAndSquareCover()
    {
        var args = Build(Request(OutputFormat.Mp3, "320"));

        Assert.Contains("--embed-metadata", args);
        Assert.Contains("--embed-thumbnail", args);
        Assert.Equal("jpg", ValueAfter(args, "--convert-thumbnails"));
        Assert.Equal(YtDlpArguments.SquareCoverArt, ValueAfter(args, "--postprocessor-args"));
    }

    [Fact]
    public void VideoWithCoverArt_KeepsTheOriginalThumbnailShape()
    {
        var args = Build(Request(OutputFormat.Mp4, "1080"));

        Assert.Contains("--embed-thumbnail", args);
        Assert.DoesNotContain("--postprocessor-args", args);
    }

    [Fact]
    public void WithoutCoverArt_SkipsAllEmbedding()
    {
        var args = Build(Request(OutputFormat.Mp3, embedArt: false));

        Assert.DoesNotContain("--embed-metadata", args);
        Assert.DoesNotContain("--embed-thumbnail", args);
        Assert.DoesNotContain("--postprocessor-args", args);
    }

    [Fact]
    public void RequestsMachineReadableProgressAndSavedFilePaths()
    {
        var args = Build(Request(OutputFormat.Mp3));

        Assert.Contains(YtDlpArguments.DownloadProgressTemplate, args);
        Assert.Contains(YtDlpArguments.ProcessingTemplate, args);
        Assert.Equal(YtDlpArguments.SavedFileTemplate, ValueAfter(args, "--print"));
        Assert.True(args.IndexOf("--no-quiet") > args.IndexOf("--print"), "--no-quiet must come after --print");
    }
}
