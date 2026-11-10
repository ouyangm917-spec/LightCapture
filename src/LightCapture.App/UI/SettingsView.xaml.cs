using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LightCapture.App.UI;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    internal bool UseControlHotkey
    {
        get => ControlChoice.IsChecked == true;
        set { ControlChoice.IsChecked = value; AltChoice.IsChecked = !value; }
    }

    internal void SetOfflineReady(bool ready)
    {
        StatusText.Text = ready ? "已就绪" : "语言包缺失";
        StatusText.Foreground = (Brush)new BrushConverter().ConvertFromString(ready ? "#0A8955" : "#B57225")!;
        StatusBadge.Background = (Brush)new BrushConverter().ConvertFromString(ready ? "#E6F5EE" : "#FFF3DF")!;
    }

    internal void ShowError(string? error)
    {
        ErrorText.Text = error ?? "";
        ErrorText.Visibility = string.IsNullOrEmpty(error) ? Visibility.Collapsed : Visibility.Visible;
    }
}
