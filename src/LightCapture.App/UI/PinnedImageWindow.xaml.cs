using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using LightCapture.App.Native;
using LightCapture.App.Services;
using LightCapture.Core.Geometry;

namespace LightCapture.App.UI;

internal partial class PinnedImageWindow : Window
{
    private bool _copying;
    private bool _closed;
    private double _zoom = 1;
    internal BitmapSource? Snapshot => Picture.Source as BitmapSource;

    internal PinnedImageWindow(BitmapSource snapshot, PixelRect region)
    {
        InitializeComponent();
        Picture.Source = snapshot;
        Left = region.X; Top = region.Y; Width = region.Width; Height = region.Height;
        Loaded += (_, _) => { NativeMethods.Place(this, region); NativeMethods.KeepInWorkArea(this); };
        MouseEnter += (_, _) => CloseButton.Visibility = Visibility.Visible;
        MouseLeave += (_, _) => CloseButton.Visibility = Visibility.Hidden;
        CloseButton.Click += (_, _) => Close();
        PreviewMouseWheel += (_, e) =>
        {
            if (_closed || !IsLoaded || e.Delta == 0 || ActualWidth <= 0 || ActualHeight <= 0) return;
            e.Handled = true;
            var zoom = NextZoom(_zoom, e.Delta);
            if (zoom == _zoom) return;
            var origin = PointToScreen(new Point());
            var end = PointToScreen(new Point(ActualWidth, ActualHeight));
            var pointer = e.GetPosition(this);
            var bounds = ZoomBounds(new(origin.X, origin.Y, end.X - origin.X, end.Y - origin.Y),
                new(pointer.X / ActualWidth, pointer.Y / ActualHeight), zoom / _zoom);
            // 使用实际屏幕像素保持鼠标锚点，避免跨 DPI 显示器时混用窗口 DIP 坐标。
            if (NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero,
                (int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, 0x0014))
            {
                _zoom = zoom;
            }
        };
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || !DragSurface.CanDrag(e.OriginalSource as DependencyObject, this)) return;
            e.Handled = true;
            try { DragMove(); }
            catch (InvalidOperationException) when (Mouse.LeftButton != MouseButtonState.Pressed || _closed) { }
            if (!_closed) NativeMethods.KeepInWorkArea(this);
        };
        var menu = new ContextMenu();
        var copy = new MenuItem { Header = "复制图片", InputGestureText = "Ctrl+C" };
        copy.Click += async (_, _) => await CopyAsync();
        var close = new MenuItem { Header = "关闭这张贴图", InputGestureText = "Esc" };
        close.Click += (_, _) => Close();
        menu.Items.Add(copy); menu.Items.Add(new Separator()); menu.Items.Add(close);
        ContextMenu = menu;
        KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Close(); }
            else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            { e.Handled = true; await CopyAsync(); }
        };
        Closed += (_, _) =>
        {
            _closed = true;
            Picture.Source = null; ContextMenu = null; Content = null;
            ImageMemory.RequestCleanup();
        };
    }

    private async Task CopyAsync()
    {
        if (_copying || _closed || Snapshot is not { } image) return;
        using var operation = ImageMemory.BeginOperation();
        _copying = true;
        try { await BitmapIO.CopyAsync(image); }
        catch (Exception e) { if (!_closed) Ui.Error(e, this); }
        finally { _copying = false; }
    }

    internal static double NextZoom(double current, int delta) => Math.Clamp(current * Math.Pow(1.1, delta / 120.0), .1, 4);

    internal static PixelRect ZoomBounds(PixelRect current, PixelPoint anchor, double factor)
    {
        var width = Math.Max(1, Math.Round(current.Width * factor));
        var height = Math.Max(1, Math.Round(current.Height * factor));
        return new(Math.Round(current.X + (current.Width - width) * Math.Clamp(anchor.X, 0, 1)),
            Math.Round(current.Y + (current.Height - height) * Math.Clamp(anchor.Y, 0, 1)), width, height);
    }
}
