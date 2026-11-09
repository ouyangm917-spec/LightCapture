using LightCapture.Core.Recording;

namespace LightCapture.Core.Tests;

public class RecordingTimelineTests
{
    [Fact]
    public void TrimmingKeepsPartialFirstAndLastFrameDurations()
    {
        CapturedFrame[] frames = [new("a", 0), new("b", 100), new("c", 200)];
        var slices = RecordingTimeline.Trim(frames, 300, 50, 250);
        Assert.Equal([50d, 100d, 50d], slices.Select(slice => slice.DurationMilliseconds));
        Assert.Equal([0, 1, 2], slices.Select(slice => slice.FrameIndex));
    }

    [Fact]
    public void TimestampGapsKeepWallClockTiming()
    {
        CapturedFrame[] frames = [new("a", 0), new("b", 80), new("c", 260)];
        Assert.Equal([80d, 180d, 40d], RecordingTimeline.Trim(frames, 300, 0, 300).Select(f => f.DurationMilliseconds));
        Assert.Equal(1, RecordingTimeline.FindFrame(frames, 259));
    }

    [Fact]
    public void FifteenFpsDoesNotAccumulateRoundingDrift()
    {
        var slices = Enumerable.Range(0, 150).Select(i => new FrameSlice(i, 1000d / 15)).ToArray();
        Assert.Equal(1000, RecordingTimeline.ToGifDelays(slices, 1).Sum(x => (int)x));
        Assert.Equal(500, RecordingTimeline.ToGifDelays(slices, 2).Sum(x => (int)x));
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(100, 100)]
    [InlineData(10, 301)]
    public void InvalidTrimIsRejected(double start, double end) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RecordingTimeline.Trim([new("a", 0)], 300, start, end));
}
