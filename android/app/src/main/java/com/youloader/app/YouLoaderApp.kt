package com.youloader.app

import android.app.Application
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

class YouLoaderApp : Application() {
    val settings by lazy { Settings(this) }

    /** Work that should outlive any one screen, such as unpacking and updating the engine. */
    val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)

    override fun onCreate() {
        super.onCreate()
        DownloadQueue.init(this)
        DownloadService.createChannels(this)
        scope.launch { Engine.start(this@YouLoaderApp, settings) }
    }
}
