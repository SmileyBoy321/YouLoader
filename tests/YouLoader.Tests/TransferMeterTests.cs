using YouLoader.Core.Services;

namespace YouLoader.Tests;

public class TransferMeterTests
{
    static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    const long MB = 1024 * 1024;

    /// <summary>
    /// Replays what yt-dlp does with 4 parallel chunks: nothing arrives for a while, then 2 MB at once.
    /// The true average is 4 MB/s, but yt-dlp's own instant speed swings between 0 and 20 MB/s.
    /// </summary>
    static List<(TimeSpan At, TransferReading Reading)> ReplayBurstyDownload(long total, int seconds)
    {
        var meter = new TransferMeter();
        var readings = new List<(TimeSpan, TransferReading)>();
        long downloaded = 0;
        for (var tick = 0; tick <= seconds * 10; tick++)
        {
            var at = TimeSpan.FromMilliseconds(tick * 100);
            if (tick > 0 && tick % 5 == 0) downloaded += 2 * MB;
            if (meter.Update(Start + at, downloaded, total) is { } reading) readings.Add((at, reading));
        }
        return readings;
    }

    [Fact]
    public void SpeedSettlesNearTheRealAverageDespiteBursts()
    {
        var readings = ReplayBurstyDownload(total: 200 * MB, seconds: 20);

        foreach (var (at, reading) in readings.Where(r => r.At >= TimeSpan.FromSeconds(6)))
        {
            Assert.NotNull(reading.BytesPerSecond);
            Assert.InRange(reading.BytesPerSecond!.Value / MB, 3.4, 4.6);
        }
    }

    [Fact]
    public void TimeLeftCountsDownSmoothlyInsteadOfJumping()
    {
        var readings = ReplayBurstyDownload(total: 200 * MB, seconds: 20);
        var steady = readings.Where(r => r.At >= TimeSpan.FromSeconds(6)).Select(r => r.Reading.TimeLeft!.Value.TotalSeconds).ToList();

        // One second passes between readings, so time left should drop by about one second, never leap.
        for (var i = 1; i < steady.Count; i++)
            Assert.InRange(steady[i - 1] - steady[i], -2, 4);
    }

    [Fact]
    public void RefreshesAtMostOncePerSecond()
    {
        var readings = ReplayBurstyDownload(total: 200 * MB, seconds: 10);

        Assert.InRange(readings.Count, 10, 11);
        for (var i = 1; i < readings.Count; i++)
            Assert.True(readings[i].At - readings[i - 1].At >= TransferMeter.DisplayInterval);
    }

    [Fact]
    public void NoSpeedOrTimeLeftUntilThereIsASecondOfRealData()
    {
        // Regression: yt-dlp's first reading after 1 KB claimed "128 KB/s · 19:10 left" on a 143 MB file.
        var meter = new TransferMeter();

        var first = meter.Update(Start, 1024, 143 * MB);
        var early = meter.Update(Start.AddSeconds(0.5), 4 * MB, 143 * MB);
        var afterOneSecond = meter.Update(Start.AddSeconds(1), 5 * MB, 143 * MB);

        Assert.NotNull(first);
        Assert.Null(first.BytesPerSecond);
        Assert.Null(first.TimeLeft);
        Assert.Null(early);
        Assert.NotNull(afterOneSecond!.BytesPerSecond);
        Assert.InRange(afterOneSecond.TimeLeft!.Value.TotalSeconds, 25, 35);
    }

    [Fact]
    public void NewFileStartsFresh()
    {
        var meter = new TransferMeter();
        meter.Update(Start, 0, 100 * MB);
        meter.Update(Start.AddSeconds(1), 50 * MB, 100 * MB);

        // The audio stream starts after the video: bytes drop back to zero, and the video's speed must not carry over.
        var reading = meter.Update(Start.AddSeconds(1.1), 0, 4 * MB);

        Assert.NotNull(reading);
        Assert.Null(reading.BytesPerSecond);
    }

    [Fact]
    public void UnknownTotalGivesSpeedButNoTimeLeft()
    {
        var meter = new TransferMeter();
        meter.Update(Start, 0, null);

        var reading = meter.Update(Start.AddSeconds(2), 4 * MB, null);

        Assert.Equal(2.0 * MB, reading!.BytesPerSecond!.Value, 1);
        Assert.Null(reading.TimeLeft);
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KB")]
    [InlineData(31_562_137, "30.1 MB")]
    [InlineData(3_221_225_472, "3.00 GB")]
    public void FormatsSizes(double bytes, string expected)
    {
        Assert.Equal(expected, TransferMeter.FormatBytes(bytes));
    }

    [Theory]
    [InlineData(12, "0:12")]
    [InlineData(185, "3:05")]
    [InlineData(3723, "1:02:03")]
    public void FormatsTimeLeft(int seconds, string expected)
    {
        Assert.Equal(expected, TransferMeter.FormatTimeLeft(TimeSpan.FromSeconds(seconds)));
    }
}
