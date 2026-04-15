using TextLayer.Domain.Geometry;

namespace TextLayer.Domain.Services;

public sealed class ScreenOverlayCoordinateMapper
{
    public RectD ToDipRect(PixelRect pixelRect, double pixelsPerDipX, double pixelsPerDipY)
        => new(
            pixelRect.X / Math.Max(pixelsPerDipX, 0.0001d),
            pixelRect.Y / Math.Max(pixelsPerDipY, 0.0001d),
            pixelRect.Width / Math.Max(pixelsPerDipX, 0.0001d),
            pixelRect.Height / Math.Max(pixelsPerDipY, 0.0001d));

    public RectD ToRelativeDipRect(PixelRect pixelRect, PixelRect containerRect, double pixelsPerDipX, double pixelsPerDipY)
        => new(
            (pixelRect.X - containerRect.X) / Math.Max(pixelsPerDipX, 0.0001d),
            (pixelRect.Y - containerRect.Y) / Math.Max(pixelsPerDipY, 0.0001d),
            pixelRect.Width / Math.Max(pixelsPerDipX, 0.0001d),
            pixelRect.Height / Math.Max(pixelsPerDipY, 0.0001d));

    public PixelRect CreatePixelRect(PointD startDip, PointD endDip, PixelRect containerRect, double pixelsPerDipX, double pixelsPerDipY)
    {
        var leftDip = Math.Min(startDip.X, endDip.X);
        var topDip = Math.Min(startDip.Y, endDip.Y);
        var rightDip = Math.Max(startDip.X, endDip.X);
        var bottomDip = Math.Max(startDip.Y, endDip.Y);

        var leftPx = containerRect.X + (int)Math.Floor(leftDip * pixelsPerDipX);
        var topPx = containerRect.Y + (int)Math.Floor(topDip * pixelsPerDipY);
        var rightPx = containerRect.X + (int)Math.Ceiling(rightDip * pixelsPerDipX);
        var bottomPx = containerRect.Y + (int)Math.Ceiling(bottomDip * pixelsPerDipY);

        leftPx = Math.Clamp(leftPx, containerRect.X, containerRect.Right);
        topPx = Math.Clamp(topPx, containerRect.Y, containerRect.Bottom);
        rightPx = Math.Clamp(rightPx, containerRect.X, containerRect.Right);
        bottomPx = Math.Clamp(bottomPx, containerRect.Y, containerRect.Bottom);

        return new PixelRect(
            leftPx,
            topPx,
            Math.Max(0, rightPx - leftPx),
            Math.Max(0, bottomPx - topPx));
    }

    public PointD ToImageSpace(PointD overlayDipPoint, double pixelsPerDipX, double pixelsPerDipY)
        => new(
            overlayDipPoint.X * pixelsPerDipX,
            overlayDipPoint.Y * pixelsPerDipY);
}
