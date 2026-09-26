using System.Diagnostics;
using System.IO;
using System.Windows;
using YouLoader.Core.Services;

namespace YouLoader;

/// <summary>Shows exactly what uninstalling removes, removes it, then says goodbye.</summary>
public partial class UninstallWindow : Window
{
    readonly string outputDir;

    public sealed record Row(string Name, string Detail, string Size);

    public UninstallWindow(string outputDir)
    {
        InitializeComponent();
        this.outputDir = outputDir;
        Plan = Uninstaller.Plan(Environment.ProcessPath, outputDir);
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);

        Rows.ItemsSource = BuildRows();
        TotalText.Text = Uninstaller.FormatSize(Plan.Bytes);
        KeptText.Text = $"Nothing you downloaded is deleted. It all stays in:\n{outputDir}";
    }

    public UninstallPlan Plan { get; }

    /// <summary>True once everything (except the running exe, removed right after closing) is gone.</summary>
    public bool Removed { get; private set; }

    List<Row> BuildRows()
    {
        List<Row> rows = [];
        if (Plan.ExePath is { } exe)
            rows.Add(new("YouLoader.exe", Path.GetDirectoryName(exe) ?? "", Size(exe)));
        if (Has(Uninstaller.SettingsDir))
            rows.Add(new("Settings", "Your format, quality and save folder choices", Size(Uninstaller.SettingsDir)));
        if (Has(Uninstaller.DataDir))
            rows.Add(new("Helper tools", "yt-dlp, ffmpeg and Deno, downloaded on first launch", Size(Uninstaller.DataDir)));
        if (Has(Uninstaller.UnpackDir))
            rows.Add(new("Temporary files", "Unpacked by YouLoader each time a new version starts", Size(Uninstaller.UnpackDir)));
        if (Plan.Files.Count > 0)
            rows.Add(new("Playlist history",
                Plan.Files.Count == 1 ? "1 hidden file in your save folder" : $"{Plan.Files.Count} hidden files in your save folder",
                Uninstaller.FormatSize(Plan.Files.Sum(f => new FileInfo(f).Length))));
        return rows;
    }

    bool Has(string folder) => Plan.Folders.Any(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));

    static string Size(string path) => Uninstaller.FormatSize(Uninstaller.SizeOf(path));

    void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        KeepButton.IsEnabled = UninstallButton.IsEnabled = false;
        var failed = Uninstaller.RemoveData(Plan);
        Removed = true;

        DoneText.Text = $"Thanks for using it. Your downloads are still in:\n{outputDir}";
        if (failed.Count > 0)
        {
            FailedText.Text = "These were in use and couldn't be deleted. You can delete them yourself:\n" + string.Join("\n", failed);
            FailedText.Visibility = Visibility.Visible;
        }
        ConfirmPanel.Visibility = Visibility.Collapsed;
        DonePanel.Visibility = Visibility.Visible;
        CloseButton.Focus();
    }

    void Keep_Click(object sender, RoutedEventArgs e) => Close();

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void OpenDownloads_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(outputDir)) Process.Start("explorer.exe", $"\"{outputDir}\"");
    }
}
