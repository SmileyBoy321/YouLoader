package com.youloader.app.core

import com.youloader.app.AppUpdates
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/** Same cases as the Windows app's ErrorExplanationTests, with Android's own storage errors. */
class ErrorMessagesTest {
    private fun explain(vararg lines: String) = ErrorMessages.explain(lines.toList(), 1)

    @Test fun knownProblemsGetSpecificExplanations() {
        mapOf(
            "ERROR: [youtube] abcdefghijk: Private video. Sign in if you've been granted access" to "This video is private.",
            "ERROR: [youtube] abcdefghijk: Video unavailable" to "This video is unavailable.",
            "ERROR: [youtube] xxxxxxxxxxx: This video is unavailable" to "This video is unavailable.",
            "ERROR: [youtube] abcdefghijk: Sign in to confirm you’re not a bot" to "YouTube asked for a bot check.",
            "ERROR: [youtube] abcdefghijk: Sign in to confirm your age" to "This video is age-restricted.",
            "ERROR: [youtube] abcdefghijk: Join this channel to get access to members-only content" to "This video is for channel members only.",
            "ERROR: [youtube] abcdefghijk: The uploader has not made this video available in your country" to "This video is blocked in your country.",
            "ERROR: [youtube] abcdefghijk: Premieres in 3 hours" to "This video hasn't started yet.",
            "ERROR: unable to download video data: HTTP Error 403: Forbidden" to "YouTube refused the download.",
            "ERROR: unable to download video data: HTTP Error 429: Too Many Requests" to "YouTube is temporarily limiting your connection.",
            "ERROR: Unsupported URL: https://example.com" to "YouLoader can't download from this link.",
            "ERROR: [Errno 28] No space left on device" to "Your phone's storage is full.",
            "ERROR: Couldn't save song.mp3: /storage/emulated/0/Download/YouLoader/song.mp3: open failed: EACCES (Permission denied)" to "YouLoader isn't allowed to save into Downloads.",
            "ERROR: [Errno 36] File name too long: '/data/user/0/com.youloader.app/cache/work/1/…'" to "The video's title is too long to use as a file name.",
            "ERROR: Postprocessing: ffprobe and ffmpeg not found. Please install or provide the path" to "The converter (ffmpeg) is missing.",
            "ERROR: [youtube] abcdefghijk: Requested format is not available" to "This quality isn't available for this video.",
            "ERROR: Unable to download webpage: <urlopen error [Errno 7] No address associated with hostname>" to "Couldn't connect.",
        ).forEach { (line, summary) ->
            val explanation = explain(line)
            assertEquals(line, summary, explanation.summary)
            assertTrue(explanation.cause.isNotBlank())
            assertTrue(explanation.suggestions.isNotEmpty())
        }
    }

    @Test fun conversionFailureUsesFfmpegsRealReason() {
        val explanation = explain(
            "[ExtractAudio] Destination: song.mp3",
            "[mp3 @ 0x7b] Could not write header (incorrect codec parameters ?): Invalid argument",
            "ERROR: Postprocessing: Conversion failed!",
        )
        assertEquals("The file couldn't be converted to this format.", explanation.summary)
        assertTrue(explanation.suggestions.any { "Cover art" in it })
    }

    @Test fun debugLinesDoNotCauseFalseMatches() {
        val explanation = explain(
            "[debug] Command-line config: ['--postprocessor-args', 'Conversion failed', 'Private video']",
            "ERROR: [youtube] abc: Video unavailable",
        )
        assertEquals("This video is unavailable.", explanation.summary)
    }

    @Test fun unknownErrorShowsWhatTheEngineSaidWithoutThePrefix() {
        val explanation = explain("ERROR: Something nobody has seen before")
        assertEquals("Something went wrong.", explanation.summary)
        assertTrue("Something nobody has seen before" in explanation.cause)
        assertFalse("ERROR:" in explanation.cause)
    }

    @Test fun noErrorLineAtAllMentionsTheExitCode() {
        assertTrue("exit code 2" in ErrorMessages.explain(listOf("[youtube] hello"), 2).cause)
        assertTrue("without saying why" in ErrorMessages.explain(listOf("[youtube] hello"), null).cause)
    }

    @Test fun transientErrorsAreRetried() {
        assertTrue(ErrorMessages.isTransient("ERROR: unable to download video data: HTTP Error 403: Forbidden"))
        assertTrue(ErrorMessages.isTransient("ERROR: The read operation timed out"))
        assertFalse(ErrorMessages.isTransient("ERROR: [youtube] abc: Private video"))
        assertFalse(ErrorMessages.isTransient(null))
    }

    @Test fun catalogueIdsAreUnique() {
        val ids = ErrorMessages.catalogue.map { it.first }
        assertEquals(ids.size, ids.toSet().size)
    }

    @Test fun technicalLogKeepsTheLastFortyShortLinesAndHidesPrivateFolders() {
        val log = (1..100).map { "line $it /data/user/0/com.youloader.app/cache/x" }
        val text = TechnicalLog.format(log, listOf("/data/user/0/com.youloader.app"))
        assertTrue(text.startsWith("line 61"))
        assertTrue(text.endsWith("line 100 <app>/cache/x"))
        assertFalse("com.youloader.app" in text)
        assertTrue(TechnicalLog.format(listOf("x".repeat(1000))).let { it.length < 500 && it.endsWith("…") })
    }

    @Test fun technicalLogKeepsAtMost120Lines() {
        val log = TechnicalLog()
        repeat(200) { log.add("line $it") }
        assertEquals(120, log.snapshot().size)
        assertEquals("line 199", log.snapshot().last())
    }

    @Test fun appUpdateVersionsCompareByNumber() {
        assertTrue(AppUpdates.isNewer("v2.3.0", "2.2.0"))
        assertTrue(AppUpdates.isNewer("2.10", "2.9.1"))
        assertFalse(AppUpdates.isNewer("v2.2", "2.2.0"))
        assertFalse(AppUpdates.isNewer("2.1.9", "2.2.0"))
        assertFalse(AppUpdates.isNewer("nightly", "2.2.0"))
    }
}
