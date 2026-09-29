package com.youloader.app

import android.content.Context
import com.yausername.ffmpeg.FFmpeg
import com.yausername.youtubedl_android.YoutubeDL
import com.yausername.youtubedl_android.YoutubeDLException
import com.yausername.youtubedl_android.YoutubeDLRequest
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.util.concurrent.atomic.AtomicInteger

/**
 * The download engine: yt-dlp, Python, ffmpeg and QuickJS, all bundled by youtubedl-android.
 * The library adds --ffmpeg-location and --js-runtimes to every run by itself.
 *
 * The bundled yt-dlp is months old by the time anyone installs the app, and YouTube changes often,
 * so it's updated once a day, like the Windows app. Updating replaces yt-dlp's files, so an update
 * waits until no download is running, and downloads wait while an update runs.
 */
object Engine {
    enum class State { STARTING, READY, UPDATING, FAILED }

    private val _state = MutableStateFlow(State.STARTING)
    val state: StateFlow<State> = _state

    private val _version = MutableStateFlow<String?>(null)
    /** yt-dlp's version, e.g. "2026.08.19", or null for the copy bundled with the app. */
    val version: StateFlow<String?> = _version

    @Volatile var startError: Throwable? = null
        private set

    private val lock = Mutex()
    private val running = AtomicInteger()
    private const val DAY_MS = 24L * 60 * 60 * 1000

    /** Unpacks Python and ffmpeg (a few seconds on first launch), then updates yt-dlp if it's due. */
    suspend fun start(context: Context, settings: Settings) {
        if (_state.value != State.STARTING) return
        val app = context.applicationContext
        val ok = withContext(Dispatchers.IO) {
            runCatching {
                YoutubeDL.init(app)
                FFmpeg.init(app)
            }.onFailure { startError = it }.isSuccess
        }
        if (!ok) {
            _state.value = State.FAILED
            return
        }
        _version.value = readVersion(app)
        _state.value = State.READY
        if (System.currentTimeMillis() - settings.lastEngineUpdate > DAY_MS) update(app, settings)
    }

    sealed interface UpdateResult {
        data class Updated(val version: String?) : UpdateResult
        data object AlreadyUpToDate : UpdateResult
        data object Busy : UpdateResult
        data class Failed(val error: Throwable) : UpdateResult
    }

    /** Updates yt-dlp from its GitHub releases. Skipped while downloads are running. */
    suspend fun update(context: Context, settings: Settings): UpdateResult {
        if (_state.value != State.READY) return UpdateResult.Busy
        return lock.withLock {
            if (running.get() > 0 || _state.value != State.READY) return@withLock UpdateResult.Busy
            _state.value = State.UPDATING
            try {
                val status = withContext(Dispatchers.IO) {
                    YoutubeDL.updateYoutubeDL(context.applicationContext, YoutubeDL.UpdateChannel.STABLE)
                }
                settings.lastEngineUpdate = System.currentTimeMillis()
                _version.value = readVersion(context.applicationContext)
                if (status == YoutubeDL.UpdateStatus.DONE) UpdateResult.Updated(_version.value) else UpdateResult.AlreadyUpToDate
            } catch (e: Exception) {
                UpdateResult.Failed(e)
            } finally {
                _state.value = State.READY
            }
        }
    }

    /** Runs [block] as one download: waits for startup or an update to finish, and holds off updates meanwhile. */
    suspend fun <T> useForDownload(block: suspend () -> T): T {
        val state = _state.first { it == State.READY || it == State.FAILED }
        if (state == State.FAILED) throw EngineUnavailable(startError)
        lock.withLock { running.incrementAndGet() }
        try {
            return block()
        } finally {
            running.decrementAndGet()
        }
    }

    class EngineUnavailable(cause: Throwable?) : Exception(cause?.message, cause)

    /** How a run ended. [exitCode] is null when yt-dlp failed and the library didn't report the code. */
    data class RunResult(val exitCode: Int?, val canceled: Boolean)

    /** Runs yt-dlp once and blocks until it ends. [onLine] receives every line it prints. */
    fun run(url: String, args: List<String>, processId: String, onLine: (String) -> Unit): RunResult {
        val request = YoutubeDLRequest(url).addCommands(args)
        return try {
            // stderr is merged into the same stream so errors arrive in order with everything else.
            val response = YoutubeDL.execute(request, processId, redirectErrorStream = true) { _, _, line ->
                if (line.isNotBlank()) onLine(line)
            }
            RunResult(response.exitCode, canceled = false)
        } catch (e: YoutubeDL.CanceledException) {
            RunResult(null, canceled = true)
        } catch (e: YoutubeDLException) {
            RunResult(null, canceled = false)
        } catch (e: InterruptedException) {
            RunResult(null, canceled = true)
        }
    }

    fun cancel(processId: String) = YoutubeDL.destroyProcessById(processId)

    private fun readVersion(context: Context) = YoutubeDL.versionName(context)?.removePrefix("yt-dlp ")
}
