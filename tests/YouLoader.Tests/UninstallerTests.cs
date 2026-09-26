using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class UninstallerTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), $"youloader-uninstall-{Guid.NewGuid():N}");
    readonly string settings, data, unpack, output, exe;

    public UninstallerTests()
    {
        settings = Dir("Roaming", "YouLoader");
        data = Dir("Local", "YouLoader");
        unpack = Dir("Temp", ".net", "YouLoader");
        output = Dir("Downloads", "YouLoader");
        exe = Path.Combine(root, "Apps", "YouLoader.exe");

        Write(Path.Combine(settings, "settings.json"), 100);
        Write(Path.Combine(data, "tools", "yt-dlp.exe"), 1000);
        Write(Path.Combine(unpack, "abc123", "native.dll"), 500);
        Write(exe, 2000);
        Write(Path.Combine(output, "Song.mp3"), 300);
        Write(Path.Combine(output, "My Playlist", "001 - Track.mp3"), 300);
        Write(Path.Combine(output, ".youloader-mp3.archive"), 10);
        File.SetAttributes(Path.Combine(output, ".youloader-mp3.archive"), FileAttributes.Hidden);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    string Dir(params string[] parts)
    {
        var dir = Path.Combine([root, .. parts]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void Write(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    UninstallPlan Plan() => Uninstaller.Plan(exe, output, [settings, data, unpack, Path.Combine(root, "missing")]);

    [Fact]
    public void PlanListsEverythingYouLoaderCreated_AndHowMuchSpaceItFrees()
    {
        var plan = Plan();

        Assert.Equal(exe, plan.ExePath);
        Assert.Equal([settings, data, unpack], plan.Folders);
        Assert.Equal([Path.Combine(output, ".youloader-mp3.archive")], plan.Files);
        Assert.Equal(100 + 1000 + 500 + 10 + 2000, plan.Bytes);
    }

    [Fact]
    public void RemovingKeepsDownloadedMusicAndVideos()
    {
        var failed = Uninstaller.RemoveData(Plan(), unpack);

        Assert.Empty(failed);
        Assert.False(Directory.Exists(settings));
        Assert.False(Directory.Exists(data));
        Assert.False(File.Exists(Path.Combine(output, ".youloader-mp3.archive")));
        Assert.True(File.Exists(Path.Combine(output, "Song.mp3")));
        Assert.True(File.Exists(Path.Combine(output, "My Playlist", "001 - Track.mp3")));
    }

    [Fact]
    public void TheRunningExeAndItsUnpackedFilesAreLeftForTheFinalStep()
    {
        Uninstaller.RemoveData(Plan(), unpack);

        Assert.True(File.Exists(exe));
        Assert.True(Directory.Exists(unpack));
    }

    [Fact]
    public void FinalStepWaitsThenDeletesTheExeAndUnpackedFiles_EvenWithSpacesInPaths()
    {
        var command = Uninstaller.FinalCleanupCommand(Plan(), unpack);

        Assert.Equal("cmd.exe", command.FileName);
        Assert.True(command.CreateNoWindow);
        Assert.StartsWith("/d /c ping 127.0.0.1 -n 4 >nul", command.Arguments);
        Assert.Contains($"del /f /q \"{exe}\"", command.Arguments);
        Assert.Contains($"rmdir /s /q \"{unpack}\"", command.Arguments);
    }

    [Fact]
    public void FinalStepRunsForReal()
    {
        var plan = Plan();
        Uninstaller.RemoveData(plan, unpack);

        using var process = System.Diagnostics.Process.Start(Uninstaller.FinalCleanupCommand(plan, unpack))!;
        process.WaitForExit(15_000);

        Assert.False(File.Exists(exe));
        Assert.False(Directory.Exists(unpack));
        Assert.True(File.Exists(Path.Combine(output, "Song.mp3")));
    }

    [Theory]
    [InlineData("dotnet.exe")]
    [InlineData("SomethingElse.exe")]
    public void NeverDeletesAProgramThatIsntYouLoader(string name)
    {
        var other = Path.Combine(root, name);
        Write(other, 10);

        var plan = Uninstaller.Plan(other, output, [settings]);

        Assert.Null(plan.ExePath);
        Assert.DoesNotContain(other, Uninstaller.FinalCleanupCommand(plan, unpack).Arguments);
    }

    [Fact]
    public void MissingSaveFolderIsFine()
    {
        var plan = Uninstaller.Plan(exe, Path.Combine(root, "nowhere"), [settings]);

        Assert.Empty(plan.Files);
    }
}
