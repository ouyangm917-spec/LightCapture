using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LightCapture.App.Services;
using LightCapture.App.UI;
using LightCapture.Core.Annotations;
using LightCapture.Core.Geometry;

namespace LightCapture.App.Diagnostics;

/// <summary>合成图片内存对比；不创建 HWND、不显示窗口、不注册热键、不访问桌面或剪贴板。</summary>
internal static class MemoryProbe
{
    private sealed record Tracked(string Kind, WeakReference Reference);
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        var tracked = new List<Tracked>();
        var samples = new List<object>();
        async Task Sample(string stage)
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            samples.Add(new
            {
                Stage = stage, ProcessId = process.Id,
                WorkingSetMiB = Math.Round(process.WorkingSet64 / 1048576d, 2),
                PrivateCommitMiB = Math.Round(process.PrivateMemorySize64 / 1048576d, 2),
                ManagedMiB = Math.Round(GC.GetTotalMemory(false) / 1048576d, 2),
                MainWindow = Application.Current.MainWindow?.GetType().Name,
                WindowCount = Application.Current.Windows.Count,
                Alive = tracked.GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => g.Count(x => x.Reference.IsAlive))
            });
            await File.WriteAllTextAsync(Path.Combine(output, "memory.json"), JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
        }
        await Task.Delay(500);
        await Sample("initial");
        var pins = CreateBatch(tracked);
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        await Sample("captures_closed_three_pins_open");
        ClosePins(pins);
        await Task.Delay(1000);
        await Sample("all_closed_1s");
        await Task.Delay(4000);
        await Sample("all_closed_5s");
        // 双方都执行相同强制回收，区分 GC 延迟和仍被引用的对象；不用于冒充自然回落。
        await CollectAsync();
        await Sample("diagnostic_gc");
        Application.Current.MainWindow = null;
        await CollectAsync();
        await Sample("diagnostic_clear_main_window");
    }

    private static async Task CollectAsync()
    {
        await Task.Run(() => { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); });
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        await Task.Delay(250);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<PinnedImageWindow> CreateBatch(List<Tracked> tracked)
    {
        var pins = new List<PinnedImageWindow>();
        for (var i = 0; i < 3; i++)
        {
            using var bitmap = new System.Drawing.Bitmap(3840, 2160);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                graphics.Clear(i % 2 == 0 ? System.Drawing.Color.Teal : System.Drawing.Color.Goldenrod);
            var desktop = ScreenCaptureService.ToSource(bitmap);
            var capture = new CaptureWindow(desktop, new(0, 0, 3840, 2160), _ => { });
            var crop = BitmapIO.Crop(desktop, new(0, 0, 1920, 1080));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            // 仅搭建选区状态；不调用会显示工具栏或启动真实捕获的交互入口。
            typeof(CaptureWindow).GetField("_selection", flags)!.SetValue(capture, new PixelRect(0, 0, 1920, 1080));
            typeof(CaptureWindow).GetField("_originalSelection", flags)!.SetValue(capture, crop);
            typeof(CaptureWindow).GetField("_choosing", flags)!.SetValue(capture, false);
            typeof(CaptureWindow).GetMethod("SetImage", flags)!.Invoke(capture, [crop]);
            RenderOffscreen((FrameworkElement)capture.Content, 960, 540);
            var rendered = new AnnotationRenderer(crop).Render(Array.Empty<Annotation>());
            var pin = new PinnedImageWindow(rendered, new(0, 0, 1920, 1080));
            RenderOffscreen((FrameworkElement)pin.Content, 640, 360);
            pins.Add(pin);
            tracked.AddRange(new[] { new Tracked("capture", new(capture)), new("desktop", new(desktop)), new("crop", new(crop)), new("pin", new(pin)), new("pin_image", new(rendered)) });
            capture.Close();
        }
        return pins;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderOffscreen(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(element);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ClosePins(List<PinnedImageWindow> pins)
    {
        foreach (var pin in pins) pin.Close();
        pins.Clear();
    }
}
