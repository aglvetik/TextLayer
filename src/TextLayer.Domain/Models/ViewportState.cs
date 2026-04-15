using TextLayer.Domain.Enums;

namespace TextLayer.Domain.Models;

public sealed record ViewportState(double Zoom, double PanX, double PanY, FitMode FitMode)
{
    public static ViewportState FitToWindow => new(1d, 0d, 0d, Enums.FitMode.FitToWindow);

    public static ViewportState ActualSize => new(1d, 0d, 0d, Enums.FitMode.ActualSize);
}
