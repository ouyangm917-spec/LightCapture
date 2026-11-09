using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCapture.App.Services;
using LightCapture.Core.Annotations;
using LightCapture.Core.Geometry;

namespace LightCapture.App.UI;

internal sealed class AnnotationRenderer(BitmapSource background)
{
    private readonly Dictionary<Guid, (PixelRect Rect, int Block, BitmapSource Bitmap)> _mosaics = [];
    private static Point Point(PixelPoint p) => new(p.X, p.Y);
    internal static Brush ColorBrush(uint argb) => new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));

    internal void Draw(DrawingContext dc, Annotation a)
    {
        var brush = ColorBrush(a.ColorArgb);
        var pen = new Pen(brush, a.Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var rect = BitmapIO.Rect(a.Bounds);
        switch (a.Kind)
        {
            case AnnotationKind.Rectangle: dc.DrawRectangle(null, pen, rect); break;
            case AnnotationKind.Ellipse: dc.DrawEllipse(null, pen, new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2); break;
            case AnnotationKind.Arrow:
                dc.DrawLine(pen, Point(a.Start), Point(a.End));
                var angle = Math.Atan2(a.End.Y - a.Start.Y, a.End.X - a.Start.X);
                var length = 10 + a.Thickness * 2;
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(Point(a.End), true, true);
                    g.LineTo(new Point(a.End.X - length * Math.Cos(angle - .42), a.End.Y - length * Math.Sin(angle - .42)), true, false);
                    g.LineTo(new Point(a.End.X - length * Math.Cos(angle + .42), a.End.Y - length * Math.Sin(angle + .42)), true, false);
                }
                dc.DrawGeometry(brush, null, geometry);
                break;
            case AnnotationKind.Pen:
                if (a.Points.Length == 1) dc.DrawEllipse(brush, null, Point(a.Start), a.Thickness / 2, a.Thickness / 2);
                for (var i = 1; i < a.Points.Length; i++) dc.DrawLine(pen, Point(a.Points[i - 1]), Point(a.Points[i]));
                break;
            case AnnotationKind.Text:
                dc.DrawText(Format(a.Text, a.FontSize, brush), Point(a.Start));
                break;
            case AnnotationKind.Step:
                dc.DrawEllipse(brush, new Pen(Brushes.White, 1.5), Point(a.Start), a.StepRadius, a.StepRadius);
                var number = Format(a.StepNumber.ToString(CultureInfo.InvariantCulture), a.StepRadius * 1.18, Brushes.White, true);
                dc.DrawText(number, new Point(a.Start.X - number.Width / 2, a.Start.Y - number.Height / 2));
                break;
            case AnnotationKind.Mosaic:
                var clipped = a.Bounds.RoundOut().Intersect(new(0, 0, background.PixelWidth, background.PixelHeight));
                if (clipped.IsEmpty) break;
                var block = (int)(6 + a.Thickness * 2);
                if (!_mosaics.TryGetValue(a.Id, out var cached) || cached.Rect != clipped || cached.Block != block)
                {
                    var crop = BitmapIO.Crop(background, clipped);
                    var small = BitmapIO.Resize(crop, Math.Max(1, crop.PixelWidth / block));
                    cached = (clipped, block, small);
                    _mosaics[a.Id] = cached;
                }
                var group = new DrawingGroup();
                using (var g = group.Open()) g.DrawImage(cached.Bitmap, BitmapIO.Rect(clipped));
                RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.NearestNeighbor);
                dc.DrawDrawing(group);
                break;
        }
    }

    internal BitmapSource Render(IEnumerable<Annotation> items)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(background, new Rect(0, 0, background.PixelWidth, background.PixelHeight));
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, background.PixelWidth, background.PixelHeight)));
            foreach (var item in items) Draw(dc, item);
            dc.Pop();
        }
        var output = new RenderTargetBitmap(background.PixelWidth, background.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        output.Render(visual); output.Freeze(); return output;
    }
    internal static FormattedText Format(string text, double size, Brush brush, bool bold = false) => new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, brush, 1);
}
