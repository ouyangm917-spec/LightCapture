using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LightCapture.Core.Annotations;

namespace LightCapture.App.UI;

public partial class CaptureToolbar : UserControl
{
    private readonly Dictionary<AnnotationKind, ToggleButton> _tools = [];
    private readonly Dictionary<uint, ToggleButton> _colors = [];
    private readonly Dictionary<double, ToggleButton> _widths = [];
    private readonly List<ButtonBase> _editing = [];
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _delete;
    private readonly ToggleButton _translate;
    private readonly TextBlock _stepHint;
    internal event Action<AnnotationKind>? ToolChosen;
    internal event Action<uint>? ColorChosen;
    internal event Action<double>? ThicknessChosen;
    internal event Action? Undo, Redo, Translate, Record, Save, Cancel, Complete, Delete, CancelTranslation;
    internal event Action? DragRequested;

    public CaptureToolbar()
    {
        InitializeComponent();
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || !DragSurface.CanDrag(e.OriginalSource as DependencyObject, this)) return;
            e.Handled = true;
            DragRequested?.Invoke();
        };
        foreach (var (kind, tip) in new[]
        {
            (AnnotationKind.Select, "选择 / 移动 (V)"), (AnnotationKind.Rectangle, "矩形 (R)"),
            (AnnotationKind.Ellipse, "椭圆 (O)"), (AnnotationKind.Arrow, "箭头 (A)"),
            (AnnotationKind.Pen, "画笔 (P)"), (AnnotationKind.Mosaic, "马赛克 (M)"),
            (AnnotationKind.Text, "文字 (T)"), (AnnotationKind.Step, "步骤编号 (S)")
        })
        {
            var button = Toggle(kind.ToString(), tip, () => ToolChosen?.Invoke(kind));
            _tools[kind] = button; ToolsRow.Children.Add(button);
        }
        ToolsRow.Children.Add(Divider());
        _undo = ActionButton("Undo", "撤销 (Ctrl+Z)", () => Undo?.Invoke()); ToolsRow.Children.Add(_undo);
        _redo = ActionButton("Redo", "重做 (Ctrl+Y / Ctrl+Shift+Z)", () => Redo?.Invoke()); ToolsRow.Children.Add(_redo);
        _translate = Toggle("Translate", "翻译当前截图（离线英译中）", () => Translate?.Invoke()); ToolsRow.Children.Add(_translate);
        ToolsRow.Children.Add(ActionButton("Video", "录制当前选区为 GIF", () => Record?.Invoke()));
        ToolsRow.Children.Add(Divider());
        ToolsRow.Children.Add(ActionButton("Save", "保存 PNG (Ctrl+S)", () => Save?.Invoke()));
        var cancel = ActionButton("Close", "取消截图 (Esc)", () => Cancel?.Invoke());
        cancel.Foreground = Brush("#DA665E"); _editing.Remove(cancel); ToolsRow.Children.Add(cancel);
        var complete = ActionButton("Check", "完成并复制图片 (Enter)", () => Complete?.Invoke());
        complete.Width = 99; complete.Foreground = Brush("#13A86B");
        var label = new StackPanel { Orientation = Orientation.Horizontal };
        label.Children.Add(new ToolbarIcon("Check") { Width = 21, Height = 21, VerticalAlignment = VerticalAlignment.Center });
        label.Children.Add(new TextBlock { Text = "完成", FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        label.Children.Add(new TextBlock { Text = "Enter", FontSize = 9, Foreground = Brush("#9CB7A8"), Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        complete.Content = label; ToolsRow.Children.Add(complete);

        foreach (var color in new uint[] { 0xFFED805D, 0xFFFFCE62, 0xFF47C794, 0xFF5CA5F2, 0xFFB193E8, 0xFFFFFFFF, 0xFF22262E })
        {
            var button = Toggle(null, $"颜色 #{color & 0xFFFFFF:X6}", () => ColorChosen?.Invoke(color));
            button.Width = 24; button.Height = 26;
            button.Content = new Border { Width = 16, Height = 16, Background = AnnotationRenderer.ColorBrush(color), CornerRadius = new CornerRadius(3), BorderBrush = Brush("#DCE3DF"), BorderThickness = new Thickness(1) };
            _colors[color] = button; StyleRow.Children.Add(button);
        }
        StyleRow.Children.Add(Divider());
        foreach (var (width, tip) in new[] { (2d, "细"), (3d, "中"), (6d, "粗") })
        {
            var button = Toggle(null, tip, () => ThicknessChosen?.Invoke(width)); button.Width = 24; button.Height = 26;
            button.Content = new Border { Width = width + 2, Height = width + 2, CornerRadius = new CornerRadius(8), Background = Brush("#68766E") };
            _widths[width] = button; StyleRow.Children.Add(button);
        }
        _delete = ActionButton("Delete", "删除选中标注 (Delete)", () => Delete?.Invoke()); _delete.Height = 26; StyleRow.Children.Add(_delete);
        _stepHint = new TextBlock { FontSize = 11, Foreground = Brush("#84938A"), Margin = new Thickness(8, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        StyleRow.Children.Add(_stepHint);
        CancelTranslationButton.Click += (_, _) => CancelTranslation?.Invoke();
    }

    internal void Update(AnnotationKind tool, uint color, double thickness, bool canUndo, bool canRedo, int nextStep, bool selected, bool busy, bool translated)
    {
        foreach (var button in _editing) button.IsEnabled = !busy;
        foreach (var (kind, button) in _tools) button.IsChecked = kind == tool;
        foreach (var (value, button) in _colors) button.IsChecked = value == color;
        foreach (var (value, button) in _widths) button.IsChecked = value == thickness;
        _undo.IsEnabled = !busy && canUndo;
        _redo.IsEnabled = !busy && canRedo; _redo.Visibility = canRedo ? Visibility.Visible : Visibility.Collapsed;
        _delete.IsEnabled = !busy && selected;
        _translate.IsChecked = translated;
        _translate.ToolTip = translated ? "查看原图（再次点击恢复译图）" : "翻译当前截图（离线英译中）";
        Palette.Visibility = !busy && (tool != AnnotationKind.Select || selected) ? Visibility.Visible : Visibility.Collapsed;
        _stepHint.Text = tool == AnnotationKind.Step ? $"下一步 {nextStep}" : "";
    }
    internal void ShowStatus(string? text = null, bool cancellable = false, bool error = false)
    {
        StatusText.Text = text ?? "";
        StatusText.Foreground = Brush(error ? "#C05D55" : "#63796E");
        Status.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        CancelTranslationButton.Visibility = cancellable ? Visibility.Visible : Visibility.Collapsed;
    }
    private Button ActionButton(string icon, string tip, Action action)
    {
        var button = new Button { Style = (Style)Resources["Tool"], ToolTip = tip, Content = new ToolbarIcon(icon) };
        AutomationProperties.SetName(button, tip);
        button.Click += (_, _) => action(); _editing.Add(button); return button;
    }
    private ToggleButton Toggle(string? icon, string tip, Action action)
    {
        var button = new ToggleButton { Style = (Style)Resources["ToggleTool"], ToolTip = tip, Content = icon == null ? null : new ToolbarIcon(icon) };
        AutomationProperties.SetName(button, tip);
        button.Click += (_, _) => action(); _editing.Add(button); return button;
    }
    private static Border Divider() => new() { Width = 1, Height = 19, Margin = new Thickness(4, 0, 4, 0), Background = Brush("#E6E9EB"), VerticalAlignment = VerticalAlignment.Center };
    private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
}
