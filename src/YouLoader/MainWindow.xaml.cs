using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using YouLoader.Core.Models;
using YouLoader.Core.Services;

namespace YouLoader;

public partial class MainWindow : Window
{
    const int MaxParallelDownloads = 2;
    const string SourceUrl = "https://github.com/SmileyBoy321/YouLoader";

    static readonly Dictionary<OutputFormat, string> FormatHints = new()
    {
        [OutputFormat.Mp3] = "Music that plays everywhere: phones, computers, car stereos and MP3 players. Saved with cover art and song info.",
        [OutputFormat.Mp4] = "Video with sound. 1080p and below use H.264, which plays everywhere.",
    };

    readonly AppSettings settings = AppSettings.Load();
    readonly ToolManager tools = new();
    readonly SemaphoreSlim slots = new(MaxParallelDownloads);
    DownloadService? downloader;
    OutputFormat selectedFormat;
    QualityPreset? selectedQuality;
    AppUpdate? availableUpdate;
    string? lastClipboardLink; // last link picked up from the clipboard, so it isn't pasted twice
    string? autoPastedText;    // what the box held right after auto-paste; if it still does, the user hasn't touched it

    public ObservableCollection<DownloadItem> Items { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Items.CollectionChanged += (_, _) => EmptyText.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ApplySettings();
        Loaded += async (_, _) => await PrepareToolsAsync();
        Activated += (_, _) => AutoPasteFromClipboard();
        Closing += OnClosing;
        SourceInitialized += (_, _) => UseDarkTitleBar();
    }

    // Matches the Windows title bar to the dark window (Windows 10 20H1 and later).
    void UseDarkTitleBar()
    {
        const int DwmwaUseImmersiveDarkMode = 20;
        var enabled = 1;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    // ---- Settings ----

    void ApplySettings()
    {
        var formatChip = FormatPanel.Children.OfType<RadioButton>()
            .FirstOrDefault(r => (string)r.Tag == settings.Format.ToString())
            ?? FormatPanel.Children.OfType<RadioButton>().First();
        formatChip.IsChecked = true;

        PlaylistBox.IsChecked = settings.WholePlaylist;
        EmbedArtBox.IsChecked = settings.EmbedArt;
        OutputText.Text = settings.OutputDir;
        OutputText.ToolTip = settings.OutputDir;
    }

    void SaveSettings()
    {
        settings.Format = selectedFormat;
        if (selectedQuality is not null) settings.Quality[selectedFormat.ToString()] = selectedQuality.Key;
        settings.WholePlaylist = PlaylistBox.IsChecked == true;
        settings.EmbedArt = EmbedArtBox.IsChecked == true;
        settings.Save();
    }

    void Format_Checked(object sender, RoutedEventArgs e)
    {
        selectedFormat = Enum.Parse<OutputFormat>((string)((RadioButton)sender).Tag);
        FormatHint.Text = FormatHints[selectedFormat];
        BuildQualityChips();
    }

    void BuildQualityChips()
    {
        QualityPanel.Children.Clear();
        selectedQuality = null;
        var wanted = settings.QualityFor(selectedFormat);

        foreach (var preset in QualityPresets.For(selectedFormat))
        {
            var chip = new RadioButton
            {
                Content = preset.Label,
                GroupName = "quality",
                Style = (Style)FindResource("Chip"),
            };
            chip.Checked += (_, _) =>
            {
                selectedQuality = preset;
                settings.Quality[selectedFormat.ToString()] = preset.Key;
            };
            QualityPanel.Children.Add(chip);
            if (preset.Key == wanted) chip.IsChecked = true;
        }
    }

    // ---- Tools ----

    async Task PrepareToolsAsync()
    {
        DownloadButton.IsEnabled = false;
        UpdateToolsButton.IsEnabled = false;
        var status = new Progress<string>(text => ToolStatus.Text = text);
        try
        {
            await tools.EnsureAsync(status);
            downloader = new DownloadService(tools);
            DownloadButton.IsEnabled = true;
            await ShowToolVersionAsync();

            if (DateTime.UtcNow - settings.LastToolUpdateUtc > TimeSpan.FromDays(1))
                await UpdateToolsAsync(quiet: true);

            await CheckForAppUpdateAsync();
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or TaskCanceledException or UnauthorizedAccessException)
        {
            ToolStatus.Text = $"Setup failed: {e.Message}";
            UpdateToolsButton.Content = "Retry setup";
        }
        finally
        {
            UpdateToolsButton.IsEnabled = true;
        }
    }

    async Task ShowToolVersionAsync()
    {
        var version = await tools.GetYtDlpVersionAsync();
        ToolStatus.Text = $"Ready · yt-dlp {version} · ffmpeg ✓ · {tools.JsRuntimeName}";
    }

    async Task UpdateToolsAsync(bool quiet)
    {
        // Replacing yt-dlp.exe while downloads use it would break them.
        if (Items.Any(i => i.IsActive))
        {
            if (!quiet) ToolStatus.Text = "Finish or cancel running downloads before updating yt-dlp.";
            return;
        }

        UpdateToolsButton.IsEnabled = false;
        DownloadButton.IsEnabled = false;
        ToolStatus.Text = "Checking for a newer yt-dlp…";
        try
        {
            var result = await tools.UpdateYtDlpAsync();
            settings.LastToolUpdateUtc = DateTime.UtcNow;
            settings.Save();
            await ShowToolVersionAsync();
            if (!quiet && result.Length > 0) ToolStatus.Text += $" · {result}";
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            ToolStatus.Text = $"Couldn't update yt-dlp: {e.Message}";
        }
        finally
        {
            UpdateToolsButton.IsEnabled = true;
            DownloadButton.IsEnabled = downloader is not null;
        }
    }

    async Task CheckForAppUpdateAsync()
    {
        var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("YouLoader");
        availableUpdate = await UpdateChecker.CheckAsync(current, http);
        if (availableUpdate is null) return;

        UpdateText.Text = $"YouLoader {availableUpdate.Version} is out. You have {current.ToString(3)}.";
        UpdateBanner.Visibility = Visibility.Visible;
    }

    async void UpdateTools_Click(object sender, RoutedEventArgs e)
    {
        if (downloader is null)
        {
            UpdateToolsButton.Content = "Update yt-dlp";
            await PrepareToolsAsync();
        }
        else
        {
            await UpdateToolsAsync(quiet: false);
        }
    }

    void GetUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (availableUpdate is not null) OpenUrl(availableUpdate.Url);
    }

