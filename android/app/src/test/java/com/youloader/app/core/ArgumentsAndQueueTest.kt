package com.youloader.app.core

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/** Same cases as the Windows app's YtDlpArgumentsTests and ModelTests. */
class ArgumentsAndQueueTest {
    private val mp3 = DownloadRequest(OutputFormat.MP3, "320", wholePlaylist = false, embedArt = true)
    private val mp4 = DownloadRequest(OutputFormat.MP4, "1080", wholePlaylist = false, embedArt = true)

    private fun List<String>.valueOf(option: String) = this[indexOf(option) + 1]

    @Test fun mp3Is320WithSquareCoverArt() {
        val args = YtDlpArguments.build("https://youtu.be/a", mp3, "/work", "/archive")
        assertEquals("mp3", args.valueOf("--audio-format"))
        assertEquals("320K", args.valueOf("--audio-quality"))
        assertTrue("--embed-thumbnail" in args)
        assertEquals(YtDlpArguments.SQUARE_COVER_ART, args.valueOf("--postprocessor-args"))
        assertEquals("/work", args.valueOf("--paths"))
    }

    @Test fun mp3QualityPresets() {
        assertEquals("0", YtDlpArguments.formatArguments(OutputFormat.MP3, "v0").valueOf("--audio-quality"))
        assertEquals("192K", YtDlpArguments.formatArguments(OutputFormat.MP3, "192").valueOf("--audio-quality"))
    }

    @Test fun cappedVideoPrefersH264AndMergesToMp4() {
        val args = YtDlpArguments.build("https://youtu.be/a", mp4, "/work", "/archive")
        assertEquals("res:1080,vcodec:h264,acodec:aac", args.valueOf("--format-sort"))
        assertEquals("mp4", args.valueOf("--merge-output-format"))
        // Video keeps its own 16:9 thumbnail.
        assertFalse("--postprocessor-args" in args)
    }

    @Test fun bestVideoSortsByResolution() {
        assertEquals("res,fps,acodec:aac", YtDlpArguments.formatArguments(OutputFormat.MP4, "best").valueOf("--format-sort"))
    }

    @Test fun singleVideosAreFreshAndPlaylistsRememberWhatTheyFetched() {
        val single = YtDlpArguments.build("https://youtu.be/a", mp3, "/work", "/archive")
        assertTrue("--no-playlist" in single && "--force-overwrites" in single)
        assertEquals(YtDlpArguments.SINGLE_TEMPLATE, single.valueOf("--output"))

        val playlist = YtDlpArguments.build("https://www.youtube.com/playlist?list=PL1", mp3, "/work", "/archive")
        assertTrue("--yes-playlist" in playlist && "--ignore-errors" in playlist)
        assertEquals("/archive", playlist.valueOf("--download-archive"))
        assertEquals(YtDlpArguments.COLLECTION_TEMPLATE, playlist.valueOf("--output"))
    }

    @Test fun noCoverArtWhenTurnedOff() {
        val args = YtDlpArguments.build("https://youtu.be/a", mp3.copy(embedArt = false), "/work", "/archive")
        assertFalse("--embed-thumbnail" in args || "--embed-metadata" in args)
    }

    @Test fun namesStaySafeForSharedStorage() {
        assertTrue("--windows-filenames" in YtDlpArguments.build("https://youtu.be/a", mp3, "/work", "/archive"))
    }

    @Test fun unknownQualityFallsBackToTheFormatsDefault() {
        assertEquals("320", QualityPresets.find(OutputFormat.MP3, "nonsense").key)
        assertEquals("1080", QualityPresets.find(OutputFormat.MP4, null).key)
    }

    @Test fun itemsOfTheSameVideoAndFormatShareAKey() {
        assertEquals(
            DownloadItem(1, "https://youtu.be/abc", mp3).key,
            DownloadItem(2, "https://www.youtube.com/watch?v=abc&t=5", mp3).key,
        )
        assertFalse(DownloadItem(1, "https://youtu.be/abc", mp3).key == DownloadItem(2, "https://youtu.be/abc", mp4).key)
    }

    @Test fun completeSaysWhatWasSaved() {
        val item = DownloadItem(1, "https://youtu.be/a", mp3)
        item.reportSavedFile(SavedFile("content://x", "Download/YouLoader/Song.mp3", "audio/mpeg"))
        item.complete()
        assertEquals("Saved · Song.mp3", item.status)

        val playlist = DownloadItem(2, "https://www.youtube.com/playlist?list=PL1", mp3)
        playlist.complete()
        assertEquals("Up to date · nothing new to download", playlist.status)
    }

    @Test fun canceledPlaylistSaysHowManyFilesWereKept() {
        val item = DownloadItem(1, "https://www.youtube.com/playlist?list=PL1", mp3)
        repeat(7) { item.reportSavedFile(SavedFile("content://$it", "Download/YouLoader/List/$it.mp3", "audio/mpeg")) }
        item.markCanceled()
        assertEquals("Canceled · 7 files saved", item.status)
    }

    @Test fun resetPutsAFailedItemBackInTheQueue() {
        val item = DownloadItem(1, "https://youtu.be/a", mp3)
        item.reportStarting()
        item.fail(ErrorMessages.explain(listOf("ERROR: Private video"), 1), "log")
        assertTrue(item.canRetry)
        item.reset()
        assertEquals(DownloadState.QUEUED, item.state)
        assertEquals(null, item.error)
        assertEquals(DownloadItem.WAITING_TEXT, item.status)
    }

    @Test fun activeDownloadsComeFirstThenWaitingThenNewestFinished() {
        var now = 0L
        val clock = { now }
        fun item(id: Long) = DownloadItem(id, "https://youtu.be/$id", mp3, clock)
        now = 1; val finishedFirst = item(1)
        now = 2; val finishedLast = item(2)
        now = 3; val waiting = item(3)
        now = 4; val downloading = item(4)
        now = 5; finishedFirst.reportStarting(); finishedFirst.complete()
        now = 6; finishedLast.reportStarting(); finishedLast.complete()
        now = 7; downloading.reportStarting()

        val sorted = QueueOrder.sort(listOf(finishedFirst, finishedLast, waiting, downloading))
        assertEquals(listOf(4L, 3L, 2L, 1L), sorted.map { it.id })
        assertEquals("1 downloading · 1 waiting", QueueOrder.summary(sorted.map { it.snapshot() }))
    }
}
