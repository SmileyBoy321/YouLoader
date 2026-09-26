using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace YouLoader.Core.Services;

/// <summary>
/// Finds yt-dlp, ffmpeg and a JavaScript runtime, and downloads whichever are missing.
/// YouLoader always runs its own copy of yt-dlp so it can keep it updated: YouTube changes
/// often and an outdated yt-dlp is the number one reason downloads break.
/// </summary>
public sealed class ToolManager
{
    public const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    public const string FfmpegUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
    public const string DenoUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";

    static readonly HttpClient SharedHttp = CreateHttpClient();

    readonly HttpClient http;

    public ToolManager(string? toolsDir = null, HttpClient? http = null)
    {
        ToolsDir = toolsDir ?? DefaultToolsDir;
        this.http = http ?? SharedHttp;
    }

    public static string DefaultToolsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YouLoader", "tools");

    public string ToolsDir { get; }
    public string YtDlpPath => Path.Combine(ToolsDir, "yt-dlp.exe");
    public string FfmpegDir { get; private set; } = "";

    /// <summary>Value for yt-dlp's --js-runtimes, e.g. "node:C:\...\node.exe". YouTube needs one to unlock all formats.</summary>
    public string? JsRuntime { get; private set; }

    public string JsRuntimeName => JsRuntime?.Split(':', 2)[0] ?? "none";

    public async Task EnsureAsync(IProgress<string>? status = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ToolsDir);

        if (!File.Exists(YtDlpPath))
            await DownloadFileAsync(YtDlpUrl, YtDlpPath, "yt-dlp", status, ct);

        FfmpegDir = FindFfmpegDir() ?? "";
        if (FfmpegDir.Length == 0)
        {
            await DownloadAndExtractAsync(FfmpegUrl, ["ffmpeg.exe", "ffprobe.exe"], "ffmpeg", status, ct);
            FfmpegDir = ToolsDir;
        }

        JsRuntime = FindJsRuntime();
        if (JsRuntime is null)
        {
            await DownloadAndExtractAsync(DenoUrl, ["deno.exe"], "Deno", status, ct);
            JsRuntime = "deno:" + Path.Combine(ToolsDir, "deno.exe");
        }
    }

    public string? FindFfmpegDir() =>
        SearchDirs().FirstOrDefault(dir =>
            File.Exists(Path.Combine(dir, "ffmpeg.exe")) && File.Exists(Path.Combine(dir, "ffprobe.exe")));

    public string? FindJsRuntime() =>
        Find("deno.exe") is { } deno ? "deno:" + deno
        : Find("node.exe") is { } node ? "node:" + node
        : null;

    public async Task<string> GetYtDlpVersionAsync(CancellationToken ct = default) =>
        (await RunYtDlpAsync(["--version"], ct)).Trim();

    /// <summary>Runs yt-dlp's self-updater and returns its final status line.</summary>
    public async Task<string> UpdateYtDlpAsync(CancellationToken ct = default)
    {
        var output = await RunYtDlpAsync(["--update"], ct);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
    }

    string? Find(string exe) => SearchDirs().Select(dir => Path.Combine(dir, exe)).FirstOrDefault(File.Exists);

    IEnumerable<string> SearchDirs()
    {
        yield return ToolsDir;
        yield return AppContext.BaseDirectory;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return dir.Trim('"');
    }

    async Task<string> RunYtDlpAsync(string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(YtDlpPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Couldn't start yt-dlp.");
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return await stdout + await stderr;
    }

    async Task DownloadFileAsync(string url, string target, string name, IProgress<string>? status, CancellationToken ct)
    {
        var temp = target + ".download";
        using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(temp);

            var buffer = new byte[81920];
            long done = 0;
            long lastReported = -1;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;

                // Report whole percents when the size is known, otherwise whole megabytes.
                var marker = total > 0 ? done * 100 / total.Value : done / 1_048_576;
                if (marker == lastReported) continue;
                lastReported = marker;
                status?.Report(total > 0
                    ? $"Downloading {name}… {marker}%"
                    : $"Downloading {name}… {marker} MB");
            }
        }
        File.Move(temp, target, overwrite: true);
    }

    async Task DownloadAndExtractAsync(string url, string[] files, string name, IProgress<string>? status, CancellationToken ct)
    {
        var zipPath = Path.Combine(ToolsDir, $"{Guid.NewGuid():N}.zip");
        try
        {
            await DownloadFileAsync(url, zipPath, name, status, ct);
            status?.Report($"Unpacking {name}…");
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var fileName in files)
            {
                var entry = zip.Entries.FirstOrDefault(e => e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"{fileName} is missing from the {name} download.");
                entry.ExtractToFile(Path.Combine(ToolsDir, fileName), overwrite: true);
            }
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("YouLoader (+https://github.com/SmileyBoy321/YouLoader)");
        return client;
    }
}
