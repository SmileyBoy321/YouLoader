package com.youloader.app.core

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** Same cases as the Windows app's ShazamTests. */
class ShazamTest {
    @Test fun findsTheSongNumber() {
        val cases = mapOf(
            "https://www.shazam.com/track/843646036/without-me?referrer=share" to "843646036",
            "https://shazam.com/track/843646036" to "843646036",
            "https://www.shazam.com/en-us/track/843646036/without-me" to "843646036",
            "https://shz.am/t843646036" to "843646036",
            "https://www.shazam.com/artist/red-village/1850036710" to null,
            "https://www.shazam.com/track/" to null,
            "https://www.shazam.com/track/abc/x" to null,
            "https://example.com/track/843646036" to null,
            "https://youtu.be/abc" to null,
        )
        for ((url, expected) in cases) assertEquals(url, expected, LinkParser.shazamTrackId(url))
    }

    @Test fun shazamLinksAreSupportedAlongsideYouTube() {
        assertTrue(LinkParser.isSupportedUrl("https://www.shazam.com/track/843646036/without-me?referrer=share"))
        assertTrue(LinkParser.isSupportedUrl("https://youtu.be/abc"))
        assertFalse(LinkParser.isSupportedUrl("https://vimeo.com/123"))
        assertFalse(LinkParser.isCollection("https://www.shazam.com/track/843646036/without-me", wholePlaylist = true))
    }

    @Test fun theSameSongIsTheSameDownloadHoweverItsLinkIsWritten() {
        val keys = listOf(
            "https://www.shazam.com/track/843646036/without-me?referrer=share",
            "https://shazam.com/en-us/track/843646036",
            "https://shz.am/t843646036",
        ).map { LinkParser.downloadKey(it, wholePlaylist = false) }.distinct()
        assertEquals(listOf("shazam:843646036"), keys)
    }

    @Test fun findsTheLinkInWhatTheShazamAppShares() {
        assertEquals(
            listOf("https://www.shazam.com/track/843646036/without-me"),
            LinkParser.extractUrls("I used Shazam to discover Without Me by Red Village & mikeeysmind. https://www.shazam.com/track/843646036/without-me"),
        )
    }

    @Test fun readsTitleAndArtist() {
        val song = Shazam.parseTrack("""{"layout":"5","type":"MUSIC","key":"843646036","title":"Without Me","subtitle":"Red Village & mikeeysmind"}""")
        assertEquals(ShazamSong("Without Me", "Red Village & mikeeysmind"), song)
        assertEquals("Red Village & mikeeysmind – Without Me", song!!.displayName)
        assertEquals("ytsearch1:Red Village & mikeeysmind - Without Me", song.searchQuery)
    }

    @Test fun noTitleMeansNoSong() {
        for (json in listOf("", "not json", "[]", """{"subtitle":"Someone"}""", """{"title":"  "}""")) {
            assertNull(json, Shazam.parseTrack(json))
        }
    }

    @Test fun aSongWithoutAnArtistIsSearchedByTitle() {
        val song = ShazamSong("Without Me", "")
        assertEquals("Without Me", song.displayName)
        assertEquals("ytsearch1:Without Me", song.searchQuery)
    }

    @Test fun aLookedUpSongIsDownloadedBySearchAndKeepsItsName() {
        val item = DownloadItem(1, "https://www.shazam.com/track/843646036/without-me", DownloadRequest(OutputFormat.MP3, "320", false, true))
        assertEquals("https://www.shazam.com/track/843646036/without-me", item.ytDlpTarget)

        item.reportSong(ShazamSong("Without Me", "Red Village & mikeeysmind"))

        assertEquals("ytsearch1:Red Village & mikeeysmind - Without Me", item.ytDlpTarget)
        assertEquals("Red Village & mikeeysmind – Without Me", item.title)
        assertTrue("--no-playlist" in YtDlpArguments.build(item.ytDlpTarget, item.request, "/work", "/archive"))
    }
}
