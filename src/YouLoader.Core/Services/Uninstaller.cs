using System.Diagnostics;

namespace YouLoader.Core.Services;

/// <summary>Everything YouLoader put on this PC, and what removing it frees.</summary>
/// <param name="ExePath">The running YouLoader.exe, deleted last, after the app has closed.</param>
/// <param name="Folders">Settings, helper tools and the files the single-file exe unpacks to %TEMP%.</param>
/// <param name="Files">Hidden playlist-history files in the save folder.</param>
public sealed record UninstallPlan(string? ExePath, IReadOnlyList<string> Folders, IReadOnlyList<string> Files, long Bytes);

/// <summary>
/// Removes YouLoader and everything it created. It never touches downloaded music or videos,
/// and there's nothing in the registry to clean: YouLoader never writes there.
/// </summary>
public static class Uninstaller
{
    public static string SettingsDir => Path.GetDirectoryName(AppSettings.DefaultPath)!;
    public static string DataDir => Path.GetDirectoryName(ToolManager.DefaultToolsDir)!;

    /// <summary>Where the single-file exe unpacks its native libraries (one subfolder per version).</summary>
    public static string UnpackDir => Path.Combine(Path.GetTempPath(), ".net", "YouLoader");

    public static UninstallPlan Plan(string? exePath, string outputDir) =>
        Plan(exePath, outputDir, [SettingsDir, DataDir, UnpackDir]);

    public static UninstallPlan Plan(string? exePath, string outputDir, IEnumerable<string> folders)
    {
        var exe = exePath is not null
                  && Path.GetFileName(exePath).Equals("YouLoader.exe", StringComparison.OrdinalIgnoreCase)
                  && File.Exists(exePath)
            ? exePath
            : null;
        var existingFolders = folders.Where(Directory.Exists).ToList();
        var archives = Directory.Exists(outputDir)
            ? Directory.GetFiles(outputDir, ".youloader-*.archive").ToList()
            : [];

        var bytes = existingFolders.Sum(FolderSize)
                    + archives.Sum(f => new FileInfo(f).Length)
                    + (exe is null ? 0 : new FileInfo(exe).Length);
        return new UninstallPlan(exe, existingFolders, archives, bytes);
    }

    /// <summary>
    /// Deletes everything except the running exe and the unpack folder it's still using; those go
    /// in <see cref="StartFinalCleanup"/> once the app has exited. Returns anything that couldn't be removed.
    /// </summary>
    public static List<string> RemoveData(UninstallPlan plan, string? unpackDir = null)
    {
        unpackDir ??= UnpackDir;
        List<string> failed = [];
        foreach (var folder in plan.Folders.Where(f => !SamePath(f, unpackDir)))
            Try(() => Directory.Delete(folder, recursive: true), folder, failed);
        foreach (var file in plan.Files)
            Try(() =>
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }, file, failed);
        return failed;
    }

    /// <summary>
    /// Starts a hidden command that waits for YouLoader to close, then deletes the exe and the
    /// unpacked files, since a running program can't delete itself. Call right before exiting.
    /// </summary>
    public static void StartFinalCleanup(UninstallPlan plan, string? unpackDir = null) =>
        Process.Start(FinalCleanupCommand(plan, unpackDir ?? UnpackDir));

    public static ProcessStartInfo FinalCleanupCommand(UninstallPlan plan, string unpackDir)
    {
        // ping is the classic way to wait a couple of seconds in cmd without a console window.
        var steps = new List<string> { "ping 127.0.0.1 -n 4 >nul" };
        if (plan.ExePath is not null) steps.Add($"del /f /q \"{plan.ExePath}\"");
        if (plan.Folders.Any(f => SamePath(f, unpackDir))) steps.Add($"rmdir /s /q \"{unpackDir}\"");

        return new ProcessStartInfo("cmd.exe", "/d /c " + string.Join(" & ", steps))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
    }

    public static string FormatSize(long bytes) => TransferMeter.FormatBytes(bytes);

    /// <summary>Size of a file or a whole folder, or 0 when it isn't there.</summary>
    public static long SizeOf(string path) =>
        File.Exists(path) ? new FileInfo(path).Length : Directory.Exists(path) ? FolderSize(path) : 0;

    static long FolderSize(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    static void Try(Action delete, string path, List<string> failed)
    {
        try
        {
            delete();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            failed.Add(path);
        }
    }

    static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}
