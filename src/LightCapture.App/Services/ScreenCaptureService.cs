using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using LightCapture.App.Native;
using LightCapture.Core.Geometry;

namespace LightCapture.App.Services;

internal static class ScreenCaptureService
{
    internal static PixelRect DesktopBounds
    {
        get { var r = System.Windows.Forms.SystemInformation.VirtualScreen; return new(r.X, r.Y, r.Width, r.Height); }
    }
    internal static Bitmap Capture(PixelRect region, bool cursor = false, int maxWidth = int.MaxValue)
    {
        var r = region.RoundOut();
        if (r.IsEmpty || r.Width * r.Height > 60_000_000) throw new InvalidOperationException("截图范围过大，请降低屏幕分辨率后重试。");
        using var original = new Bitmap((int)r.Width, (int)r.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(original))
        {
            graphics.CopyFromScreen((int)r.X, (int)r.Y, 0, 0, original.Size, CopyPixelOperation.SourceCopy);
            if (cursor)
            {
                var info = new NativeMethods.CursorInfo { Size = Marshal.SizeOf<NativeMethods.CursorInfo>() };
                if (NativeMethods.GetCursorInfo(ref info) && info.Flags == 1 && NativeMethods.GetIconInfo(info.Cursor, out var icon))
                {
                    var hdc = graphics.GetHdc();
                    try { NativeMethods.DrawIconEx(hdc, info.Position.X - (int)r.X - (int)icon.HotspotX, info.Position.Y - (int)r.Y - (int)icon.HotspotY, info.Cursor, 0, 0, 0, IntPtr.Zero, 3); }
                    finally
                    {
                        graphics.ReleaseHdc(hdc);
                        if (icon.Mask != IntPtr.Zero) NativeMethods.DeleteObject(icon.Mask);
                        if (icon.Color != IntPtr.Zero) NativeMethods.DeleteObject(icon.Color);
                    }
                }
            }
        }
        if (r.Width <= maxWidth) return (Bitmap)original.Clone();
        var resized = new Bitmap(maxWidth, Math.Max(1, (int)Math.Round(r.Height * maxWidth / r.Width)), PixelFormat.Format24bppRgb);
        using var target = Graphics.FromImage(resized);
        target.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        target.DrawImage(original, 0, 0, resized.Width, resized.Height);
        return resized;
    }

    internal static BitmapSource ToSource(Bitmap bitmap)
    {
        var hbitmap = bitmap.GetHbitmap();
        try
        {
            var image = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally { NativeMethods.DeleteObject(hbitmap); }
    }
    internal static BitmapSource Desktop()
    {
        using var bitmap = Capture(DesktopBounds);
        return ToSource(bitmap);
    }
}
