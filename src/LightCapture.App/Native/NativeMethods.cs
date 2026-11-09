using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LightCapture.Core.Geometry;

namespace LightCapture.App.Native;

internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] internal static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr value);
    [DllImport("user32.dll")] internal static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] internal static extern bool GetIconInfo(IntPtr icon, out IconInfo info);
    [DllImport("user32.dll")] internal static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon, int width, int height, uint step, IntPtr brush, uint flags);
    [StructLayout(LayoutKind.Sequential)] internal struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct CursorInfo { public int Size; public int Flags; public IntPtr Cursor; public NativePoint Position; }
    [StructLayout(LayoutKind.Sequential)] internal struct IconInfo { public int IsIcon; public uint HotspotX; public uint HotspotY; public IntPtr Mask; public IntPtr Color; }

    internal static void Place(Window window, PixelRect rect)
    {
        SetWindowPos(new WindowInteropHelper(window).Handle, new IntPtr(-1), (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, 0x0040);
    }

    internal static void PlaceNear(Window window, PixelRect region)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)region.Center.X, (int)region.Center.Y));
        var area = screen.WorkingArea;
        var handle = new WindowInteropHelper(window).Handle;
        var scale = Math.Max(96, GetDpiForWindow(handle)) / 96.0;
        var width = (int)Math.Ceiling(window.ActualWidth * scale);
        var height = (int)Math.Ceiling(window.ActualHeight * scale);
        var x = Math.Clamp((int)region.Right - width, area.Left, Math.Max(area.Left, area.Right - width));
        var y = (int)region.Bottom + 10;
        if (y + height > area.Bottom) y = (int)region.Y - height - 10;
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - height));
        SetWindowPos(handle, new IntPtr(-1), x, y, width, height, 0x0050);
    }

    internal static void ExcludeFromCapture(Window window) => SetWindowDisplayAffinity(new WindowInteropHelper(window).Handle, 0x11);

    internal static void KeepInWorkArea(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect)) return;
        var area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var current = new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        var constrained = ConstrainToArea(current, new(area.Left, area.Top, area.Width, area.Height));
        if (constrained.TopLeft != current.TopLeft)
            SetWindowPos(handle, IntPtr.Zero, (int)constrained.X, (int)constrained.Y, 0, 0, 0x0015); // 只移动，保留尺寸、层级与焦点。
    }
    internal static PixelRect ConstrainToArea(PixelRect window, PixelRect area) => new(
        Math.Clamp(window.X, area.X, Math.Max(area.X, area.Right - window.Width)),
        Math.Clamp(window.Y, area.Y, Math.Max(area.Y, area.Bottom - window.Height)), window.Width, window.Height);
}
