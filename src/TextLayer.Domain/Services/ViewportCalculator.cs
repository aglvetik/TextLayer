using TextLayer.Domain.Enums;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;

namespace TextLayer.Domain.Services;

public sealed class ViewportCalculator
{
    public ViewportState CreateFitToWindow(SizeD viewportSize, SizeD imageSize)
        => new(GetFitZoom(viewportSize, imageSize), 0d, 0d, FitMode.FitToWindow);

    public ViewportState CreateActualSize() => ViewportState.ActualSize;

    public double GetFitZoom(SizeD viewportSize, SizeD imageSize)
    {
        if (viewportSize.IsEmpty || imageSize.IsEmpty)
        {
            return 1d;
        }

        return Math.Min(viewportSize.Width / imageSize.Width, viewportSize.Height / imageSize.Height);
    }

    public PointD ToImageSpace(PointD viewportPoint, SizeD viewportSize, SizeD imageSize, ViewportState state)
    {
        var offset = GetImageOffset(viewportSize, imageSize, state);
        return new PointD(
            (viewportPoint.X - offset.X) / Math.Max(state.Zoom, 0.0001d),
            (viewportPoint.Y - offset.Y) / Math.Max(state.Zoom, 0.0001d));
    }

    public PointD ToViewportSpace(PointD imagePoint, SizeD viewportSize, SizeD imageSize, ViewportState state)
    {
        var offset = GetImageOffset(viewportSize, imageSize, state);
        return new PointD(
            offset.X + (imagePoint.X * state.Zoom),
            offset.Y + (imagePoint.Y * state.Zoom));
    }

    public PointD GetImageOffset(SizeD viewportSize, SizeD imageSize, ViewportState state)
    {
        var scaledWidth = imageSize.Width * state.Zoom;
        var scaledHeight = imageSize.Height * state.Zoom;
        var centeredX = (viewportSize.Width - scaledWidth) / 2d;
        var centeredY = (viewportSize.Height - scaledHeight) / 2d;
        return new PointD(centeredX + state.PanX, centeredY + state.PanY);
    }

    public ViewportState PanBy(ViewportState state, double deltaX, double deltaY)
        => state with
        {
            PanX = state.PanX + deltaX,
            PanY = state.PanY + deltaY,
            FitMode = FitMode.Custom,
        };

    public ViewportState ZoomAroundPoint(
        ViewportState state,
        SizeD viewportSize,
        SizeD imageSize,
        PointD anchorViewportPoint,
        double zoomFactor,
        double minZoom = 0.05d,
        double maxZoom = 12d)
    {
        if (viewportSize.IsEmpty || imageSize.IsEmpty)
        {
            return state;
        }

        var imagePoint = ToImageSpace(anchorViewportPoint, viewportSize, imageSize, state);
        var newZoom = Math.Clamp(state.Zoom * zoomFactor, minZoom, maxZoom);
        var centeredX = (viewportSize.Width - (imageSize.Width * newZoom)) / 2d;
        var centeredY = (viewportSize.Height - (imageSize.Height * newZoom)) / 2d;

        return state with
        {
            Zoom = newZoom,
            PanX = anchorViewportPoint.X - centeredX - (imagePoint.X * newZoom),
            PanY = anchorViewportPoint.Y - centeredY - (imagePoint.Y * newZoom),
            FitMode = FitMode.Custom,
        };
    }
}
