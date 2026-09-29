package com.youloader.app.core

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** Same cases as the Windows app's OutputParserTests and TransferMeterTests. */
class OutputParserTest {
    private fun progress(line: String) = OutputParser.parse(line) as DownloadProgress

    @Test fun parsesProgressLine() {
        val p = progress("[dl]10198052|71283827|none|||Me at the zoo")
        assertEquals(10198052L, p.downloadedBytes)
        assertEquals(71283827L, p.totalBytes)
        assertEquals(14.3, p.percent, 0.05)
        assertFalse(p.isVideoStream)
        assertNull(p.playlistIndex)
        assertEquals("Me at the zoo", p.displayTitle)
    }

    @Test fun keepsPipesThatArePartOfTheTitle() {
        val p = progress("[dl]1|10|avc1|||Song | Live | 2024")
        assertEquals("Song | Live | 2024", p.title)
        assertTrue(p.isVideoStream)
    }

    @Test fun showsPlaylistPositionInTitle() {
        val p = progress("[dl]100|100|none|3|12|Track")
        assertEquals(3, p.playlistIndex)
        assertEquals(12, p.playlistCount)
        assertEquals("(3/12) Track", p.displayTitle)
    }

    @Test fun unknownTotalIsMissingNotZero() {
        for (unknown in listOf("NA", "", "  ")) {
            val p = progress("[dl]1024|$unknown|none|||x")
            assertNull(p.totalBytes)
            assertEquals(0.0, p.percent, 0.0)
        }
    }

    @Test fun totalEstimatesWithDecimalsAreAccepted() = assertEquals(1000L, progress("[dl]500|1000.7|none|||x").totalBytes)

    @Test fun percentNeverGoesAbove100() = assertEquals(100.0, progress("[dl]1200|1000|none|||x").percent, 0.0)

    @Test fun brokenProgressLineIsIgnored() = assertNull(OutputParser.parse("[dl]45|only|three"))

    @Test fun parsesProcessingStepAndSavedFile() {
        assertEquals(ProcessingStep("ExtractAudio"), OutputParser.parse("[pp]ExtractAudio"))
        assertEquals(
            FileSaved("/data/user/0/com.youloader.app/cache/work/1/Árcangel – ÿ.mp3"),
            OutputParser.parse("[file]/data/user/0/com.youloader.app/cache/work/1/Árcangel – ÿ.mp3"),
        )
    }

    @Test fun otherLinesAreIgnored() {
        listOf("[youtube] Extracting URL: https://youtu.be/x", "[download] Destination: x.webm", "", "WARNING: something")
            .forEach { assertNull(it, OutputParser.parse(it)) }
    }

    @Test fun describeShowsWholePercentSizeSpeedAndTimeLeft() {
        val p = DownloadProgress(31_562_137, 71_303_987, false, null, null, "x")
        val reading = TransferReading(4.4 * 1024 * 1024, 12)
        assertEquals("44% · 30.1 of 68.0 MB · 4.4 MB/s · 0:12 left", OutputParser.describe(p, OutputFormat.MP3, reading))
    }

    @Test fun describeVideoProgressSaysWhichStreamIsDownloading() {
        val none = TransferReading(null, null)
        assertTrue(OutputParser.describe(DownloadProgress(10, 100, true, null, null, "x"), OutputFormat.MP4, none).startsWith("Video · 10%"))
        assertTrue(OutputParser.describe(DownloadProgress(10, 100, false, null, null, "x"), OutputFormat.MP4, none).startsWith("Audio · 10%"))
    }

    @Test fun describeWithoutKnownSizeShowsBytesSoFar() {
        assertEquals("2.0 MB", OutputParser.describe(DownloadProgress(2 * 1024 * 1024, null, false, null, null, "x"), OutputFormat.MP3, TransferReading(null, null)))
    }

