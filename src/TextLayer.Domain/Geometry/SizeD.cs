namespace TextLayer.Domain.Geometry;

public readonly record struct SizeD(double Width, double Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
