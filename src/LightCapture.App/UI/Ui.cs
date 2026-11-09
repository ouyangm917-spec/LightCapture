using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LightCapture.App.UI;

internal static class Ui
{
    internal static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(39, 45, 53));
    internal static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(110, 119, 132));
    internal static readonly Brush Green = new SolidColorBrush(Color.FromRgb(41, 157, 106));
    internal static TextBlock Text(string text, double size = 13, Brush? color = null) => new() { Text = text, FontSize = size, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) };
    internal static Button Button(string text, Action action, string? tip = null)
    {
        var button = new Button { Content = text, ToolTip = tip, Focusable = false };
        button.Click += (_, _) => action();
        return button;
    }
    internal static void Error(Exception error, Window? owner = null)
    {
        var message = error is AggregateException aggregate ? aggregate.GetBaseException().Message : error.Message;
        if (owner?.IsVisible == true) MessageBox.Show(owner, message, "轻截", MessageBoxButton.OK, MessageBoxImage.Warning);
        else MessageBox.Show(message, "轻截", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    internal static Window Dialog(string title, double width, double height) => new() { Title = title, Width = width, Height = height, MinWidth = width, MinHeight = height, WindowStartupLocation = WindowStartupLocation.CenterScreen };
}
