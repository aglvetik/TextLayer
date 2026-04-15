using TextLayer.Domain.Geometry;

namespace TextLayer.App.Models;

public sealed record ScreenSelectionResult(
    PixelRect PixelBounds,
    MonitorInfo Monitor,
    nint SourceWindowHandle = 0);
