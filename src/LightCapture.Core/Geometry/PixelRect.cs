namespace LightCapture.Core.Geometry;

/// <summary>以物理像素表示的矩形。桌面坐标允许负数，避免混合 DPI 时把 DIP 当像素。</summary>
public readonly record struct PixelRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public PixelPoint TopLeft => new(X, Y);
    public PixelPoint Center => new(X + Width / 2, Y + Height / 2);

    public bool Contains(PixelPoint point, double padding = 0) =>
        point.X >= X - padding && point.X <= Right + padding &&
        point.Y >= Y - padding && point.Y <= Bottom + padding;

    public PixelRect Intersect(PixelRect other)
    {
        var x = Math.Max(X, other.X);
        var y = Math.Max(Y, other.Y);
        return new(x, y, Math.Max(0, Math.Min(Right, other.Right) - x), Math.Max(0, Math.Min(Bottom, other.Bottom) - y));
    }

    public PixelRect Offset(double dx, double dy) => new(X + dx, Y + dy, Width, Height);
    public PixelRect Inflate(double amount) => new(X - amount, Y - amount, Width + 2 * amount, Height + 2 * amount);

    public static PixelRect FromPoints(PixelPoint a, PixelPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    public PixelRect RoundOut() => new(Math.Floor(X), Math.Floor(Y), Math.Ceiling(Right) - Math.Floor(X), Math.Ceiling(Bottom) - Math.Floor(Y));
}
