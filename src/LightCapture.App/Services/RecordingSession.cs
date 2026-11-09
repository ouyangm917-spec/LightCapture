using System.Diagnostics;
using System.Drawing.Imaging;
using LightCapture.Core.Geometry;
using LightCapture.Core.Recording;

namespace LightCapture.App.Services;

internal sealed class RecordingSession : IDisposable
{
    private readonly string _folder;
    private readonly List<CapturedFrame> _frames = [];
    internal IReadOnlyList<CapturedFrame> Frames => _frames;
    internal double DurationMilliseconds { get; private set; }
    internal string StopReason { get; private set; } = "";
    internal int Width { get; private set; }
    internal int Height { get; private set; }
    private RecordingSession(string cacheRoot)
    {
        _folder = Path.Combine(Path.GetFullPath(cacheRoot), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    internal static Task<RecordingSession> RecordAsync(PixelRect region, string cacheRoot, IProgress<TimeSpan> progress, CancellationToken stop, Func<PixelRect, System.Drawing.Bitmap>? capture = null)
        => Task.Run(async () =>
        {
            var session = new RecordingSession(cacheRoot);
            var watch = Stopwatch.StartNew();
            long bytes = 0;
            try
            {
                while (!stop.IsCancellationRequested && watch.Elapsed.TotalSeconds < 60)
                {
                    var timestamp = watch.Elapsed.TotalMilliseconds;
                    var path = Path.Combine(session._folder, $"{session._frames.Count:D5}.png");
                    using (var bitmap = capture != null ? capture(region) : ScreenCaptureService.Capture(region, cursor: true, maxWidth: 1280))
                    {
                        session.Width = bitmap.Width; session.Height = bitmap.Height;
                        bitmap.Save(path, ImageFormat.Png);
                    }
                    session._frames.Add(new(path, session._frames.Count == 0 ? 0 : timestamp));
                    bytes += new FileInfo(path).Length;
                    progress.Report(watch.Elapsed);
                    if (bytes >= 512L * 1024 * 1024) { session.StopReason = "已达到 512 MB 录制缓存上限"; break; }
                    var wait = 1000.0 / 15 - (watch.Elapsed.TotalMilliseconds - timestamp);
                    if (wait > 0)
                    {
                        try { await Task.Delay(TimeSpan.FromMilliseconds(wait), stop); }
                        catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                    }
                }
                session.DurationMilliseconds = Math.Clamp(watch.Elapsed.TotalMilliseconds, 100, 60_000);
                if (watch.Elapsed.TotalSeconds >= 60) session.StopReason = "已达到每段 60 秒的录制上限";
                if (session._frames.Count == 0) throw new OperationCanceledException("录制已取消。");
                return session;
            }
            catch { session.Dispose(); throw; }
        });

    public void Dispose()
    {
        // 只删除本次随机生成的会话目录，不扫描或递归清理用户目录。
        if (!Guid.TryParseExact(Path.GetFileName(_folder), "N", out _)) return;
        try { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
