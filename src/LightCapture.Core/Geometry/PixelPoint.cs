namespace LightCapture.Core.Geometry;

public readonly record struct PixelPoint(double X, double Y)
{
    public double DistanceTo(PixelPoint other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
    public PixelPoint Offset(double dx, double dy) => new(X + dx, Y + dy);
}
