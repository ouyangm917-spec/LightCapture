using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCapture.App.Services;
using LightCapture.App.UI;
using LightCapture.Core.Annotations;
using LightCapture.Core.Geometry;

namespace LightCapture.App.Diagnostics;

internal static class ScreenshotRevisionChecks
{
    internal static async Task RunAsync(string output, Func<string, Func<Task<string>>, Task> check)
    {
        await check("白色工具栏离屏布局与标注样式", () =>
        {
            var toolbar = new CaptureToolbar();
            toolbar.Update(AnnotationKind.Select, 0xFFED805D, 3, false, false, 1, false, false, false);
            var compact = RenderControl(toolbar, 1);
            Require(compact.PixelWidth is > 580 and < 650 && compact.PixelHeight == 58, "默认工具栏不是紧凑单排");
            foreach (var scale in new[] { 1d, 1.25, 1.5 })
            {
                var bitmap = RenderControl(toolbar, scale);
                File.WriteAllBytes(Path.Combine(output, $"toolbar-{scale * 100:0}.png"), BitmapIO.Png(bitmap));
                // WPF 切换渲染 DPI 时会重算布局舍入；保留一像素的取整容差。
                Require(Math.Abs(bitmap.PixelWidth - compact.PixelWidth * scale) <= 2, $"缩放 {scale} 后宽度 {bitmap.PixelWidth}，基准 {compact.PixelWidth}");
            }
            var preview = OnBackdrop(RenderControl(toolbar, 1.25));
            File.WriteAllBytes(Path.Combine(output, "toolbar-preview.png"), BitmapIO.Png(preview));
            toolbar.Update(AnnotationKind.Step, 0xFFED805D, 3, true, false, 3, true, false, false);
            var expanded = RenderControl(toolbar, 1.25);
            Require(expanded.PixelHeight > preview.PixelHeight, "步骤工具没有显示样式行");
            File.WriteAllBytes(Path.Combine(output, "toolbar-step-preview.png"), BitmapIO.Png(OnBackdrop(expanded)));
            toolbar.Update(AnnotationKind.Select, 0xFFED805D, 3, true, true, 3, false, true, false);
            toolbar.ShowStatus("正在离线翻译…", cancellable: true);
            var enabled = Descendants(toolbar).OfType<ButtonBase>().Where(button => button.Visibility == Visibility.Visible && button.IsEnabled).ToArray();
            Require(enabled.Length == 2, "翻译中应只保留取消截图和取消翻译");
            return Task.FromResult("真实 WPF 控件在 100% / 125% / 150% 离屏渲染；默认单排、步骤样式按需显示，忙碌时禁止编辑。没有创建窗口句柄。");
        });

        await check("译文渲染保留非文字像素、暗色背景及边界", () =>
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 480, 180));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(32, 42, 50)), null, new Rect(240, 0, 240, 180));
                dc.DrawRectangle(Brushes.Coral, null, new Rect(120, 130, 40, 30));
                dc.DrawText(AnnotationRenderer.Format("Save the image", 20, Brushes.Black), new Point(20, 35));
                dc.DrawText(AnnotationRenderer.Format("Cancel", 20, Brushes.White), new Point(260, 90));
            }
            var original = Bitmap(visual, 480, 180);
            OcrRegion[] regions = [new(new(19, 35, 205, 30), "Save the image"), new(new(259, 90, 120, 30), "Cancel"), new(new(-5, -5, 70, 20), "At the edge")];
            var translated = ImageTranslationRenderer.Render(original, regions, ["保存图片", "取消", "边缘文字"]);
            VerifyOutsidePixels(original, translated, regions);
            Require(DifferentPixels(original, translated) > 100, "译文没有绘制");
            File.WriteAllBytes(Path.Combine(output, "translation-layout-check.png"), BitmapIO.Png(translated));
            var document = new AnnotationDocument();
            document.Add(new Annotation { Kind = AnnotationKind.Step, Start = new(430, 140), End = new(430, 140) });
            var exported = new AnnotationRenderer(translated).Render(document.Items);
            Require(exported.PixelWidth == original.PixelWidth && exported.PixelHeight == original.PixelHeight, "标注导出改变图片尺寸");
            Require(Pixels(exported).AsSpan(0, 480 * 4 * 80).SequenceEqual(Pixels(translated).AsSpan(0, 480 * 4 * 80)), "导出未保留译文底图");
            File.WriteAllBytes(Path.Combine(output, "translation-with-step.png"), BitmapIO.Png(exported));
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            var cancelled = false;
            try { ImageTranslationRenderer.Render(original, regions, ["保存图片", "取消", "边缘文字"], cancellation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Require(cancelled, "取消后仍生成译图");
            return Task.FromResult("浅色/深色和越界文字框均可绘制；文字框外逐像素一致；译图叠加步骤后的 PNG 尺寸和底图一致。");
        });

        await check("截图 OCR 坐标到原位离线翻译完整管线", async () =>
        {
            var original = TranslationSample();
            File.WriteAllBytes(Path.Combine(output, "translation-before.png"), BitmapIO.Png(original));
            var regions = await OcrService.RecognizeRegionsAsync(BitmapIO.Png(original), CancellationToken.None);
            File.WriteAllText(Path.Combine(output, "translation-ocr.json"), System.Text.Json.JsonSerializer.Serialize(regions));
            Require(regions.Count == 4, $"预期 4 行，实际识别 {regions.Count} 行");
            Require(regions.All(region => region.Bounds.X >= 30 && region.Bounds.Right < 700), "OCR 位置错误");
            var phases = new List<string>();
            var translated = await ImageTranslationService.TranslateAsync(original, CancellationToken.None, new InlineProgress(phases.Add));
            Require(translated.IsFrozen, "后台译图未冻结，无法回到 UI 使用");
            Require(phases.Count == 2, "未按顺序报告识别和翻译阶段");
            VerifyOutsidePixels(original, translated, regions);
            Require(DifferentPixels(original, translated) > 1000, "管线没有替换原文像素");
            File.WriteAllBytes(Path.Combine(output, "translation-after.png"), BitmapIO.Png(translated));
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            var cancelled = false;
            try { await ImageTranslationService.TranslateAsync(original, cancellation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Require(cancelled, "取消后仍返回译图");
            return $"实际 OCR 的 {regions.Count} 行经附带离线模型回填；输出 1000×380，非文字区域不变；取消不提交译图。";
        });

        await check("离线批量翻译保持区域对应关系", async () =>
        {
            var results = await OfflineTranslator.TranslateBatchAsync(["Save the screenshot.", "Cancel.", "Open the file.", "Step 3: Copy the image."], CancellationToken.None);
            Require(results.Count == 4 && results.All(value => value.Any(c => c is >= '\u4e00' and <= '\u9fff')), "批量译文不完整");
            Require(results[0].Contains("保存") && results[1].Contains("取消") && results[2].Contains("文件"), "译文区域次序不正确");
            Require(results.All(value => !value.Contains('▁')), "译文残留分词标记");
            return string.Join(" / ", results);
        });
    }

    private static BitmapSource TranslationSample()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(247, 249, 248)), null, new Rect(0, 0, 1000, 380));
            dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(220, 229, 224)), 1), new Rect(15, 15, 970, 350), 10, 10);
            dc.DrawText(AnnotationRenderer.Format("Capture and translate", 42, Brushes.Black), new Point(36, 30));
            dc.DrawText(AnnotationRenderer.Format("Step 1: Select an area.", 28, Brushes.Black), new Point(36, 120));
            dc.DrawText(AnnotationRenderer.Format("Step 2: Click Translate.", 28, Brushes.Black), new Point(36, 190));
            dc.DrawText(AnnotationRenderer.Format("Step 3: Copy the image.", 28, Brushes.Black), new Point(36, 260));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(230, 245, 238)), null, new Rect(750, 70, 175, 210), 12, 12);
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(19, 168, 107)), 4), new Rect(791, 125, 92, 70));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(19, 168, 107)), null, new Rect(36, 334, 890, 3));
        }
        return Bitmap(visual, 1000, 380);
    }
    private static BitmapSource RenderControl(FrameworkElement element, double scale)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale), (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(element); bitmap.Freeze(); return bitmap;
    }
    private static BitmapSource OnBackdrop(BitmapSource toolbar)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(88, 113, 115)), null, new Rect(0, 0, toolbar.PixelWidth + 32, toolbar.PixelHeight + 8));
            dc.DrawImage(toolbar, new Rect(16, 4, toolbar.PixelWidth, toolbar.PixelHeight));
        }
        return Bitmap(visual, toolbar.PixelWidth + 32, toolbar.PixelHeight + 8);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static BitmapSource Bitmap(Visual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static byte[] Pixels(BitmapSource image)
    {
        var pixels = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[image.PixelWidth * image.PixelHeight * 4]; pixels.CopyPixels(bytes, image.PixelWidth * 4, 0); return bytes;
    }
    private static int DifferentPixels(BitmapSource before, BitmapSource after)
    {
        var first = Pixels(before); var second = Pixels(after); var count = 0;
        for (var i = 0; i < first.Length; i += 4) if (!first.AsSpan(i, 4).SequenceEqual(second.AsSpan(i, 4))) count++;
        return count;
    }
    private static void VerifyOutsidePixels(BitmapSource original, BitmapSource translated, IReadOnlyList<OcrRegion> regions)
    {
        Require(original.PixelWidth == translated.PixelWidth && original.PixelHeight == translated.PixelHeight, "译图尺寸改变");
        var before = Pixels(original); var after = Pixels(translated);
        for (var y = 0; y < original.PixelHeight; y++)
            for (var x = 0; x < original.PixelWidth; x++)
            {
                if (regions.Any(region => region.Bounds.Inflate(2).RoundOut().Contains(new(x + .5, y + .5)))) continue;
                var index = (y * original.PixelWidth + x) * 4;
                Require(before.AsSpan(index, 4).SequenceEqual(after.AsSpan(index, 4)), $"非文字像素 ({x},{y}) 被修改");
            }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class InlineProgress(Action<string> report) : IProgress<string> { public void Report(string value) => report(value); }
}