    // ---- Links ----

    void UrlBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UrlPlaceholder.Visibility = UrlBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    // Enter downloads; Shift+Enter adds a new line.
    void UrlBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
        e.Handled = true;
        StartDownloads();
    }

    void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsText()) return;
        AppendLinks(Clipboard.GetText());
        UrlBox.Focus();
        UrlBox.CaretIndex = UrlBox.Text.Length;
    }

    // Saves a click: a freshly copied YouTube or SoundCloud link is waiting in the box when you switch back.
    // A newly copied link replaces one YouLoader pasted itself, but never overwrites what the user typed:
    // then it's added on a new line instead, so copying links one after another builds a list.
    void AutoPasteFromClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText().Trim();
            if (text == lastClipboardLink || text.Length > 2000) return;
            var urls = LinkParser.ExtractUrls(text);
            if (urls.Count == 0 || !urls.All(IsSupportedSite)) return;
            lastClipboardLink = text;

            if (UrlBox.Text.Length == 0 || UrlBox.Text == autoPastedText)
            {
                UrlBox.Text = text;
                autoPastedText = UrlBox.Text;
            }
            else if (!UrlBox.Text.Contains(text, StringComparison.Ordinal))
            {
                AppendLinks(text);
                autoPastedText = null;
            }
            UrlBox.CaretIndex = UrlBox.Text.Length;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another app is holding the clipboard; skip this time.
        }
    }

    static bool IsSupportedSite(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Host.Contains("youtube.com") || uri.Host == "youtu.be" || uri.Host.EndsWith("soundcloud.com"));

    void AppendLinks(string text)
    {
        var separator = UrlBox.Text.Length == 0 || UrlBox.Text.EndsWith('\n') ? "" : Environment.NewLine;
        UrlBox.AppendText(separator + text.Trim());
    }

    // ---- Downloads ----

    void Download_Click(object sender, RoutedEventArgs e) => StartDownloads();

    void StartDownloads()
    {
        if (downloader is null || selectedQuality is null) return;

        var urls = LinkParser.ExtractUrls(UrlBox.Text);
        if (urls.Count == 0)
        {
            MessageBox.Show(this, "Paste at least one link that starts with https://", "YouLoader",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var request = new DownloadRequest(
            selectedFormat,
            selectedQuality.Key,
            PlaylistBox.IsChecked == true,
            EmbedArtBox.IsChecked == true,
            settings.OutputDir);

        List<DownloadItem> alreadyListed = [];
        var added = 0;
        foreach (var url in urls)
        {
            var item = new DownloadItem(url, request);

            // The same video in the same format is already downloading or done: point at it instead of adding a copy.
            var existing = Items.FirstOrDefault(i => i.Key == item.Key && (i.IsActive || i.IsDone));
            if (existing is not null)
            {
                if (!alreadyListed.Contains(existing)) alreadyListed.Add(existing);
                continue;
            }

            Items.Insert(0, item);
            _ = RunAsync(item);
            added++;
        }

        UrlBox.Clear();
        autoPastedText = null;
        SaveSettings();

        if (alreadyListed.Count == 0)
        {
            HideNotice();
            QueueScroll.ScrollToTop();
            return;
        }

        var skipped = alreadyListed.Count;
        ShowNotice(skipped == 1
            ? $"“{alreadyListed[0].Title}” is already in your list as {alreadyListed[0].FormatLabel}, so it wasn't added again. It's highlighted below."
            : $"{skipped} links are already in your list in this format, so they weren't added again. They're highlighted below.");
        foreach (var item in alreadyListed) _ = HighlightAsync(item);
        if (added == 0) BringIntoView(alreadyListed[0]);
        else QueueScroll.ScrollToTop();
    }

    // ---- Notices ----

    CancellationTokenSource? noticeTimer;

    async void ShowNotice(string text)
    {
        NoticeText.Text = text;
        NoticeBar.Visibility = Visibility.Visible;

        noticeTimer?.Cancel();
        var timer = noticeTimer = new CancellationTokenSource();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), timer.Token);
            HideNotice();
        }
        catch (TaskCanceledException)
        {
            // A newer notice replaced this one.
        }
    }

    void HideNotice() => NoticeBar.Visibility = Visibility.Collapsed;

    static async Task HighlightAsync(DownloadItem item)
    {
        item.IsHighlighted = true;
        await Task.Delay(TimeSpan.FromSeconds(3));
        item.IsHighlighted = false;
    }

    void BringIntoView(DownloadItem item)
    {
        if (QueueList.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container)
            container.BringIntoView();
    }

    async Task RunAsync(DownloadItem item)
    {
        try
        {
            await slots.WaitAsync(item.Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            item.MarkCanceled();
            return;
        }

        try
        {
            await downloader!.RunAsync(item);
        }
        finally
        {
            slots.Release();
        }
    }

    static DownloadItem ItemOf(object sender) => (DownloadItem)((FrameworkElement)sender).DataContext;

    void Cancel_Click(object sender, RoutedEventArgs e) => ItemOf(sender).Cancel();

    void Retry_Click(object sender, RoutedEventArgs e)
    {
        var item = ItemOf(sender);
        item.Reset();
        _ = RunAsync(item);
    }

    void Why_Click(object sender, RoutedEventArgs e)
    {
        var item = ItemOf(sender);
        item.ShowExplanation = !item.ShowExplanation;
    }

    void ToggleDetails_Click(object sender, RoutedEventArgs e)
    {
        var item = ItemOf(sender);
        item.ShowTechnicalDetails = !item.ShowTechnicalDetails;
    }

    void CopyDetails_Click(object sender, RoutedEventArgs e)
    {
        if (CopyDetails(ItemOf(sender)))
            ShowNotice("Technical details copied. Your Windows user folder appears as %USERPROFILE%, so they're safe to share.");
    }

    // Opens a new GitHub issue with the basics filled in. The log is copied first, so the user only has to paste it.
    void Report_Click(object sender, RoutedEventArgs e)
    {
        var item = ItemOf(sender);
        var copied = CopyDetails(item);
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
        var body =
            $"**What happened:** {item.Error?.Summary}\n" +
            $"**Format:** {item.FormatLabel}, quality {item.Request.Quality}\n" +
            $"**YouLoader:** {version}\n\n" +
            (copied ? "**Technical details** (already copied, paste them between the lines below):\n\n```\n\n```\n\n" : "") +
            "**Anything else?** For example the link, if you're happy to share it.\n";
        var title = Uri.EscapeDataString(item.Error?.Summary ?? "Download failed");
        OpenUrl($"{SourceUrl}/issues/new?title={title}&body={Uri.EscapeDataString(body)}");
    }

    static bool CopyDetails(DownloadItem item)
    {
        if (item.TechnicalDetails is null) return false;
        try
        {
            Clipboard.SetText(item.TechnicalDetails);
            return true;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another app is holding the clipboard.
            return false;
        }
    }

    void Play_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender).FilePath is { } path && File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    void Show_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender).FilePath is { } path && File.Exists(path))
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        else
            OpenFolder();
    }

    void ClearFinished_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items.Where(i => !i.IsActive).ToList()) Items.Remove(item);
    }

    // ---- Folder ----

    void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose where to save downloads",
            InitialDirectory = Directory.Exists(settings.OutputDir) ? settings.OutputDir : null,
        };
        if (dialog.ShowDialog(this) != true) return;

        settings.OutputDir = dialog.FolderName;
        OutputText.Text = settings.OutputDir;
        OutputText.ToolTip = settings.OutputDir;
        SaveSettings();
    }

    void OpenFolder_Click(object sender, RoutedEventArgs e) => OpenFolder();

    void OpenFolder()
    {
        Directory.CreateDirectory(settings.OutputDir);
        Process.Start("explorer.exe", $"\"{settings.OutputDir}\"");
    }

    // ---- Links out ----

    void Source_Click(object sender, RoutedEventArgs e) => OpenUrl(SourceUrl);

    static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    // ---- Closing ----

    void OnClosing(object? sender, CancelEventArgs e)
    {
        var running = Items.Count(i => i.IsActive);
        if (running > 0)
        {
            var answer = MessageBox.Show(this,
                $"{running} download(s) are still running. Quit and cancel them?",
                "YouLoader", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            foreach (var item in Items.Where(i => i.IsActive)) item.Cancel();
        }
        SaveSettings();
    }
}
