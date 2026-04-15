namespace TextLayer.Domain.Geometry;

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Left => X;

    public double Top => Y;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public PointD Center => new(X + (Width / 2d), Y + (Height / 2d));

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(PointD point)
        => point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    public RectD Inflate(double amount)
        => new(X - amount, Y - amount, Width + (amount * 2d), Height + (amount * 2d));

    public double DistanceTo(PointD point)
    {
        var dx = Math.Max(Math.Max(Left - point.X, 0d), point.X - Right);
        var dy = Math.Max(Math.Max(Top - point.Y, 0d), point.Y - Bottom);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
