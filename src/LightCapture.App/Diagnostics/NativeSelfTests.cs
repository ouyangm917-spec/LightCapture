using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCapture.App.Services;
using LightCapture.App.UI;
using LightCapture.Core.Annotations;
using LightCapture.Core.Geometry;
using LightCapture.Core.Imaging;
using LightCapture.Core.Recording;

namespace LightCapture.App.Diagnostics;

internal static class NativeSelfTests
{
    private sealed record Check(string Name, bool Passed, string Detail, long Milliseconds);
    internal static async Task<int> RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<Check>();
        async Task CheckAsync(string name, Func<Task<string>> test)
        {
            var watch = Stopwatch.StartNew();
            try { checks.Add(new(name, true, await test(), watch.ElapsedMilliseconds)); }
            catch (Exception e) { checks.Add(new(name, false, e.ToString(), watch.ElapsedMilliseconds)); }
        }
        await CheckAsync("GIF 原生编码、独立解码、帧时长和无限循环", () =>
        {
            var path = Path.Combine(output, "native-gif-check.gif");
            var colors = new[] { Colors.Red, Colors.Lime, Colors.Blue };
            using (var file = File.Create(path))
            using (var writer = new GifAnimationWriter(file, 64, 40))
                for (var i = 0; i < colors.Length; i++) writer.WriteFrame(GifExportService.EncodeFrame(Solid(colors[i], 64, 40)), (ushort)(10 + i * 5));
            using var input = File.OpenRead(path);
            var decoder = new GifBitmapDecoder(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Require(decoder.Frames.Count == 3, "帧数不是 3");
            for (var i = 0; i < colors.Length; i++)
            {
                var frame = new FormatConvertedBitmap(decoder.Frames[i], PixelFormats.Bgra32, null, 0);
                var pixel = new byte[4]; frame.CopyPixels(new Int32Rect(20, 20, 1, 1), pixel, 4, 0);
                Require(Math.Abs(pixel[0] - colors[i].B) < 8 && Math.Abs(pixel[1] - colors[i].G) < 8 && Math.Abs(pixel[2] - colors[i].R) < 8, $"第 {i + 1} 帧颜色错误");
                var metadata = decoder.Frames[i].Metadata as BitmapMetadata;
                Require(Convert.ToUInt16(metadata?.GetQuery("/grctlext/Delay")) == 10 + i * 5, $"第 {i + 1} 帧时长错误");
            }
            using var native = System.Drawing.Image.FromFile(path);
            var loop = native.GetPropertyItem(0x5101)?.Value;
            Require(loop is { Length: >= 2 } && BitConverter.ToUInt16(loop, 0) == 0, "原生解码器未读取到无限循环");
            return Task.FromResult("3 帧，64×40，100/150/200 ms，NETSCAPE 无限循环；WPF 与 GDI+ 均可读取。");
        });
        var ocrImage = TextSample();
        await File.WriteAllBytesAsync(Path.Combine(output, "ocr-sample.png"), BitmapIO.Png(ocrImage));
        await CheckAsync("本地英文 OCR", async () =>
        {
            var text = await OcrService.RecognizeAsync(BitmapIO.Png(ocrImage), CancellationToken.None);
            Require(text.Contains("Light", StringComparison.OrdinalIgnoreCase) && text.Contains("Step", StringComparison.OrdinalIgnoreCase), "未识别到样本文字");
            return text;
        });
        await CheckAsync("步骤标注导出尺寸", () =>
        {
            var document = new AnnotationDocument();
            document.Add(new Annotation { Kind = AnnotationKind.Step, Start = new(50, 50), End = new(50, 50) });
            document.Add(new Annotation { Kind = AnnotationKind.Step, Start = new(140, 70), End = new(140, 70) });
            var rendered = new AnnotationRenderer(Solid(Colors.White, 240, 140)).Render(document.Items);
            Require(rendered.PixelWidth == 240 && rendered.PixelHeight == 140, "导出尺寸错误");
            var pixel = new byte[4]; rendered.CopyPixels(new Int32Rect(50, 40, 1, 1), pixel, 4, 0);
            Require(pixel[1] < 240, "步骤标注未绘制");
            File.WriteAllBytes(Path.Combine(output, "steps-sample.png"), BitmapIO.Png(rendered));
            return Task.FromResult("240×140 PNG，包含编号 1、2。");
        });
        await CheckAsync("离线英译中模型实测", async () =>
        {
            var text = await OfflineTranslator.TranslateAsync("Click the Save button to save the screenshot.", CancellationToken.None);
            Require(text.Any(c => c is >= '\u4e00' and <= '\u9fff'), "未返回中文");
            return text;
        });
        await CheckAsync("合成帧录制、修剪缩放变速导出和缓存回收", async () =>
        {
            var cacheRoot = Path.Combine(output, "recording-cache");
            var count = 0;
            using var stop = new CancellationTokenSource();
            var recording = await RecordingSession.RecordAsync(new PixelRect(0, 0, 96, 64), cacheRoot, new Progress<TimeSpan>(), stop.Token, _ =>
            {
                var frame = new System.Drawing.Bitmap(96, 64);
                using var graphics = System.Drawing.Graphics.FromImage(frame);
                graphics.Clear(count % 2 == 0 ? System.Drawing.Color.OrangeRed : System.Drawing.Color.RoyalBlue);
                if (++count == 5) stop.Cancel();
                return frame;
            });
            try
            {
                Require(recording.Frames.Count == 5, "录制帧数量不正确");
                var destination = Path.Combine(output, "recording-pipeline-check.gif");
                var begin = recording.DurationMilliseconds * .2;
                var end = recording.DurationMilliseconds * .8;
                await GifExportService.ExportAsync(recording, destination, begin, end, 48, 2, new Progress<int>(), CancellationToken.None);
                using var file = File.OpenRead(destination);
                var decoder = new GifBitmapDecoder(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                Require(decoder.Frames.Count >= 2 && decoder.Frames[0].PixelWidth == 48 && decoder.Frames[0].PixelHeight == 32, "修剪或缩放结果错误");
                var totalDelay = decoder.Frames.Sum(frame => Convert.ToUInt16(((BitmapMetadata)frame.Metadata).GetQuery("/grctlext/Delay")) * 10);
                Require(Math.Abs(totalDelay - (end - begin) / 2) <= 25, "变速后的累计帧时长错误");
            }
            finally { recording.Dispose(); }
            Require(Directory.GetDirectories(cacheRoot).Length == 0, "会话目录未回收");
            return "5 张合成帧经实际录制管线落盘，截取中间 60%、48×32、2× 速度导出，缓存已回收；没有截取桌面。";
        });
        await CheckAsync("取消离线翻译后子进程退出", async () =>
        {
            using var cancel = new CancellationTokenSource();
            var processId = 0;
            var cancelled = false;
            try
            {
                await OfflineTranslator.TranslateAsync(string.Join(" ", Enumerable.Repeat("Click the Save button to save the screenshot.", 150)), cancel.Token, id => { processId = id; cancel.CancelAfter(80); });
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { cancelled = true; }
            Require(cancelled && processId > 0, "取消信号未正确传播");
            try
            {
                using var worker = Process.GetProcessById(processId);
                await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (ArgumentException) { }
            return "已启动的本次翻译子进程在取消后退出，未遗留推理任务。";
        });
        await ScreenshotRevisionChecks.RunAsync(output, CheckAsync);
        await CheckAsync("多张贴图独立保留与关闭", () =>
        {
            var first = new PinnedImageWindow(Solid(Colors.Teal, 240, 140), new(-500, 20, 240, 140));
            var second = new PinnedImageWindow(Solid(Colors.Gold, 320, 180), new(40, 60, 320, 180));
            try
            {
                Require(first.Topmost && second.Topmost && first.Owner == null && second.Owner == null, "贴图未独立置顶");
                Require(!first.ShowInTaskbar && !second.ShowInTaskbar, "贴图不应挤占任务栏");
                Require(first.Snapshot?.PixelWidth == 240 && second.Snapshot?.PixelWidth == 320, "贴图图像被覆盖");
                first.Close();
                Require(first.Snapshot == null && second.Snapshot?.PixelWidth == 320, "关闭第一张影响其他贴图或未释放图像");
                second.Close();
                Require(second.Snapshot == null, "第二张图像未释放");
            }
            finally { first.Close(); second.Close(); }
            return Task.FromResult("两张不同尺寸贴图独立置顶、不关联截图 Owner，关闭一张保留另一张并释放图片；未显示窗口或操作剪贴板。");
        });
        await UiRevisionChecks.RunAsync(output, CheckAsync);
        await CheckAsync("应用、窗口和托盘图标资源", () =>
        {
            using var stream = Application.GetResourceStream(AppIcon.ResourceUri)!.Stream;
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var sizes = new[] { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };
            Require(sizes.All(size => decoder.Frames.Any(frame => frame.PixelWidth == size && frame.PixelHeight == size)), "ICO 缺少所需尺寸");
            using var trayIcon = AppIcon.LoadTrayIcon();
            using var trayBitmap = trayIcon.ToBitmap();
            Require(trayBitmap.GetPixel(0, 0).A == 0, "托盘图标背景不透明");
            using var executableIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            Require(executableIcon != null, "EXE 缺少图标");
            using var executableBitmap = executableIcon!.ToBitmap();
            var center = executableBitmap.GetPixel(executableBitmap.Width / 2, executableBitmap.Height / 2);
            Require(center.G > center.R + 30 && center.B > center.R + 30, "EXE 未嵌入青绿色应用图标");
            var window = new Window();
            try
            {
                window.BeginInit(); window.EndInit();
                Require(window.Icon is BitmapFrame or BitmapImage, "窗口样式未加载应用图标");
            }
            finally { window.Close(); }
            return Task.FromResult("10 个 ICO 尺寸可解码，透明背景、托盘资源、EXE 内嵌图标和窗口样式加载通过；未创建托盘或显示窗口。");
        });
        await File.WriteAllTextAsync(Path.Combine(output, "native-checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return checks.All(c => c.Passed) ? 0 : 1;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static BitmapSource Solid(Color color, int width, int height)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, width, height));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static BitmapSource TextSample()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1000, 220));
            dc.DrawText(AnnotationRenderer.Format("Light Capture\nStep 1: Open the file.", 48, Brushes.Black), new Point(25, 20));
        }
        var bitmap = new RenderTargetBitmap(1000, 220, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
}
