using System.Windows;
using System.Windows.Controls.Primitives;
using LightCapture.App.Services;
using LightCapture.App.UI;

namespace LightCapture.App.Diagnostics;

internal static class StartupChecks
{
    internal static Task RunAsync(string output, Func<string, Func<Task<string>>, Task> check) => check("自启动设置、取消和失败回退", () =>
    {
        var store = new SettingsStore(Path.Combine(output, "startup-check"));
        store.Value.ControlHotkey = false;
        string? command = null;
        var writes = 0;
        var activeHotkey = false;
        var rejectWrite = false;
        var rejectHotkey = false;
        var startup = new StartupRegistration(@"C:\轻截 App\LightCapture.exe", () => command, value =>
        {
            writes++;
            if (rejectWrite) throw new UnauthorizedAccessException("测试拒绝写入");
            command = value;
        });
        Require(startup.Command == "\"C:\\轻截 App\\LightCapture.exe\" --autostart", "含空格的启动路径未正确引用");
        SettingsWindow Open() => new(store, value =>
        {
            if (rejectHotkey) return "测试快捷键冲突";
            activeHotkey = value; return null;
        }, startup);
        void Save(bool enabled)
        {
            var window = Open();
            try
            {
                var view = (SettingsView)window.Content;
                view.StartWithWindows = enabled;
                view.SaveButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Require(view.ErrorText.Visibility == Visibility.Collapsed, "正常保存出现错误");
            }
            finally { window.Close(); }
        }

        Save(true);
        Require(command == startup.Command, "启用后启动命令错误");
        var cancel = Open();
        try
        {
            var view = (SettingsView)cancel.Content;
            Require(view.StartWithWindows, "设置未读取当前启动项");
            view.StartWithWindows = false;
            view.CancelButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(command == startup.Command && writes == 1, "取消修改了启动项");
        }
        finally { cancel.Close(); }
        Save(false);
        Require(command == null, "关闭开关未移除启动项");
        command = "\"D:\\旧版本\\LightCapture.exe\" --autostart";
        Save(true);
        Require(command == startup.Command, "保存没有刷新旧版本路径");
        rejectHotkey = true;
        var conflict = Open();
        try
        {
            var view = (SettingsView)conflict.Content;
            view.StartWithWindows = false;
            var before = writes;
            view.SaveButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(writes == before && view.ErrorText.Visibility == Visibility.Visible, "快捷键冲突仍修改启动项");
        }
        finally { conflict.Close(); rejectHotkey = false; }

        rejectWrite = true;
        var denied = Open();
        try
        {
            var view = (SettingsView)denied.Content;
            view.StartWithWindows = false; view.UseControlHotkey = true;
            view.SaveButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(!activeHotkey && !store.Value.ControlHotkey && command == startup.Command && view.ErrorText.Visibility == Visibility.Visible, "启动项写入失败未回退快捷键");
        }
        finally { denied.Close(); rejectWrite = false; }

        var blocked = Path.Combine(store.DataDirectory, "settings.json.tmp");
        Directory.CreateDirectory(blocked);
        var failed = Open();
        try
        {
            var view = (SettingsView)failed.Content;
            view.StartWithWindows = false; view.UseControlHotkey = true;
            view.SaveButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(!activeHotkey && !store.Value.ControlHotkey && command == startup.Command && view.ErrorText.Visibility == Visibility.Visible, "配置保存失败未还原启动项和快捷键");
        }
        finally { failed.Close(); Directory.Delete(blocked); }

        var unreadable = new StartupRegistration(@"C:\LightCapture.exe", () => throw new UnauthorizedAccessException("测试拒绝读取"), _ => throw new InvalidOperationException("不应写入"));
        var readFailure = new SettingsWindow(store, _ => throw new InvalidOperationException("不应注册快捷键"), unreadable);
        try
        {
            var view = (SettingsView)readFailure.Content;
            Require(!view.SaveButton.IsEnabled && view.ErrorText.Visibility == Visibility.Visible, "启动项读取失败仍允许保存");
        }
        finally { readFailure.Close(); }
        return Task.FromResult("启用/关闭、取消、含空格路径、旧路径更新、快捷键冲突、读写失败及配置写盘失败回退通过；使用内存启动项替身，没有修改 Windows 启动项或显示窗口。");
    });

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
