using YouLoader.Core.Models;
using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class ShazamTests
{
    [Theory]
    [InlineData("https://www.shazam.com/track/843646036/without-me?referrer=share", "843646036")]
    [InlineData("https://shazam.com/track/843646036", "843646036")]
    [InlineData("https://www.shazam.com/en-us/track/843646036/without-me", "843646036")]
    [InlineData("https://shz.am/t843646036", "843646036")]
    [InlineData("https://www.shazam.com/artist/red-village/1850036710", null)]
    [InlineData("https://www.shazam.com/track/", null)]
    [InlineData("https://www.shazam.com/track/abc/x", null)]
    [InlineData("https://example.com/track/843646036", null)]
    [InlineData("https://youtu.be/abc", null)]
    public void ShazamTrackId_FindsTheSongNumber(string url, string? expected)
    {
        Assert.Equal(expected, LinkParser.ShazamTrackId(url));
    }

    [Fact]
    public void ShazamLinksAreSupportedAlongsideYouTube()
    {
        Assert.True(LinkParser.IsSupportedUrl("https://www.shazam.com/track/843646036/without-me?referrer=share"));
        Assert.True(LinkParser.IsSupportedUrl("https://youtu.be/abc"));
        Assert.False(LinkParser.IsSupportedUrl("https://vimeo.com/123"));
        Assert.False(LinkParser.IsCollection("https://www.shazam.com/track/843646036/without-me", wholePlaylist: true));
    }

    [Fact]
    public void TheSameSongIsTheSameDownloadHoweverItsLinkIsWritten()
    {
        var keys = new[]
        {
            "https://www.shazam.com/track/843646036/without-me?referrer=share",
            "https://shazam.com/en-us/track/843646036",
            "https://shz.am/t843646036",
        }.Select(u => LinkParser.DownloadKey(u, wholePlaylist: false)).Distinct();

        Assert.Equal(["shazam:843646036"], keys);
    }

    [Fact]
    public void FindsTheLinkInWhatTheShazamAppShares()
    {
        var urls = LinkParser.ExtractUrls("I used Shazam to discover Without Me by Red Village & mikeeysmind. https://www.shazam.com/track/843646036/without-me");

        Assert.Equal(["https://www.shazam.com/track/843646036/without-me"], urls);
    }

    [Fact]
    public void ParseTrack_ReadsTitleAndArtist()
    {
        var song = Shazam.ParseTrack("""{"layout":"5","type":"MUSIC","key":"843646036","title":"Without Me","subtitle":"Red Village & mikeeysmind"}""");

        Assert.Equal(new ShazamSong("Without Me", "Red Village & mikeeysmind"), song);
        Assert.Equal("Red Village & mikeeysmind – Without Me", song!.DisplayName);
        Assert.Equal("ytsearch1:Red Village & mikeeysmind - Without Me", song.SearchQuery);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"subtitle":"Someone"}""")]
    [InlineData("""{"title":"  "}""")]
    public void ParseTrack_ReturnsNullWithoutATitle(string json)
    {
        Assert.Null(Shazam.ParseTrack(json));
    }

    [Fact]
    public void ASongWithoutAnArtistIsSearchedByTitle()
    {
        var song = new ShazamSong("Without Me", "");

        Assert.Equal("Without Me", song.DisplayName);
        Assert.Equal("ytsearch1:Without Me", song.SearchQuery);
    }

    [Fact]
    public void ALookedUpSongIsDownloadedBySearchAndKeepsItsName()
    {
        var item = new DownloadItem("https://www.shazam.com/track/843646036/without-me", new DownloadRequest(OutputFormat.Mp3, "320", false, true, @"C:\Music"));
        Assert.Equal("https://www.shazam.com/track/843646036/without-me", item.YtDlpTarget);

        item.ReportSong(new ShazamSong("Without Me", "Red Village & mikeeysmind"));

        Assert.Equal("ytsearch1:Red Village & mikeeysmind - Without Me", item.YtDlpTarget);
        Assert.Equal("Red Village & mikeeysmind – Without Me", item.Title);
        var args = YtDlpArguments.Build(item.YtDlpTarget, item.Request, @"C:\ffmpeg", null);
        Assert.Contains("--no-playlist", args);
        Assert.Equal(["--", "ytsearch1:Red Village & mikeeysmind - Without Me"], args[^2..]);
    }
}
