using System.Windows;
using System.Windows.Controls;
using LightCapture.App.Services;

namespace LightCapture.App.UI;

internal sealed class SettingsWindow : Window
{
    internal SettingsWindow(SettingsStore settings, Func<bool, string?> applyHotkey)
    {
        Title = "轻截 · 设置"; Width = 390; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(Ui.Text("简单截一下", 22));
        root.Children.Add(Ui.Text("框选 → 标注 → Enter 复制 → 粘贴", 13, Ui.Muted));
        root.Children.Add(Ui.Text("截图快捷键", 12));
        var hotkey = new ComboBox { ItemsSource = new[] { "Alt + A", "Ctrl + Alt + A" }, SelectedIndex = settings.Value.ControlHotkey ? 1 : 0, Height = 32 };
        root.Children.Add(hotkey);
        root.Children.Add(Ui.Text("如果已被微信等应用占用，请切换另一个快捷键。", 11, Ui.Muted));
        root.Children.Add(Ui.Text(OfflineTranslator.Available ? "英译中：离线语言包已就绪" : "英译中：缺少离线语言包", 13));
        root.Children.Add(Ui.Text("无账号、无 API Key。识别和翻译均在本机处理。", 12, Ui.Muted));
        root.Children.Add(Ui.Text("GIF：15 fps · 最长 60 秒 · 最大宽度 1280", 12, Ui.Muted));
        root.Children.Add(Ui.Button("保存", () =>
        {
            var useControl = hotkey.SelectedIndex == 1;
            var error = applyHotkey(useControl);
            if (error != null) { MessageBox.Show(this, error, "轻截"); return; }
            settings.Value.ControlHotkey = useControl;
            try { settings.Save(); Close(); } catch (Exception e) { Ui.Error(e, this); }
        }));
        Content = root;
    }
}
