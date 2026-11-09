using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LightCapture.App.UI;

namespace LightCapture.App.Diagnostics;

/// <summary>仅通过 --demo 打开的验收样本，避免测试录制个人屏幕内容。</summary>
internal sealed class DemoWindow : Window
{
    internal DemoWindow()
    {
        Title = "LightCapture · 验收样本"; Width = 900; Height = 650; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new StackPanel { Margin = new Thickness(44, 34, 44, 30) };
        root.Children.Add(Ui.Text("LIGHTCAPTURE  /  QUICK START", 12, Ui.Muted));
        root.Children.Add(Ui.Text("Capture. Explain. Share.", 34));
        root.Children.Add(Ui.Text("A small tool for clear ideas.", 19, Ui.Muted));
        root.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 18) });
        root.Children.Add(Ui.Text("Step 1: Select an area of your screen.", 23));
        root.Children.Add(Ui.Text("Step 2: Add arrows, text, or numbered steps.", 23));
        root.Children.Add(Ui.Text("Step 3: Copy the image and paste it anywhere.", 23));
        root.Children.Add(Ui.Text("Click the Save button to save the screenshot.", 20));
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 12, Margin = new Thickness(0, 28, 0, 10), Foreground = Ui.Green };
        root.Children.Add(bar);
        var hint = Ui.Text("按截图快捷键开始验收；完成后在这里按 Ctrl+V 检查图片粘贴。", 12, Ui.Muted); root.Children.Add(hint);
        var pasted = new Image { MaxHeight = 350, Stretch = Stretch.Uniform };
        var scroll = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Content = scroll;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) => bar.Value = (bar.Value + 2) % 100;
        Loaded += (_, _) => timer.Start(); Closed += (_, _) => timer.Stop();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (!Clipboard.ContainsImage()) { hint.Text = "剪贴板没有图片。"; return; }
                var clipboardImage = Clipboard.GetImage();
                if (clipboardImage == null) { hint.Text = "剪贴板图片暂时不可用。"; return; }
                pasted.Source = clipboardImage;
                if (!root.Children.Contains(pasted)) root.Children.Add(pasted);
                hint.Text = $"图片粘贴成功：{clipboardImage.PixelWidth} × {clipboardImage.PixelHeight}";
                scroll.ScrollToEnd(); e.Handled = true;
            }
        };
    }
}
