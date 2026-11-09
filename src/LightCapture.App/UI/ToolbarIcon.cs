using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LightCapture.App.UI;

/// <summary>沿用 HTML 原型的 24×24 线条图标，避免字体符号随系统字体变化。</summary>
internal sealed class ToolbarIcon : Control
{
    private readonly Geometry _geometry;
    internal ToolbarIcon(string name)
    {
        Width = Height = 19;
        IsHitTestVisible = false;
        _geometry = Geometry.Parse(name switch
        {
            "Select" => "M5,3 L19,12 12,13 9,20 Z",
            "Rectangle" => "M4,5 H20 V19 H4 Z",
            "Ellipse" => "M20,12 A8,7 0 1 1 4,12 A8,7 0 1 1 20,12",
            "Arrow" => "M4,20 L20,4 M11,4 H20 V13",
            "Pen" => "M4,20 L9,19 20,8 16,4 5,15 Z M13,7 L17,11 M5,15 L9,19",
            "Mosaic" => "M4,4 H10 V10 H4 Z M14,4 H20 V10 H14 Z M4,14 H10 V20 H4 Z M14,14 H20 V20 H14 Z",
            "Text" => "M4,6 V4 H20 V6 M12,4 V20 M8,20 H16",
            "Step" => "M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M10,9 L12,7 V17 M10,17 H14",
            "Undo" => "M8,3 L3,8 8,13 M3,8 H14 A6,6 0 0 1 14,20",
            "Redo" => "M16,3 L21,8 16,13 M21,8 H10 A6,6 0 0 0 10,20",
            "Translate" => "M3,5 H14 M8,3 V5 M11,5 C11,10 8,13 4,15 M5,8 C6,11 9,13 12,14 M13,21 L17,11 21,21 M15,17 H19",
            "Video" => "M6,5 H13 Q16,5 16,8 V16 Q16,19 13,19 H6 Q3,19 3,16 V8 Q3,5 6,5 Z M16,10 L21,7 V17 L16,14",
            "Save" => "M12,3 V15 M8,11 L12,15 16,11 M4,16 V21 H20 V16",
            "Check" => "M5,12 L9,16 19,6",
            "Close" => "M6,6 L18,18 M6,18 L18,6",
            "Delete" => "M9,5 H15 M4,8 H20 M6,8 L7,21 H17 L18,8 M10,11 V18 M14,11 V18",
            "Play" => "M8,5 L19,12 8,19 Z",
            "Pause" => "M9,5 V19 M15,5 V19",
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        });
        _geometry.Freeze();
    }
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        dc.DrawGeometry(null, new Pen(Foreground, 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, _geometry);
        dc.Pop();
    }
}
