using Microsoft.Win32;

namespace LightCapture.App.Services;

/// <summary>管理当前用户的登录启动项；读取系统登记值，避免与独立配置文件不同步。</summary>
internal sealed class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LightCapture";
    private readonly Func<string?> _read;
    private readonly Action<string?> _write;
    internal string Command { get; }

    internal StartupRegistration(string executable, Func<string?>? read = null, Action<string?>? write = null)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"')
            || !string.Equals(Path.GetFileName(executable), "LightCapture.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("请通过 LightCapture.exe 设置开机自启动。", nameof(executable));
        Command = $"\"{executable}\" --autostart";
        _read = read ?? ReadRegistry;
        _write = write ?? WriteRegistry;
    }

    internal string? Read() => _read();
    internal void Write(string? command)
    {
        if (command == Command && command.Length > 260) throw new InvalidOperationException("应用路径过长，请将完整文件夹移至较短路径后重试。");
        _write(command);
    }

    private static string? ReadRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    private static void WriteRegistry(string? command)
    {
        if (command == null)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        else
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }
    }
}
