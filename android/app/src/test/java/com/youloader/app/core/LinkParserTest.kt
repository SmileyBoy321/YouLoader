package com.youloader.app.core

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/** Same cases as the Windows app's LinkParserTests and QueueAndLinkBoxTests. */
class LinkParserTest {
    @Test fun extractsLinksSeparatedByNewlinesSpacesAndCommas() {
        val urls = LinkParser.extractUrls("https://youtu.be/a\nhttps://youtu.be/b https://youtu.be/c,https://youtu.be/d\r\n")
        assertEquals(listOf("https://youtu.be/a", "https://youtu.be/b", "https://youtu.be/c", "https://youtu.be/d"), urls)
    }

    @Test fun ignoresTextThatIsNotAHttpLink() {
        assertTrue(LinkParser.extractUrls("hello world ftp://x.com youtube.com/watch?v=abc").isEmpty())
    }

    @Test fun removesDuplicatesAndWrappingCharacters() {
        assertEquals(
            listOf("https://youtu.be/a", "https://youtu.be/b"),
            LinkParser.extractUrls("<https://youtu.be/a> \"https://youtu.be/b\" 'https://youtu.be/a'"),
        )
    }

    @Test fun findsTheLinkInWhatTheYouTubeAppShares() {
        assertEquals(
            listOf("https://youtu.be/dQw4w9WgXcQ?si=abc"),
            LinkParser.extractUrls("Rick Astley - Never Gonna Give You Up https://youtu.be/dQw4w9WgXcQ?si=abc"),
        )
    }

    @Test fun recognisesPlaylistsChannelsAndMixes() {
        val cases = listOf(
            Triple("https://www.youtube.com/playlist?list=PL123", false, true),
            Triple("https://www.youtube.com/playlist", false, false),
            Triple("https://www.youtube.com/@SomeChannel", false, true),
            Triple("https://www.youtube.com/@SomeChannel/videos", false, true),
            Triple("https://www.youtube.com/channel/UC123", false, true),
            Triple("https://www.youtube.com/watch?v=abc", true, false),
            Triple("https://www.youtube.com/watch?v=abc&list=PL123", false, false),
            Triple("https://www.youtube.com/watch?v=abc&list=PL123", true, true),
            Triple("https://youtu.be/abc?list=PL123", true, true),
            Triple("https://www.youtube.com/watch?v=CmpSVfoJo-4&list=RDCmpSVfoJo-4&start_radio=1", true, false),
            Triple("https://www.youtube.com/watch?v=CmpSVfoJo-4&list=RDCmpSVfoJo-4&start_radio=1", false, false),
            Triple("https://music.youtube.com/playlist?list=OLAK5", false, true),
            Triple("https://m.youtube.com/watch?v=abc", true, false),
            Triple("https://www.youtube.com/shorts/abc", true, false),
            Triple("https://soundcloud.com/artist/sets/album", true, false),
            Triple("https://example.com/video", true, false),
            Triple("not a url", true, false),
        )
        for ((url, whole, expected) in cases) assertEquals(url, expected, LinkParser.isCollection(url, whole))
    }

    @Test fun sameVideoWrittenDifferentlyGetsTheSameKey() {
        listOf(
            "https://www.youtube.com/watch?v=abc123",
            "https://youtu.be/abc123",
            "https://youtu.be/abc123?t=42",
            "https://m.youtube.com/watch?v=abc123&t=30s",
            "https://music.youtube.com/watch?v=abc123&feature=share",
            "https://www.youtube.com/shorts/abc123",
            "https://www.youtube.com/watch?v=abc123&list=PL1",
        ).forEach { assertEquals(it, "youtube:abc123", LinkParser.downloadKey(it, wholePlaylist = false)) }
    }

    @Test fun videoInsideAPlaylistIsKeyedByThePlaylistWhenDownloadingItWhole() {
        assertEquals("youtube:list:PL1", LinkParser.downloadKey("https://www.youtube.com/watch?v=abc123&list=PL1", wholePlaylist = true))
        assertEquals("youtube:list:PL1", LinkParser.downloadKey("https://www.youtube.com/playlist?list=PL1", wholePlaylist = false))
    }

    @Test fun differentVideosGetDifferentKeys() {
        assertNotEquals(LinkParser.downloadKey("https://youtu.be/aaa", false), LinkParser.downloadKey("https://youtu.be/bbb", false))
    }

    @Test fun onlyYouTubeLinksAreAccepted() {
        mapOf(
            "https://www.youtube.com/watch?v=abc" to true,
            "https://youtube.com/watch?v=abc" to true,
            "https://m.youtube.com/watch?v=abc" to true,
            "https://music.youtube.com/watch?v=abc" to true,
            "https://youtu.be/abc" to true,
            "https://www.youtube.com/@channel" to true,
            "https://soundcloud.com/artist/song" to false,
            "https://vimeo.com/123" to false,
            "https://notyoutube.com/watch?v=abc" to false,
            "https://youtube.com.evil.example/watch?v=abc" to false,
            "not a link" to false,
        ).forEach { (url, expected) -> assertEquals(url, expected, LinkParser.isYouTubeUrl(url)) }
    }

    @Test fun mergeAddsNewLinksOnePerLineAndSkipsOnesAlreadyThere() {
        val merge = LinkParser.mergeLinks("https://youtu.be/a", "https://www.youtube.com/watch?v=a https://youtu.be/b https://youtu.be/b")
        assertEquals("https://youtu.be/a\nhttps://youtu.be/b", merge.text)
        assertEquals(1, merge.added)
        assertEquals(2, merge.duplicates)
    }

    @Test fun mergeIntoAnEmptyBoxJustListsTheLinks() {
        assertEquals("https://youtu.be/a\nhttps://youtu.be/b", LinkParser.mergeLinks("", "https://youtu.be/a, https://youtu.be/b").text)
    }

    @Test fun countsLinksAndDuplicates() {
        assertEquals(3 to 1, LinkParser.countLinks("https://youtu.be/a\nhttps://youtu.be/b\nhttps://www.youtube.com/watch?v=a"))
    }

    @Test fun oddCharactersDoNotCrashIt() {
        assertTrue(LinkParser.extractUrls("https://example.com/a b|c https://[bad").isNotEmpty())
        assertEquals(false, LinkParser.isYouTubeUrl("https://[bad"))
    }
}
