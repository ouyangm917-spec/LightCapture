using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LightCapture.App.Native;
using LightCapture.App.Services;
using LightCapture.Core.Geometry;

namespace LightCapture.App.UI;

internal sealed class RecordingWindow : Window
{
    private readonly CancellationTokenSource _stop = new();
    private readonly TextBlock _time = new() { Text = "准备录制…", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
    private bool _finished;
    internal event Action<RecordingSession>? Ready;
    internal void Stop() => _stop.Cancel();
    internal RecordingWindow(PixelRect region, string cacheRoot, string hotkey)
    {
        Title = "轻截 · 录制"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; SizeToContent = SizeToContent.WidthAndHeight;
        Background = new SolidColorBrush(Color.FromRgb(35, 40, 47));
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(7) };
        panel.Children.Add(new TextBlock { Text = "●", Foreground = Brushes.Tomato, FontSize = 19, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) });
        panel.Children.Add(_time);
        panel.Children.Add(Ui.Button("停止", Stop, $"停止录制 ({hotkey})")); Content = panel;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Stop(); };
        Closing += (_, e) => { if (!_finished) { e.Cancel = true; Stop(); } };
        Loaded += async (_, _) =>
        {
            NativeMethods.PlaceNear(this, region); NativeMethods.ExcludeFromCapture(this);
            try
            {
                await Task.Delay(250, _stop.Token);
                var recording = await RecordingSession.RecordAsync(region, cacheRoot, new Progress<TimeSpan>(time => _time.Text = $"{time:mm\\:ss} / 01:00"), _stop.Token);
                _finished = true; Close(); Ready?.Invoke(recording);
            }
            catch (OperationCanceledException) { _finished = true; Close(); }
            catch (Exception e) { _finished = true; Close(); Ui.Error(e); }
            finally { _stop.Dispose(); }
        };
    }
}
