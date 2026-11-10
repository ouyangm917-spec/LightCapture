using System.Windows;
using LightCapture.App.Services;

namespace LightCapture.App.UI;

internal sealed class SettingsWindow : Window
{
    internal SettingsWindow(SettingsStore settings, Func<bool, string?> applyHotkey)
    {
        Title = "轻截 · 设置"; Width = 400; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var view = new SettingsView { UseControlHotkey = settings.Value.ControlHotkey };
        view.SetOfflineReady(OfflineTranslator.Available);
        view.CancelButton.Click += (_, _) => Close();
        view.SaveButton.Click += (_, _) =>
        {
            view.ShowError(null);
            var useControl = view.UseControlHotkey;
            var previous = settings.Value.ControlHotkey;
            var error = applyHotkey(useControl);
            if (error != null) { view.ShowError(error); return; }
            settings.Value.ControlHotkey = useControl;
            try { settings.Save(); Close(); }
            catch (Exception e)
            {
                settings.Value.ControlHotkey = previous;
                string? restoreError;
                try { restoreError = applyHotkey(previous); }
                catch (Exception restore) { restoreError = restore.Message; }
                view.ShowError(restoreError == null ? $"保存失败：{e.Message}" : $"保存失败，快捷键未能恢复：{restoreError}");
            }
        };
        Content = view;
    }
}
