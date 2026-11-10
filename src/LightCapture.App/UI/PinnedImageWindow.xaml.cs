using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LightCapture.App.Native;
using LightCapture.App.Services;
using LightCapture.Core.Geometry;

namespace LightCapture.App.UI;

internal partial class PinnedImageWindow : Window
{
    private bool _copying;
    private bool _closed;
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
        Closed += (_, _) => { _closed = true; Picture.Source = null; };
    }

    private async Task CopyAsync()
    {
        if (_copying || _closed || Snapshot is not { } image) return;
        _copying = true;
        try { await BitmapIO.CopyAsync(image); }
        catch (Exception e) { if (!_closed) Ui.Error(e, this); }
        finally { _copying = false; }
    }
}
