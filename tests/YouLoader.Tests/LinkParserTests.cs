using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class LinkParserTests
{
    [Fact]
    public void ExtractUrls_FindsLinksSeparatedByNewlinesSpacesAndCommas()
    {
        var text = "https://youtu.be/a\r\nhttps://www.youtube.com/watch?v=b  https://soundcloud.com/x/y,https://youtu.be/c";

        var urls = LinkParser.ExtractUrls(text);

        Assert.Equal(
            ["https://youtu.be/a", "https://www.youtube.com/watch?v=b", "https://soundcloud.com/x/y", "https://youtu.be/c"],
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
    [InlineData("https://music.youtube.com/playlist?list=OLAK5", false, true)]
    [InlineData("https://m.youtube.com/watch?v=abc", true, false)]
    [InlineData("https://www.youtube.com/shorts/abc", true, false)]
    [InlineData("https://soundcloud.com/artist", false, true)]
    [InlineData("https://soundcloud.com/artist/sets/album", false, true)]
    [InlineData("https://soundcloud.com/artist/tracks", false, true)]
    [InlineData("https://soundcloud.com/artist/some-song", true, false)]
    [InlineData("https://example.com/video", true, false)]
    [InlineData("not a url", true, false)]
    public void IsCollection_RecognisesPlaylistsChannelsAndSets(string url, bool wholePlaylist, bool expected)
    {
        Assert.Equal(expected, LinkParser.IsCollection(url, wholePlaylist));
    }
}
