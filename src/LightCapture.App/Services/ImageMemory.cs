using System.Windows;
using System.Windows.Threading;
using LightCapture.App.UI;

namespace LightCapture.App.Services;

/// <summary>图像工作全部结束后合并一次空闲回收，释放 WPF 原生位图；不裁剪进程工作集。</summary>
internal static class ImageMemory
{
    private static DispatcherTimer? _timer;
    private static int _operations;
    private static int _revision;
    private static bool _collecting;
    internal static bool IsCollecting => _collecting;
    internal static int CompletedCollections { get; private set; }

    internal static IDisposable BeginOperation()
    {
        Interlocked.Increment(ref _operations);
        return new Operation();
    }

    internal static void RequestCleanup()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted) return;
        if (!dispatcher.CheckAccess()) { dispatcher.BeginInvoke(RequestCleanup); return; }
        _revision++;
        if (_timer == null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, dispatcher) { Interval = TimeSpan.FromMilliseconds(750) };
            _timer.Tick += CollectWhenIdle;
        }
        _timer.Stop(); _timer.Start();
    }

    private static async void CollectWhenIdle(object? sender, EventArgs e)
    {
        _timer!.Stop();
        var app = Application.Current;
        if (_collecting || app == null || app.Dispatcher.HasShutdownStarted || Volatile.Read(ref _operations) != 0
            || app is App { HasImageWork: true }
            || app.Windows.Cast<Window>().Any(w => w is CaptureWindow or PinnedImageWindow or RecordingWindow or GifPreviewWindow)) return;
        var revision = _revision;
        _collecting = true;
        try
        {
            // 大像素缓冲在原生堆，关闭后托管分配很少，常规 GC 可能长时间不触发。
            // 等终结器及再次回收放在后台线程，避免在 UI 线程等待原生位图释放。
            await Task.Run(() =>
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
            });
            CompletedCollections++;
        }
        finally
        {
            _collecting = false;
            if (_revision != revision) RequestCleanup();
        }
    }

    private sealed class Operation : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Interlocked.Decrement(ref _operations);
            RequestCleanup();
        }
    }
}
