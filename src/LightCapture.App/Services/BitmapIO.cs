using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCapture.Core.Geometry;

namespace LightCapture.App.Services;

internal static class BitmapIO
{
    internal static BitmapSource Crop(BitmapSource source, PixelRect rect)
    {
        var r = rect.RoundOut().Intersect(new(0, 0, source.PixelWidth, source.PixelHeight));
        if (r.IsEmpty) throw new ArgumentException("截图范围无效。");
        var image = new CroppedBitmap(source, new Int32Rect((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height));
        image.Freeze();
        return image;
    }
    internal static byte[] Png(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
    internal static BitmapSource Load(string path, int width = 0)
    {
        using var stream = File.OpenRead(path);
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.StreamSource = stream; bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (width > 0) bitmap.DecodePixelWidth = width;
        bitmap.EndInit(); bitmap.Freeze();
        return bitmap;
    }
    internal static BitmapSource Resize(BitmapSource source, int width)
    {
        if (width <= 0 || width == source.PixelWidth) return source;
        var bitmap = new TransformedBitmap(source, new ScaleTransform((double)width / source.PixelWidth, (double)width / source.PixelWidth));
        bitmap.Freeze(); return bitmap;
    }
    internal static async Task CopyAsync(BitmapSource source)
    {
        using var png = new MemoryStream(Png(source));
        var data = new DataObject();
        data.SetImage(source);
        data.SetData("PNG", png);
        for (var attempt = 0; ; attempt++)
        {
            try { Clipboard.SetDataObject(data, true); return; }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 4) { await Task.Delay(80); }
        }
    }
    internal static Rect Rect(PixelRect r) => new(r.X, r.Y, Math.Max(0, r.Width), Math.Max(0, r.Height));
}
