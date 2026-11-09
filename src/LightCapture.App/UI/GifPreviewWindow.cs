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
    private readonly GifPreviewView _view = new();
    private Image PreviewImage => _view.PreviewImage;
    private Slider StartSlider => _view.StartSlider;
    private Slider EndSlider => _view.EndSlider;
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
        Title = "轻截 · GIF 预览"; Width = 920; Height = 640; MinWidth = 800; MinHeight = 520; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(245, 248, 246));
        _view.Configure(recording.Width, recording.Height, recording.DurationMilliseconds / 1000);
        Content = _view;
        StartSlider.ValueChanged += (_, _) => RangeChanged(true);
        EndSlider.ValueChanged += (_, _) => RangeChanged(false);
        _view.SpeedBox.SelectionChanged += (_, _) => { _clock.Restart(); _view.UpdateSummary(); };
        _view.WidthBox.SelectionChanged += (_, _) => _view.UpdateSummary();
        _view.SeekSlider.ValueChanged += (_, _) =>
        {
            if (_updating || _exporting) return;
            _position = _view.SeekSlider.Value * 1000; _clock.Restart(); ShowFrame();
        };
        _view.PlayButton.Click += (_, _) => TogglePlay();
        _view.SaveButton.Click += async (_, _) => await SaveAsync();
        _view.CloseButton.Click += (_, _) => Close();
        _timer.Tick += (_, _) => Advance();
        Loaded += (_, _) => { RangeChanged(true); _view.SetStatus(string.IsNullOrEmpty(recording.StopReason) ? "调整首尾后，导出你的 GIF" : recording.StopReason); };
        Closing += (_, e) => { if (_exporting) { e.Cancel = true; _closeRequested = true; _cancel.Cancel(); _view.SetStatus("正在取消导出…"); } };
        Closed += (_, _) => { _timer.Stop(); _cancel.Dispose(); recording.Dispose(); };
    }
    private double Speed => _view.Speed;
    private void RangeChanged(bool startChanged)
    {
        if (_updating) return; _updating = true;
        var gap = Math.Min(.1, EndSlider.Maximum);
        if (EndSlider.Value - StartSlider.Value < gap)
        {
            if (startChanged) StartSlider.Value = Math.Max(0, EndSlider.Value - gap);
            else EndSlider.Value = Math.Min(EndSlider.Maximum, StartSlider.Value + gap);
        }
        _position = StartSlider.Value * 1000; _clock.Restart();
        _view.SeekSlider.Minimum = StartSlider.Value; _view.SeekSlider.Maximum = EndSlider.Value;
        _updating = false;
        _view.UpdateSummary();
        ShowFrame();
    }
    private void TogglePlay()
    {
        if (_exporting) return;
        _playing = !_playing; _view.SetPlaying(_playing);
        if (_playing) { _clock.Restart(); _timer.Start(); } else _timer.Stop();
    }
    private void Advance()
    {
        _position += _clock.Elapsed.TotalMilliseconds * Speed; _clock.Restart();
        if (_position >= EndSlider.Value * 1000) _position = StartSlider.Value * 1000;
        ShowFrame();
    }
    private void ShowFrame()
    {
        _updating = true; _view.SeekSlider.Value = _position / 1000; _updating = false;
        _view.PlaybackTime.Text = $"{Math.Max(0, _position / 1000 - StartSlider.Value):00.0} / {EndSlider.Value - StartSlider.Value:00.0}s";
        // 修剪终点是右开边界；拖到最右端时仍显示导出片段内的最后一帧。
        var frameTime = Math.Clamp(_position, StartSlider.Value * 1000, Math.Max(StartSlider.Value * 1000, EndSlider.Value * 1000 - .001));
        var index = RecordingTimeline.FindFrame(_recording.Frames, frameTime);
        if (index == _frame || index < 0) return;
        try { PreviewImage.Source = BitmapIO.Load(_recording.Frames[index].Path, Math.Min(1280, _recording.Width)); _frame = index; }
        catch (Exception e) { _timer.Stop(); _playing = false; _view.SetPlaying(false); _view.SetStatus($"无法读取预览：{e.Message}"); }
    }
    private async Task SaveAsync()
    {
        if (_exporting) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "GIF 动图|*.gif", FileName = $"轻截-{DateTime.Now:yyyyMMdd-HHmmss}.gif" };
        if (dialog.ShowDialog(this) != true) return;
        if (_playing) TogglePlay();
        _exporting = true; _view.SetExporting(true);
        _view.SetStatus("正在导出 0%…");
        try
        {
            await GifExportService.ExportAsync(_recording, dialog.FileName, StartSlider.Value * 1000, EndSlider.Value * 1000, _view.OutputWidth, Speed, new Progress<int>(value =>
            {
                if (!_exporting || _closeRequested) return;
                _view.ExportProgress.Value = value; _view.SetStatus($"正在导出 {value}%…");
            }), _cancel.Token);
            _view.SetStatus($"已保存：{dialog.FileName}");
        }
        catch (OperationCanceledException) { _view.SetStatus("导出已取消"); }
        catch (Exception e) { if (!_closeRequested) Ui.Error(e, this); _view.SetStatus("导出失败，可以重试"); }
        finally { _exporting = false; _view.SetExporting(false); if (_closeRequested) Close(); }
    }
}
