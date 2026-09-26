using YouLoader.Core.Models;
using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class QueueOrderTests
{
    static DownloadItem Item(string id) =>
        new($"https://youtu.be/{id}", new DownloadRequest(OutputFormat.Mp3, "320", false, true, @"C:\Music"));

    [Fact]
    public void DownloadingComesFirst_ThenWaitingInStartOrder_ThenFinishedNewestFirst()
    {
        // Four links pasted at once; two slots, so the first two start and the rest wait.
        var a = Item("a"); Thread.Sleep(2);
        var b = Item("b"); Thread.Sleep(2);
        var c = Item("c"); Thread.Sleep(2);
        var d = Item("d");
        a.ReportStarting(); Thread.Sleep(2);
        b.ReportStarting();

        Assert.Equal([a, b, c, d], QueueOrder.Sort([d, c, b, a]));

        a.ReportSavedFile("a.mp3"); a.Complete(); Thread.Sleep(2);
        c.ReportStarting(); Thread.Sleep(2);
        b.ReportSavedFile("b.mp3"); b.Complete();

        Assert.Equal([c, d, b, a], QueueOrder.Sort([a, b, c, d]));
    }

    [Fact]
    public void ARetriedItemGoesToTheBackOfTheLine()
    {
        var a = Item("a"); Thread.Sleep(2);
        var b = Item("b");
        a.MarkCanceled(); Thread.Sleep(2);

        a.Reset();

        Assert.Equal([b, a], QueueOrder.Sort([a, b]));
    }

    [Fact]
    public void SummaryCountsDownloadingAndWaiting()
    {
        var a = Item("a");
        var b = Item("b");
        var c = Item("c");
        var done = Item("d");
        a.ReportStarting();
        b.ReportProcessing("Converting to MP3…");
        done.ReportSavedFile("d.mp3");
        done.Complete();

        Assert.Equal("2 downloading · 1 waiting", QueueOrder.Summary([a, b, c, done]));
        Assert.Equal("", QueueOrder.Summary([done]));
    }

    [Fact]
    public void WaitingItemsSayWhyTheyHaventStarted()
    {
        Assert.Equal("Waiting for a free slot…", Item("a").Status);
    }
}

public class LinkBoxTests
{
    const string Nl = "\r\n";

    [Fact]
    public void PastingAddsLinksOnePerLine()
    {
        var (text, added, duplicates) = LinkParser.MergeLinks("", "https://youtu.be/aaa https://youtu.be/bbb");

        Assert.Equal($"https://youtu.be/aaa{Environment.NewLine}https://youtu.be/bbb", text);
        Assert.Equal(2, added);
        Assert.Equal(0, duplicates);
    }

    [Fact]
    public void PastingALinkThatIsAlreadyThereAddsNothing_EvenWrittenDifferently()
    {
        var existing = "https://www.youtube.com/watch?v=geL8xtRYpmw&list=RDCmpSVfoJo-4&index=4";

        var (text, added, duplicates) = LinkParser.MergeLinks(existing, "https://youtu.be/geL8xtRYpmw?t=12");

        Assert.Equal(existing, text);
        Assert.Equal(0, added);
        Assert.Equal(1, duplicates);
    }

    [Fact]
    public void RepeatsInsideOnePasteAreSkippedToo()
    {
        var (text, added, duplicates) = LinkParser.MergeLinks("https://youtu.be/aaa", $"https://youtu.be/bbb{Nl}https://youtu.be/bbb{Nl}https://youtu.be/aaa");

        Assert.Equal($"https://youtu.be/aaa{Environment.NewLine}https://youtu.be/bbb", text);
        Assert.Equal(1, added);
        Assert.Equal(2, duplicates);
    }

    [Fact]
    public void IdenticalLinesCountAsDuplicates()
    {
        // The case from the screenshot: the same link pasted twice, character for character.
        var line = "https://www.youtube.com/watch?v=geL8xtRYpmw&list=RDCmpSVfoJo-4&index=4";

        Assert.Equal((2, 1), LinkParser.CountLinks(line + Nl + line));
    }

    [Fact]
    public void CountsLinksAndDuplicatesTypedByHand()
    {
        var text = $"https://youtu.be/aaa{Nl}https://youtu.be/bbb{Nl}https://www.youtube.com/watch?v=aaa";

        Assert.Equal((3, 1), LinkParser.CountLinks(text));
        Assert.Equal((0, 0), LinkParser.CountLinks("just some words"));
    }
}

public class QualityHintTests
{
    [Fact]
    public void EveryQualityOptionExplainsItself()
    {
        foreach (var format in Enum.GetValues<OutputFormat>())
            Assert.All(QualityPresets.For(format), p => Assert.True(p.Hint.Length > 20, $"{p.Label} needs a hint"));
    }

    [Fact]
    public void Mp3HintsExplainKbpsAndVbr()
    {
        Assert.Contains("kbps is", QualityPresets.Find(OutputFormat.Mp3, "320").Hint);
        Assert.Contains("VBR (variable bitrate)", QualityPresets.Find(OutputFormat.Mp3, "v0").Hint);
    }
}
