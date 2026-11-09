namespace LightCapture.Core.Recording;

public sealed record CapturedFrame(string Path, double TimestampMilliseconds);
public sealed record FrameSlice(int FrameIndex, double DurationMilliseconds);

public static class RecordingTimeline
{
    public static IReadOnlyList<FrameSlice> Trim(IReadOnlyList<CapturedFrame> frames, double duration, double start, double end)
    {
        if (frames.Count == 0) throw new ArgumentException("没有可导出的帧。", nameof(frames));
        if (!double.IsFinite(duration) || duration <= 0 || !double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end > duration || end <= start)
            throw new ArgumentOutOfRangeException(nameof(start), "修剪范围必须位于录制时长内，且终点晚于起点。");
        for (var i = 0; i < frames.Count; i++)
        {
            if (!double.IsFinite(frames[i].TimestampMilliseconds) || frames[i].TimestampMilliseconds < 0 ||
                (i > 0 && frames[i].TimestampMilliseconds < frames[i - 1].TimestampMilliseconds))
                throw new ArgumentException("帧时间戳必须非负且单调递增。", nameof(frames));
        }
        var slices = new List<FrameSlice>();
        for (var i = 0; i < frames.Count; i++)
        {
            var frameStart = i == 0 ? 0 : frames[i].TimestampMilliseconds;
            var frameEnd = i + 1 < frames.Count ? frames[i + 1].TimestampMilliseconds : duration;
            var overlap = Math.Min(end, frameEnd) - Math.Max(start, frameStart);
            if (overlap > 0) slices.Add(new(i, overlap));
        }
        if (slices.Count == 0) throw new ArgumentException("修剪区域没有有效帧。");
        return slices;
    }

    public static int FindFrame(IReadOnlyList<CapturedFrame> frames, double time)
    {
        if (frames.Count == 0) return -1;
        var low = 0;
        var high = frames.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (frames[middle].TimestampMilliseconds <= time) low = middle;
            else high = middle - 1;
        }
        return low;
    }

    public static ushort[] ToGifDelays(IReadOnlyList<FrameSlice> slices, double speed)
    {
        if (!double.IsFinite(speed) || speed < 0.25 || speed > 4) throw new ArgumentOutOfRangeException(nameof(speed));
        var delays = new ushort[slices.Count];
        double expected = 0;
        var emitted = 0;
        for (var i = 0; i < slices.Count; i++)
        {
            if (!double.IsFinite(slices[i].DurationMilliseconds) || slices[i].DurationMilliseconds <= 0)
                throw new ArgumentException("帧时长必须为正数。", nameof(slices));
            expected += slices[i].DurationMilliseconds / speed / 10;
            // 累积取整可避免 15 fps 全部取 70 ms 后产生持续的时长漂移。
            var ticks = Math.Max(2, (int)Math.Round(expected, MidpointRounding.AwayFromZero) - emitted);
            if (ticks > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(slices), "单帧时长超出 GIF 格式范围。");
            delays[i] = (ushort)ticks;
            emitted += ticks;
        }
        return delays;
    }
}
