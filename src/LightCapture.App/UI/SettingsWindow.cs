using System.Windows;
using LightCapture.App.Services;

namespace LightCapture.App.UI;

internal sealed class SettingsWindow : Window
{
    internal SettingsWindow(SettingsStore settings, Func<bool, string?> applyHotkey, StartupRegistration startup)
    {
        Title = "轻截 · 设置"; Width = 400; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var view = new SettingsView { UseControlHotkey = settings.Value.ControlHotkey };
        view.SetOfflineReady(OfflineTranslator.Available);
        try { view.StartWithWindows = !string.IsNullOrEmpty(startup.Read()); }
        catch (Exception e) { view.ShowError($"无法读取启动项：{e.Message}"); view.SaveButton.IsEnabled = false; }
        view.CancelButton.Click += (_, _) => Close();
        view.SaveButton.Click += (_, _) =>
        {
            view.ShowError(null);
            var useControl = view.UseControlHotkey;
            var previous = settings.Value.ControlHotkey;
            string? previousStartup;
            try { previousStartup = startup.Read(); }
            catch (Exception e) { view.ShowError($"无法读取启动项：{e.Message}"); return; }
            var error = applyHotkey(useControl);
            if (error != null) { view.ShowError(error); return; }
            settings.Value.ControlHotkey = useControl;
            var nextStartup = view.StartWithWindows ? startup.Command : null;
            var startupChanged = !string.Equals(previousStartup, nextStartup, StringComparison.Ordinal);
            try
            {
                if (startupChanged) startup.Write(nextStartup);
                settings.Save(); Close();
            }
            catch (Exception e)
            {
                settings.Value.ControlHotkey = previous;
                string? restoreError;
                try { restoreError = applyHotkey(previous); }
                catch (Exception restore) { restoreError = restore.Message; }
                string? startupRestoreError = null;
                if (startupChanged)
                {
                    try { startup.Write(previousStartup); }
                    catch (Exception restore) { startupRestoreError = restore.Message; }
                }
                view.ShowError($"保存失败：{e.Message}"
                    + (restoreError == null ? "" : $"；快捷键恢复失败：{restoreError}")
                    + (startupRestoreError == null ? "" : $"；启动项恢复失败：{startupRestoreError}"));
            }
        };
        Content = view;
    }
}
