using System.Collections.Immutable;
using LightCapture.Core.Geometry;

namespace LightCapture.Core.Annotations;

public enum AnnotationKind { Select, Rectangle, Ellipse, Arrow, Pen, Mosaic, Text, Step }

public sealed record Annotation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required AnnotationKind Kind { get; init; }
    public required PixelPoint Start { get; init; }
    public required PixelPoint End { get; init; }
    public uint ColorArgb { get; init; } = 0xFFED805D;
    public double Thickness { get; init; } = 3;
    public string Text { get; init; } = "";
    public int StepNumber { get; init; }
    public ImmutableArray<PixelPoint> Points { get; init; } = [];

    public double StepRadius => 12 + Thickness * 2;
    public double FontSize => 20 + Thickness * 2;

    public PixelRect Bounds => Kind switch
    {
        AnnotationKind.Step => new(Start.X - StepRadius, Start.Y - StepRadius, StepRadius * 2, StepRadius * 2),
        AnnotationKind.Text => TextBounds(),
        AnnotationKind.Pen when !Points.IsDefaultOrEmpty => new(
            Points.Min(p => p.X), Points.Min(p => p.Y),
            Points.Max(p => p.X) - Points.Min(p => p.X), Points.Max(p => p.Y) - Points.Min(p => p.Y)),
        _ => PixelRect.FromPoints(Start, End)
    };

    public Annotation Move(double dx, double dy) => this with
    {
        Start = Start.Offset(dx, dy), End = End.Offset(dx, dy),
        Points = Points.IsDefaultOrEmpty ? [] : Points.Select(p => p.Offset(dx, dy)).ToImmutableArray()
    };

    public bool HitTest(PixelPoint point, double tolerance = 6)
    {
        if (Kind == AnnotationKind.Step) return Start.DistanceTo(point) <= StepRadius + tolerance;
        if (Kind == AnnotationKind.Arrow) return DistanceToSegment(point, Start, End) <= tolerance + Thickness;
        if (Kind == AnnotationKind.Pen && Points.Length > 1)
            return Enumerable.Range(1, Points.Length - 1).Any(i => DistanceToSegment(point, Points[i - 1], Points[i]) <= tolerance + Thickness);
        return Bounds.Contains(point, tolerance);
    }

    private PixelRect TextBounds()
    {
        var lines = Text.Replace("\r", "").Split('\n');
        // 实际文字由 WPF 测量；这里保留可选择的近似边界，兼顾中英文宽度。
        var width = lines.Max(line => line.Sum(c => c > 127 ? FontSize : FontSize * 0.58));
        return new(Start.X, Start.Y, Math.Max(FontSize, width), Math.Max(1, lines.Length) * FontSize * 1.35);
    }

    private static double DistanceToSegment(PixelPoint p, PixelPoint a, PixelPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var squared = dx * dx + dy * dy;
        if (squared < 0.001) return p.DistanceTo(a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / squared, 0, 1);
        return p.DistanceTo(new(a.X + t * dx, a.Y + t * dy));
    }
}
