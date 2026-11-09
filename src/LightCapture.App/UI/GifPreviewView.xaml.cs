using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace LightCapture.App.UI;

public partial class GifPreviewView : UserControl
{
    private int _sourceWidth;
    private int _sourceHeight;
    internal double Speed => new[] { .5, 1, 1.5, 2 }[Math.Max(0, SpeedBox.SelectedIndex)];
    internal int OutputWidth => (int?)WidthBox.SelectedValue ?? _sourceWidth;

    public GifPreviewView() { InitializeComponent(); SetPlaying(false); }

    internal void Configure(int width, int height, double durationSeconds)
    {
        _sourceWidth = width; _sourceHeight = height;
        MediaInfo.Text = $"原片  {width} × {height}  ·  {durationSeconds:0.0} 秒";
        WidthBox.ItemsSource = new[] { width, 960, 640, 480 }.Where(value => value <= width).Distinct()
            .Select(value => new WidthChoice(value, value == width ? $"原尺寸 · {value} px" : $"{value} px")).ToArray();
        WidthBox.SelectedIndex = 0;
        SpeedBox.ItemsSource = new[] { "0.5× · 慢放", "1× · 正常", "1.5×", "2× · 快放" }; SpeedBox.SelectedIndex = 1;
        SeekSlider.Minimum = 0; SeekSlider.Value = 0;
        StartSlider.Maximum = EndSlider.Maximum = SeekSlider.Maximum = durationSeconds;
        StartSlider.Value = 0; EndSlider.Value = durationSeconds;
        UpdateSummary();
    }
    internal void UpdateSummary()
    {
        StartValue.Text = $"{StartSlider.Value:0.0} s";
        EndValue.Text = $"{EndSlider.Value:0.0} s";
        KeptDuration.Text = $"保留 {EndSlider.Value - StartSlider.Value:0.0}s";
        var height = Math.Max(1, (int)Math.Round(_sourceHeight * OutputWidth / (double)Math.Max(1, _sourceWidth)));
        OutputSummary.Text = $"{OutputWidth} × {height}\n约 {(EndSlider.Value - StartSlider.Value) / Speed:0.0} 秒 · 循环播放";
    }
    internal void SetPlaying(bool playing)
    {
        PlayButton.Content = new ToolbarIcon(playing ? "Pause" : "Play") { Foreground = new SolidColorBrush(Color.FromRgb(19, 168, 107)), Width = 18, Height = 18 };
        PlayButton.ToolTip = playing ? "暂停" : "播放";
        AutomationProperties.SetName(PlayButton, playing ? "暂停" : "播放");
    }
    internal void SetExporting(bool exporting)
    {
        SettingsPanel.IsEnabled = PlayButton.IsEnabled = SeekSlider.IsEnabled = SaveButton.IsEnabled = !exporting;
        ExportProgress.Value = 0; ExportProgress.Visibility = exporting ? Visibility.Visible : Visibility.Collapsed;
        CloseButton.Content = exporting ? "取消并关闭" : "关闭";
        CloseButton.Width = exporting ? 96 : 72;
    }
    internal void SetStatus(string text)
    {
        StatusText.Text = text; StatusText.ToolTip = text;
    }
    private sealed record WidthChoice(int Width, string Label);
}
