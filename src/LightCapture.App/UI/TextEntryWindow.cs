using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LightCapture.App.UI;

internal sealed class TextEntryWindow : Window
{
    private readonly TextBox _text = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 100, MaxLength = 2000 };
    internal string Value => _text.Text;
    internal TextEntryWindow(Window owner, string initial = "")
    {
        Owner = owner; Title = "文字标注"; Width = 360; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Topmost = true;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(Ui.Text("输入文字", 16));
        panel.Children.Add(_text);
        panel.Children.Add(Ui.Text("Enter 换行 · Ctrl+Enter 完成", 11, Ui.Muted));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(Ui.Button("取消", () => DialogResult = false));
        actions.Children.Add(Ui.Button("完成", () => DialogResult = !string.IsNullOrWhiteSpace(Value)));
        panel.Children.Add(actions); Content = panel;
        _text.Text = initial;
        Loaded += (_, _) => { _text.Focus(); _text.CaretIndex = _text.Text.Length; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) DialogResult = !string.IsNullOrWhiteSpace(Value); };
    }
}
