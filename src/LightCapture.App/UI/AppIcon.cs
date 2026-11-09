using System.Windows;

namespace LightCapture.App.UI;

internal static class AppIcon
{
    internal static readonly Uri ResourceUri = new("pack://application:,,,/Assets/LightCapture.ico");

    internal static System.Drawing.Icon LoadTrayIcon()
    {
        using var stream = Application.GetResourceStream(ResourceUri)?.Stream
            ?? throw new InvalidOperationException("程序图标资源缺失。");
        using var icon = new System.Drawing.Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
        // 托盘持有独立图标，避免资源流释放后依赖已关闭的流。
        return (System.Drawing.Icon)icon.Clone();
    }
}
