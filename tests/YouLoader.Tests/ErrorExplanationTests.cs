using YouLoader.Core.Models;
using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class ErrorExplanationTests
{
    static ErrorExplanation Explain(params string[] log) => ErrorMessages.Explain(log, exitCode: 1);

    [Fact]
    public void ConversionFailure_UsesFfmpegsRealReason()
    {
        // Captured from a real failure: ffmpeg explains the problem before yt-dlp reports a bare "Conversion failed!".
        var explanation = Explain(
            "[debug] ffmpeg command line: ffmpeg -y -i \"file:Me at the zoo.opus\" -map 0 -c copy \"file:Me at the zoo.temp.opus\"",
            "  Stream #0:1 -> #0:1 (copy)",
            "[opus @ 00000187ae419c80] Unsupported codec id in stream 1",
            "[out#0/opus @ 00000187ac5682c0] Could not write header (incorrect codec parameters ?): Invalid argument",
            "Conversion failed!",
            "ERROR: Postprocessing: Conversion failed!");

        Assert.Equal("The file couldn't be converted to this format.", explanation.Summary);
        Assert.Contains("cover art", explanation.Cause);
        Assert.Contains(explanation.Suggestions, s => s.Contains("Embed cover art"));
    }

    [Fact]
    public void ConversionFailure_WithoutAReason_StillExplainsTheLikelyCauses()
    {
        var explanation = Explain("Conversion failed!", "ERROR: Postprocessing: Conversion failed!");

        Assert.Equal("Converting the file failed.", explanation.Summary);
        Assert.Contains("damaged download", explanation.Cause);
    }

    [Fact]
    public void FileOpenInAnotherProgram()
    {
        // Captured with the target file open in another program.
        var explanation = Explain(
            "ERROR: Unable to download video: [WinError 32] The process cannot access the file because it is being used by another process: 'lock\\\\Me at the zoo.mp3'");

        Assert.Equal("The file is open in another program.", explanation.Summary);
        Assert.Contains(explanation.Suggestions, s => s.Contains("Close the program"));
    }

    [Fact]
    public void DamagedDownloadIsNotBlamedOnTheFormat()
    {
        var explanation = Explain(
            "[in#0 @ 000001] Error opening input: Invalid data found when processing input",
            "ERROR: Postprocessing: Conversion failed!");

        Assert.Equal("The download arrived damaged.", explanation.Summary);
    }

    [Theory]
    [InlineData("ERROR: [youtube] abcdefghijk: Private video. Sign in if you've been granted access", "This video is private.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Video unavailable", "This video is unavailable.")]
    [InlineData("ERROR: [youtube] xxxxxxxxxxx: This video is unavailable", "This video is unavailable.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Sign in to confirm you’re not a bot", "YouTube asked for a bot check.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Sign in to confirm your age", "This video is age-restricted.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Join this channel to get access to members-only content", "This video is for channel members only.")]
    [InlineData("ERROR: [youtube] abcdefghijk: The uploader has not made this video available in your country", "This video is blocked in your country.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Premieres in 3 hours", "This video hasn't started yet.")]
    [InlineData("ERROR: unable to download video data: HTTP Error 403: Forbidden", "YouTube refused the download.")]
    [InlineData("ERROR: unable to download video data: HTTP Error 429: Too Many Requests", "YouTube is temporarily limiting your connection.")]
    [InlineData("ERROR: Unsupported URL: https://example.com", "YouLoader can't download from this link.")]
    [InlineData("ERROR: [Errno 28] No space left on device", "Your disk is full.")]
    [InlineData("ERROR: [WinError 5] Access is denied: 'C:\\\\Program Files\\\\song.mp3'", "YouLoader isn't allowed to save in this folder.")]
    [InlineData("ERROR: [WinError 206] The filename or extension is too long", "The file name or folder path is too long for Windows.")]
    [InlineData("ERROR: Postprocessing: ffprobe and ffmpeg not found. Please install or provide the path", "The converter (ffmpeg) is missing.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Requested format is not available", "This quality isn't available for this video.")]
    [InlineData("ERROR: Unable to download webpage: <urlopen error [Errno 11001] getaddrinfo failed>", "Couldn't connect.")]
    public void KnownProblemsGetSpecificExplanations(string line, string summary)
    {
        var explanation = Explain(line);

        Assert.Equal(summary, explanation.Summary);
        Assert.False(string.IsNullOrWhiteSpace(explanation.Cause));
        Assert.NotEmpty(explanation.Suggestions);
    }

    [Fact]
    public void DebugLinesDoNotCauseFalseMatches()
    {
        var explanation = Explain(
            "[debug] Command-line config: ['--output', 'Permission denied - song.%(ext)s']",
            "ERROR: [youtube] abcdefghijk: Video unavailable");

        Assert.Equal("This video is unavailable.", explanation.Summary);
    }

    [Fact]
    public void UnknownErrorShowsWhatTheEngineSaidWithoutThePrefix()
    {
        var explanation = Explain("ERROR: [youtube] abcdefghijk: Something nobody has seen before");

        Assert.Equal("Something went wrong.", explanation.Summary);
        Assert.Contains("Something nobody has seen before", explanation.Cause);
        Assert.DoesNotContain("ERROR:", explanation.Cause);
    }

    [Fact]
    public void NoErrorLineAtAllMentionsTheExitCode()
    {
        var explanation = ErrorMessages.Explain([], exitCode: 2);

        Assert.Contains("exit code 2", explanation.Cause);
    }

    [Fact]
    public void EngineThatWontStartPointsAtAntivirus()
    {
        var explanation = ErrorMessages.EngineDidNotStart("The system cannot find the file specified.");

        Assert.Equal("The download engine couldn't start.", explanation.Summary);
        Assert.Contains("antivirus", explanation.Cause);
        Assert.Contains("The system cannot find the file specified.", explanation.Cause);
    }
}