    @Test fun describeStep() {
        assertEquals("Converting to MP3…", OutputParser.describeStep("ExtractAudio"))
        assertEquals("Merging video and audio…", OutputParser.describeStep("Merger"))
        assertEquals("Adding cover art…", OutputParser.describeStep("EmbedThumbnail"))
        assertEquals("Processing…", OutputParser.describeStep("SomethingNew"))
    }
}

class TransferMeterTest {
    private val start = 1_000_000L
    private val mb = 1024L * 1024

    /**
     * Replays what yt-dlp does with 4 parallel chunks: nothing arrives for a while, then 2 MB at once.
     * The true average is 4 MB/s, but yt-dlp's own instant speed swings between 0 and 20 MB/s.
     */
    private fun replayBurstyDownload(total: Long, seconds: Int): List<Pair<Long, TransferReading>> {
        val meter = TransferMeter()
        val readings = mutableListOf<Pair<Long, TransferReading>>()
        var downloaded = 0L
        for (tick in 0..seconds * 10) {
            val at = tick * 100L
            if (tick > 0 && tick % 5 == 0) downloaded += 2 * mb
            meter.update(start + at, downloaded, total)?.let { readings += at to it }
        }
        return readings
    }

    @Test fun speedSettlesNearTheRealAverageDespiteBursts() {
        for ((_, reading) in replayBurstyDownload(200 * mb, 20).filter { it.first >= 6000 }) {
            val speed = reading.bytesPerSecond!! / mb
            assertTrue("speed $speed", speed in 3.4..4.6)
        }
    }

    @Test fun timeLeftCountsDownSmoothlyInsteadOfJumping() {
        val steady = replayBurstyDownload(200 * mb, 20).filter { it.first >= 6000 }.map { it.second.secondsLeft!! }
        for (i in 1 until steady.size) assertTrue(steady[i - 1] - steady[i] in -2..4)
    }

    @Test fun refreshesAtMostOncePerSecond() {
        val readings = replayBurstyDownload(200 * mb, 10)
        assertTrue(readings.size in 10..11)
        for (i in 1 until readings.size) assertTrue(readings[i].first - readings[i - 1].first >= TransferMeter.DISPLAY_INTERVAL_MS)
    }

    @Test fun noSpeedOrTimeLeftUntilThereIsASecondOfRealData() {
        val meter = TransferMeter()
        val first = meter.update(start, 1024, 143 * mb)!!
        val early = meter.update(start + 500, 4 * mb, 143 * mb)
        val afterOneSecond = meter.update(start + 1000, 5 * mb, 143 * mb)!!
        assertNull(first.bytesPerSecond)
        assertNull(first.secondsLeft)
        assertNull(early)
        assertTrue(afterOneSecond.secondsLeft!! in 25..35)
    }

    @Test fun newFileStartsFresh() {
        val meter = TransferMeter()
        meter.update(start, 0, 100 * mb)
        meter.update(start + 1000, 50 * mb, 100 * mb)
        // The audio stream starts after the video: bytes drop back to zero, and the video's speed must not carry over.
        assertNull(meter.update(start + 1100, 0, 4 * mb)!!.bytesPerSecond)
    }

    @Test fun unknownTotalGivesSpeedButNoTimeLeft() {
        val meter = TransferMeter()
        meter.update(start, 0, null)
        val reading = meter.update(start + 2000, 4 * mb, null)!!
        assertEquals(2.0 * mb, reading.bytesPerSecond!!, 1.0)
        assertNull(reading.secondsLeft)
    }

    @Test fun formatsSizesAndTimes() {
        assertEquals("512 B", TransferMeter.formatBytes(512.0))
        assertEquals("2 KB", TransferMeter.formatBytes(2048.0))
        assertEquals("30.1 MB", TransferMeter.formatBytes(31_562_137.0))
        assertEquals("3.00 GB", TransferMeter.formatBytes(3_221_225_472.0))
        assertEquals("0:12", TransferMeter.formatTimeLeft(12))
        assertEquals("3:05", TransferMeter.formatTimeLeft(185))
        assertEquals("1:02:03", TransferMeter.formatTimeLeft(3723))
    }
}
