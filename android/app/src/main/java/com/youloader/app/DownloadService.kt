package com.youloader.app

import android.Manifest
import android.annotation.SuppressLint
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.app.ServiceCompat
import androidx.core.content.ContextCompat
import com.youloader.app.core.DownloadState
import com.youloader.app.core.ItemSnapshot
import com.youloader.app.core.QueueOrder
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/**
 * Keeps YouLoader running while downloads are in progress, even when the app is closed,
 * and shows their progress in a notification. Stops itself when the list has nothing left to do.
 */
class DownloadService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private var watcher: Job? = null
    private var startedAt = 0L

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        // Every start must promise the foreground again, including one that arrives just after the last download ended.
        ServiceCompat.startForeground(
            this, PROGRESS_ID, progressNotification(DownloadQueue.snapshot()),
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC else 0,
        )
        if (watcher == null) startedAt = System.currentTimeMillis()
        if (watcher == null) watcher = scope.launch {
            DownloadQueue.changes.collect {
                val items = DownloadQueue.snapshot()
                if (items.none { it.isActive }) {
                    finish(items)
                    return@collect
                }
                notifyProgress(items)
                // Android throttles apps that update a notification too often.
                delay(1000)
            }
        }
        return START_NOT_STICKY
    }

    override fun onDestroy() {
        scope.cancel()
        super.onDestroy()
    }

    private fun finish(items: List<ItemSnapshot>) {
        ServiceCompat.stopForeground(this, ServiceCompat.STOP_FOREGROUND_REMOVE)
        // Only worth a notification when the user isn't looking at the list already.
        val finishedHere = items.filter { (it.finishedAt ?: 0) >= startedAt }
        if (!MainActivity.isVisible && finishedHere.isNotEmpty()) notifyFinished(finishedHere)
        watcher?.cancel()
        watcher = null
        stopSelf()
    }

    @SuppressLint("MissingPermission") // Checked in canNotify().
    private fun notifyProgress(items: List<ItemSnapshot>) {
        if (!canNotify()) return
        NotificationManagerCompat.from(this).notify(PROGRESS_ID, progressNotification(items))
    }

    private fun progressNotification(items: List<ItemSnapshot>) =
        NotificationCompat.Builder(this, PROGRESS_CHANNEL).apply {
            val current = items.firstOrNull { it.state == DownloadState.DOWNLOADING || it.state == DownloadState.PROCESSING }
                ?: items.firstOrNull { it.isActive }
            setSmallIcon(R.drawable.ic_stat_download)
            setContentTitle(current?.title ?: "Getting ready…")
            setContentText(current?.status ?: "")
            setSubText(QueueOrder.summary(items))
            setOngoing(true)
            setOnlyAlertOnce(true)
            setSilent(true)
            setContentIntent(openAppIntent())
            setCategory(NotificationCompat.CATEGORY_PROGRESS)
            when (current?.state) {
                DownloadState.DOWNLOADING -> setProgress(100, current.progress.toInt(), current.progress <= 0)
                else -> setProgress(100, 0, true)
            }
        }.build()

    @SuppressLint("MissingPermission") // Checked in canNotify().
    private fun notifyFinished(items: List<ItemSnapshot>) {
        if (!canNotify()) return
        val saved = items.filter { it.state == DownloadState.DONE }
        val failed = items.count { it.state == DownloadState.FAILED }
        val title = when {
            failed == 0 && saved.size == 1 -> saved[0].title
            failed == 0 -> "${saved.size} downloads finished"
            saved.isEmpty() -> if (failed == 1) "A download failed" else "$failed downloads failed"
            else -> "${saved.size} finished · $failed failed"
        }
        val text = when {
            failed > 0 -> "Open YouLoader and tap Why? to see what happened."
            else -> "Saved to ${PublicDownloads.displayFolder}"
        }
        val notification = NotificationCompat.Builder(this, FINISHED_CHANNEL)
            .setSmallIcon(R.drawable.ic_stat_download)
            .setContentTitle(title)
            .setContentText(text)
            .setContentIntent(openAppIntent())
            .setAutoCancel(true)
            .build()
        NotificationManagerCompat.from(this).notify(FINISHED_ID, notification)
    }

    private fun openAppIntent(): PendingIntent = PendingIntent.getActivity(
        this, 0,
        Intent(this, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP),
        PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
    )

    // Android 13 added a permission for notifications; older versions only have the on/off switch in Settings.
    private fun canNotify() = NotificationManagerCompat.from(this).areNotificationsEnabled() &&
        (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU ||
            ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED)

    companion object {
        private const val PROGRESS_CHANNEL = "downloads"
        private const val FINISHED_CHANNEL = "finished"
        private const val PROGRESS_ID = 1
        private const val FINISHED_ID = 2

        fun start(context: Context) {
            ContextCompat.startForegroundService(context, Intent(context, DownloadService::class.java))
        }

        fun createChannels(context: Context) {
            if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
            val manager = context.getSystemService(NotificationManager::class.java)
            manager.createNotificationChannel(
                NotificationChannel(PROGRESS_CHANNEL, "Download progress", NotificationManager.IMPORTANCE_LOW).apply {
                    description = "Shows what's downloading while YouLoader works in the background."
                    setShowBadge(false)
                },
            )
            manager.createNotificationChannel(
                NotificationChannel(FINISHED_CHANNEL, "Finished downloads", NotificationManager.IMPORTANCE_DEFAULT).apply {
                    description = "Tells you when downloads finish while YouLoader is in the background."
                },
            )
        }
    }
}
