using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LightCapture.App.Services;
using LightCapture.Core.Recording;

namespace LightCapture.App.UI;

internal sealed class GifPreviewWindow : Window
{
    private readonly RecordingSession _recording;
    private readonly Image _image = new() { Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 12) };
    private readonly Slider _start;
    private readonly Slider _end;
    private readonly ComboBox _width;
    private readonly ComboBox _speed;
    private readonly TextBlock _range = Ui.Text("");
    private readonly TextBlock _status = Ui.Text("", 12, Ui.Muted);
    private readonly Button _save;
    private readonly Button _play;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly Stopwatch _clock = new();
    private readonly CancellationTokenSource _cancel = new();
    private bool _playing;
    private bool _updating;
    private bool _exporting;
    private bool _closeRequested;
    private double _position;
    private int _frame = -1;

    internal GifPreviewWindow(RecordingSession recording)
    {
        _recording = recording;
        Title = "轻截 · GIF 预览"; Width = 760; Height = 620; MinWidth = 600; MinHeight = 500; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(20) };
        var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        bottom.Children.Add(_range);
        _start = new Slider { Minimum = 0, Maximum = recording.DurationMilliseconds / 1000, Value = 0, TickFrequency = .1, ToolTip = "开始时间（秒）" };
        _end = new Slider { Minimum = 0, Maximum = recording.DurationMilliseconds / 1000, Value = recording.DurationMilliseconds / 1000, TickFrequency = .1, ToolTip = "结束时间（秒）" };
        bottom.Children.Add(Ui.Text("开始", 11, Ui.Muted)); bottom.Children.Add(_start);
        bottom.Children.Add(Ui.Text("结束", 11, Ui.Muted)); bottom.Children.Add(_end);
        var options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        options.Children.Add(Ui.Text("输出宽度  "));
        var widths = new[] { recording.Width, 960, 640, 480 }.Where(w => w <= recording.Width).Distinct().ToArray();
        _width = new ComboBox { ItemsSource = widths, SelectedIndex = 0, Width = 80, Height = 28 }; options.Children.Add(_width);
        options.Children.Add(new TextBlock { Text = "像素    速度  ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
        _speed = new ComboBox { ItemsSource = new[] { "0.5×", "1×", "1.5×", "2×" }, SelectedIndex = 1, Width = 80, Height = 28 }; options.Children.Add(_speed);
        bottom.Children.Add(options);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        _play = Ui.Button("播放", TogglePlay); actions.Children.Add(_play);
        _save = Ui.Button("导出 GIF", async () => await SaveAsync()); actions.Children.Add(_save);
        actions.Children.Add(Ui.Button("关闭", Close)); bottom.Children.Add(actions); bottom.Children.Add(_status);
        root.Children.Add(_image); Content = root;
        _start.ValueChanged += (_, _) => RangeChanged(true);
        _end.ValueChanged += (_, _) => RangeChanged(false);
        _speed.SelectionChanged += (_, _) => { _clock.Restart(); };
        _timer.Tick += (_, _) => Advance();
        Loaded += (_, _) => { RangeChanged(true); _status.Text = string.IsNullOrEmpty(recording.StopReason) ? "循环播放 · 调整首尾后导出" : recording.StopReason; };
        Closing += (_, e) => { if (_exporting) { e.Cancel = true; _closeRequested = true; _cancel.Cancel(); _status.Text = "正在取消导出…"; } };
        Closed += (_, _) => { _timer.Stop(); _cancel.Dispose(); recording.Dispose(); };
    }
    private double Speed => new[] { .5, 1, 1.5, 2 }[_speed.SelectedIndex];
    private void RangeChanged(bool startChanged)
    {
        if (_updating) return; _updating = true;
        var gap = Math.Min(.1, _end.Maximum);
        if (_end.Value - _start.Value < gap)
        {
            if (startChanged) _start.Value = Math.Max(0, _end.Value - gap);
            else _end.Value = Math.Min(_end.Maximum, _start.Value + gap);
        }
        _position = _start.Value * 1000; _clock.Restart(); _updating = false;
        _range.Text = $"{_start.Value:0.0}s → {_end.Value:0.0}s    ·    保留 {_end.Value - _start.Value:0.0} 秒";
        ShowFrame();
    }
    private void TogglePlay()
    {
        _playing = !_playing; _play.Content = _playing ? "暂停" : "播放";
        if (_playing) { _clock.Restart(); _timer.Start(); } else _timer.Stop();
    }
    private void Advance()
    {
        _position += _clock.Elapsed.TotalMilliseconds * Speed; _clock.Restart();
        if (_position >= _end.Value * 1000) _position = _start.Value * 1000;
        ShowFrame();
    }
    private void ShowFrame()
    {
        var index = RecordingTimeline.FindFrame(_recording.Frames, _position);
        if (index == _frame || index < 0) return;
        try { _image.Source = BitmapIO.Load(_recording.Frames[index].Path, Math.Min(960, _recording.Width)); _frame = index; }
        catch (Exception e) { _timer.Stop(); _status.Text = $"无法读取预览：{e.Message}"; }
    }
    private async Task SaveAsync()
    {
        if (_exporting) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "GIF 动图|*.gif", FileName = $"轻截-{DateTime.Now:yyyyMMdd-HHmmss}.gif" };
        if (dialog.ShowDialog(this) != true) return;
        if (_playing) TogglePlay();
        _exporting = true; _save.IsEnabled = false;
        try
        {
            await GifExportService.ExportAsync(_recording, dialog.FileName, _start.Value * 1000, _end.Value * 1000, (int)_width.SelectedItem, Speed, new Progress<int>(value => _status.Text = $"正在导出 {value}%"), _cancel.Token);
            _status.Text = $"已保存：{dialog.FileName}";
        }
        catch (OperationCanceledException) { _status.Text = "导出已取消"; }
        catch (Exception e) { Ui.Error(e, this); _status.Text = "导出失败，可以重试"; }
        finally { _exporting = false; _save.IsEnabled = true; if (_closeRequested) Close(); }
    }
}
