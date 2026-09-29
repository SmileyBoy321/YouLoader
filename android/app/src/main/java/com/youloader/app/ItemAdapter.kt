package com.youloader.app

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.core.content.ContextCompat
import androidx.recyclerview.widget.DiffUtil
import androidx.recyclerview.widget.ListAdapter
import androidx.recyclerview.widget.RecyclerView
import com.google.android.material.progressindicator.LinearProgressIndicator
import com.youloader.app.core.DownloadState
import com.youloader.app.core.ItemSnapshot
import com.youloader.app.databinding.ItemDownloadBinding

/** The download list. Rows are rebuilt from [ItemSnapshot]s; which panels are open is kept here. */
class ItemAdapter(private val actions: Actions) : ListAdapter<ItemSnapshot, ItemAdapter.Holder>(Diff) {

    interface Actions {
        fun cancel(item: ItemSnapshot)
        fun retry(item: ItemSnapshot)
        fun remove(item: ItemSnapshot)
        fun play(item: ItemSnapshot)
        fun share(item: ItemSnapshot)
        fun copyDetails(item: ItemSnapshot)
        fun report(item: ItemSnapshot)
    }

    private val explained = mutableSetOf<Long>()
    private val detailsShown = mutableSetOf<Long>()
    private val highlighted = mutableSetOf<Long>()

    /** Briefly outlines an item, e.g. when a duplicate of it was skipped. */
    fun highlight(id: Long, on: Boolean) {
        if (on) highlighted += id else highlighted -= id
        val index = currentList.indexOfFirst { it.id == id }
        if (index >= 0) notifyItemChanged(index)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int) =
        Holder(ItemDownloadBinding.inflate(LayoutInflater.from(parent.context), parent, false))

    override fun onBindViewHolder(holder: Holder, position: Int) = holder.bind(getItem(position))

    inner class Holder(private val b: ItemDownloadBinding) : RecyclerView.ViewHolder(b.root) {
        private val context = b.root.context
        private fun color(id: Int) = ContextCompat.getColor(context, id)

        fun bind(item: ItemSnapshot) {
            b.formatBadge.text = item.format.label
            b.titleText.text = item.title
            b.statusText.text = item.status
            b.statusText.setTextColor(color(when (item.state) {
                DownloadState.DONE -> R.color.success
                DownloadState.FAILED -> R.color.danger
                else -> R.color.muted
            }))
            b.card.strokeColor = color(if (item.id in highlighted) R.color.accent else R.color.card_border)

            b.progress.visibility = if (item.isActive) View.VISIBLE else View.GONE
            if (item.isActive) {
                val waiting = item.state == DownloadState.QUEUED || item.state == DownloadState.PROCESSING || item.progress <= 0
                setMode(b.progress, indeterminate = waiting)
                if (!waiting) b.progress.setProgressCompat(item.progress.toInt(), true)
            }

            val files = item.savedFiles
            val done = item.state == DownloadState.DONE
            b.cancelButton.show(item.isActive)
            b.retryButton.show(item.canRetry)
            b.whyButton.show(item.error != null)
            b.playButton.show(done && files.size == 1)
            b.shareButton.show(done && files.isNotEmpty())
            b.removeButton.show(!item.isActive)
            b.actions.visibility = if (b.actions.hasVisibleChild()) View.VISIBLE else View.GONE

            b.cancelButton.setOnClickListener { actions.cancel(item) }
            b.retryButton.setOnClickListener {
                explained -= item.id
                actions.retry(item)
            }
            b.removeButton.setOnClickListener { actions.remove(item) }
            b.playButton.setOnClickListener { actions.play(item) }
            b.shareButton.setOnClickListener { actions.share(item) }

            val error = item.error
            val showWhy = error != null && item.id in explained
            b.explanation.visibility = if (showWhy) View.VISIBLE else View.GONE
            b.whyButton.setOnClickListener {
                if (!explained.remove(item.id)) explained += item.id
                notifyItemChanged(bindingAdapterPosition)
            }
            if (showWhy) {
                b.causeText.text = error!!.cause
                b.suggestionsText.text = error.suggestions.joinToString("\n") { "•  $it" }
                val hasDetails = item.technicalDetails != null
                b.detailsButton.show(hasDetails)
                b.copyButton.show(hasDetails)
                b.detailsText.text = item.technicalDetails
                b.detailsText.visibility = if (hasDetails && item.id in detailsShown) View.VISIBLE else View.GONE
                b.detailsButton.setOnClickListener {
                    if (!detailsShown.remove(item.id)) detailsShown += item.id
                    notifyItemChanged(bindingAdapterPosition)
                }
                b.copyButton.setOnClickListener { actions.copyDetails(item) }
                b.reportButton.setOnClickListener { actions.report(item) }
            }
        }

        private fun View.show(visible: Boolean) {
            visibility = if (visible) View.VISIBLE else View.GONE
        }

        private fun ViewGroup.hasVisibleChild() = (0 until childCount).any { getChildAt(it).visibility == View.VISIBLE }
    }

    // The indicator refuses to switch modes while it's showing, so hide it for the switch.
    private fun setMode(indicator: LinearProgressIndicator, indeterminate: Boolean) {
        if (indicator.isIndeterminate == indeterminate) return
        val visibility = indicator.visibility
        indicator.visibility = View.INVISIBLE
        indicator.isIndeterminate = indeterminate
        indicator.visibility = visibility
    }

    private object Diff : DiffUtil.ItemCallback<ItemSnapshot>() {
        override fun areItemsTheSame(old: ItemSnapshot, new: ItemSnapshot) = old.id == new.id
        override fun areContentsTheSame(old: ItemSnapshot, new: ItemSnapshot) = old == new
        // Rebind in place instead of cross-fading, so progress moves smoothly.
        override fun getChangePayload(old: ItemSnapshot, new: ItemSnapshot) = true
    }
}
