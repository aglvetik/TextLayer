using TextLayer.Domain.Enums;
using TextLayer.Domain.Geometry;

namespace TextLayer.Domain.Services;

public sealed class PointerInteractionClassifier
{
    public bool IsClick(PointD origin, PointD current, double dragThreshold)
        => GetDistance(origin, current) <= dragThreshold;

    public PointerDragIntent ClassifyDragIntent(
        PointD origin,
        PointD current,
        bool startedOverText,
        bool panRequested,
        double dragThreshold)
    {
        if (panRequested && GetDistance(origin, current) > dragThreshold)
        {
            return PointerDragIntent.Pan;
        }

        if (startedOverText && GetDistance(origin, current) > dragThreshold)
        {
            return PointerDragIntent.TextSelection;
        }

        return PointerDragIntent.None;
    }

    private static double GetDistance(PointD origin, PointD current)
    {
        var dx = current.X - origin.X;
        var dy = current.Y - origin.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