public class TechnicalLogTests
{
    const string Home = @"C:\Users\Alex";

    [Theory]
    [InlineData(@"Saving to C:\Users\Alex\Downloads\song.opus")]
    [InlineData(@"'C:\\Users\\Alex\\Downloads\\song.opus'")]
    [InlineData("C:/Users/Alex/Downloads/song.opus")]
    [InlineData(@"c:\users\alex\Downloads\song.opus")]
    public void HidesTheHomeFolderHoweverItIsWritten(string line)
    {
        var text = TechnicalLog.HideHomeFolder(line, Home);

        Assert.DoesNotContain("Alex", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", text);
    }

    [Fact]
    public void FormatKeepsOnlyTheLastFortyLines()
    {
        var log = Enumerable.Range(1, 100).Select(i => $"line {i}").ToList();

        var text = TechnicalLog.Format(log, Home);

        Assert.StartsWith("line 61", text);
        Assert.EndsWith("line 100", text);
    }

    [Fact]
    public void FormatShortensVeryLongLines()
    {
        var text = TechnicalLog.Format([new string('x', 5000)], Home);

        Assert.True(text.Length < 500);
        Assert.EndsWith("…", text);
    }

    [Fact]
    public void KeepsAtMost120Lines()
    {
        var log = new TechnicalLog();
        for (var i = 0; i < 500; i++) log.Add($"line {i}");

        Assert.Equal(120, log.Lines.Count);
        Assert.Equal("line 499", log.Lines[^1]);
    }
}

public class FailedItemTests
{
    static DownloadItem NewItem() =>
        new("https://youtu.be/x", new DownloadRequest(OutputFormat.Opus, "original", false, true, @"C:\Music"));

    [Fact]
    public void FailingShowsTheSummaryAndKeepsTheExplanation()
    {
        var item = NewItem();
        var explanation = new ErrorExplanation("The file is open in another program.", "Because.", ["Close it."]);

        item.Fail(explanation, "some log");

        Assert.Equal(DownloadState.Failed, item.State);
        Assert.Equal("The file is open in another program.", item.Status);
        Assert.Same(explanation, item.Error);
        Assert.True(item.HasExplanation);
        Assert.True(item.HasTechnicalDetails);
        Assert.False(item.ShowExplanation);
    }

    [Fact]
    public void RetryClearsTheOldExplanation()
    {
        var item = NewItem();
        item.Fail(new ErrorExplanation("x", "y", ["z"]), "log");
        item.ShowExplanation = true;
        item.ShowTechnicalDetails = true;

        item.Reset();

        Assert.Null(item.Error);
        Assert.False(item.HasExplanation);
        Assert.False(item.HasTechnicalDetails);
        Assert.False(item.ShowExplanation);
        Assert.False(item.ShowTechnicalDetails);
    }

    [Fact]
    public void EmptyLogMeansNoTechnicalDetailsButton()
    {
        var item = NewItem();

        item.Fail(new ErrorExplanation("x", "y", ["z"]), "   ");

        Assert.False(item.HasTechnicalDetails);
    }
}
