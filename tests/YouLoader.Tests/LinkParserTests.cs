using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class LinkParserTests
{
    [Fact]
    public void ExtractUrls_FindsLinksSeparatedByNewlinesSpacesAndCommas()
    {
        var text = "https://youtu.be/a\r\nhttps://www.youtube.com/watch?v=b  https://example.com/x/y,https://youtu.be/c";

        var urls = LinkParser.ExtractUrls(text);

        Assert.Equal(
            ["https://youtu.be/a", "https://www.youtube.com/watch?v=b", "https://example.com/x/y", "https://youtu.be/c"],
            urls);
    }

    [Fact]
    public void ExtractUrls_IgnoresTextThatIsNotAHttpLink()
    {
        var urls = LinkParser.ExtractUrls("check this out: ftp://nope.com/file notalink youtube.com/watch?v=x");

        Assert.Empty(urls);
    }

    [Fact]
    public void ExtractUrls_RemovesDuplicatesAndWrappingCharacters()
    {
        var urls = LinkParser.ExtractUrls("<https://youtu.be/a> \"https://youtu.be/a\" 'https://youtu.be/b'");

        Assert.Equal(["https://youtu.be/a", "https://youtu.be/b"], urls);
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PL123", false, true)]
    [InlineData("https://www.youtube.com/playlist", false, false)]
    [InlineData("https://www.youtube.com/@SomeChannel", false, true)]
    [InlineData("https://www.youtube.com/@SomeChannel/videos", false, true)]
    [InlineData("https://www.youtube.com/channel/UC123", false, true)]
    [InlineData("https://www.youtube.com/watch?v=abc", true, false)]
    [InlineData("https://www.youtube.com/watch?v=abc&list=PL123", false, false)]
    [InlineData("https://www.youtube.com/watch?v=abc&list=PL123", true, true)]
    [InlineData("https://youtu.be/abc?list=PL123", true, true)]
    [InlineData("https://www.youtube.com/watch?v=CmpSVfoJo-4&list=RDCmpSVfoJo-4&start_radio=1", true, false)]
    [InlineData("https://www.youtube.com/watch?v=CmpSVfoJo-4&list=RDCmpSVfoJo-4&start_radio=1", false, false)]
    [InlineData("https://music.youtube.com/playlist?list=OLAK5", false, true)]
    [InlineData("https://m.youtube.com/watch?v=abc", true, false)]
    [InlineData("https://www.youtube.com/shorts/abc", true, false)]
    [InlineData("https://soundcloud.com/artist/sets/album", true, false)]
    [InlineData("https://example.com/video", true, false)]
    [InlineData("not a url", true, false)]
    public void IsCollection_RecognisesPlaylistsChannelsAndSets(string url, bool wholePlaylist, bool expected)
    {
        Assert.Equal(expected, LinkParser.IsCollection(url, wholePlaylist));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc123")]
    [InlineData("https://youtu.be/abc123")]
    [InlineData("https://youtu.be/abc123?t=42")]
    [InlineData("https://m.youtube.com/watch?v=abc123&t=30s")]
    [InlineData("https://music.youtube.com/watch?v=abc123&feature=share")]
    [InlineData("https://www.youtube.com/shorts/abc123")]
    [InlineData("https://www.youtube.com/watch?v=abc123&list=PL1")]
    public void SameVideoWrittenDifferentlyGetsTheSameKey(string url)
    {
        Assert.Equal("youtube:abc123", LinkParser.DownloadKey(url, wholePlaylist: false));
    }

    [Fact]
    public void VideoInsideAPlaylistIsKeyedByThePlaylistWhenDownloadingItWhole()
    {
        Assert.Equal("youtube:list:PL1", LinkParser.DownloadKey("https://www.youtube.com/watch?v=abc123&list=PL1", wholePlaylist: true));
        Assert.Equal("youtube:list:PL1", LinkParser.DownloadKey("https://www.youtube.com/playlist?list=PL1", wholePlaylist: false));
    }

    [Fact]
    public void DifferentVideosGetDifferentKeys()
    {
        Assert.NotEqual(LinkParser.DownloadKey("https://youtu.be/aaa", false), LinkParser.DownloadKey("https://youtu.be/bbb", false));
    }

    [Fact]
    public void OtherSitesIgnoreWwwAndTrailingSlashButKeepCase()
    {
        Assert.Equal(
            LinkParser.DownloadKey("https://www.example.com/artist/Song/", false),
            LinkParser.DownloadKey("https://example.com/artist/Song", false));
        Assert.NotEqual(
            LinkParser.DownloadKey("https://example.com/Video", false),
            LinkParser.DownloadKey("https://example.com/video", false));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc", true)]
    [InlineData("https://youtube.com/watch?v=abc", true)]
    [InlineData("https://m.youtube.com/watch?v=abc", true)]
    [InlineData("https://music.youtube.com/watch?v=abc", true)]
    [InlineData("https://youtu.be/abc", true)]
    [InlineData("https://www.youtube.com/@channel", true)]
    [InlineData("https://soundcloud.com/artist/song", false)]
    [InlineData("https://vimeo.com/123", false)]
    [InlineData("https://notyoutube.com/watch?v=abc", false)]
    [InlineData("https://youtube.com.evil.example/watch?v=abc", false)]
    [InlineData("not a link", false)]
    public void OnlyYouTubeLinksAreAccepted(string url, bool expected)
    {
        Assert.Equal(expected, LinkParser.IsYouTubeUrl(url));
    }
}
