namespace TextLayer.Domain.Geometry;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public int Right => X + Width;

    public int Bottom => Y + Height;
}
