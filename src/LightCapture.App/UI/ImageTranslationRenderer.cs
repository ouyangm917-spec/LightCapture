using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCapture.App.Services;
using LightCapture.Core.Geometry;

namespace LightCapture.App.UI;

internal static class ImageTranslationRenderer
{
    internal static BitmapSource Render(BitmapSource original, IReadOnlyList<OcrRegion> regions, IReadOnlyList<string> translations)
    {
        if (regions.Count != translations.Count || translations.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("译文与文字区域不匹配。");
        var bounds = new PixelRect(0, 0, original.PixelWidth, original.PixelHeight);
        var pixels = new FormatConvertedBitmap(original, PixelFormats.Bgra32, null, 0);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(original, BitmapIO.Rect(bounds));
            for (var i = 0; i < regions.Count; i++)
            {
                var box = regions[i].Bounds.Inflate(2).RoundOut().Intersect(bounds);
                if (box.IsEmpty) continue;
                var background = BackgroundColor(pixels, box);
                var luminance = .2126 * background.R + .7152 * background.G + .0722 * background.B;
                var foreground = new SolidColorBrush(luminance >= 145 ? Color.FromRgb(39, 49, 55) : Color.FromRgb(244, 247, 249));
                var text = FitText(translations[i], box, foreground);
                dc.PushClip(new RectangleGeometry(BitmapIO.Rect(box)));
                dc.DrawRectangle(new SolidColorBrush(background), null, BitmapIO.Rect(box));
                dc.DrawText(text, new Point(box.X + 1, box.Y + Math.Max(0, (box.Height - text.Height) / 2)));
                dc.Pop();
            }
        }
        var output = new RenderTargetBitmap(original.PixelWidth, original.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        output.Render(visual); output.Freeze(); return output;
    }

    private static FormattedText FitText(string value, PixelRect box, Brush foreground)
    {
        var width = Math.Max(1, box.Width - 2);
        var height = Math.Max(1, box.Height - 1);
        FormattedText Measure(double size)
        {
            var text = AnnotationRenderer.Format(value, size, foreground);
            text.MaxTextWidth = width;
            return text;
        }
        // 不设 MaxTextHeight/省略号，按实际换行高度缩放，避免静默截掉译文。
        var low = .1; var high = Math.Max(1, box.Height);
        for (var attempt = 0; attempt < 14; attempt++)
        {
            var middle = (low + high) / 2;
            var measured = Measure(middle);
            if (measured.Height <= height && measured.WidthIncludingTrailingWhitespace <= width) low = middle; else high = middle;
        }
        return Measure(low);
    }

    private static Color BackgroundColor(BitmapSource pixels, PixelRect box)
    {
        var width = (int)box.Width; var height = (int)box.Height;
        var stride = width * 4;
        var sample = new byte[stride * height];
        pixels.CopyPixels(new Int32Rect((int)box.X, (int)box.Y, width, height), sample, stride, 0);
        var buckets = new Dictionary<int, (int Count, long R, long G, long B)>();
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (x >= 2 && x < width - 2 && y >= 2 && y < height - 2) continue;
                var offset = y * stride + x * 4;
                var b = sample[offset]; var g = sample[offset + 1]; var r = sample[offset + 2];
                var key = (r >> 4) << 8 | (g >> 4) << 4 | b >> 4;
                buckets.TryGetValue(key, out var bucket);
                buckets[key] = (bucket.Count + 1, bucket.R + r, bucket.G + g, bucket.B + b);
            }
        var dominant = buckets.Values.MaxBy(bucket => bucket.Count);
        return Color.FromRgb((byte)(dominant.R / dominant.Count), (byte)(dominant.G / dominant.Count), (byte)(dominant.B / dominant.Count));
    }
}
