using TextLayer.Domain.Geometry;

namespace TextLayer.App.Models;

public sealed record MonitorInfo(
    string DeviceName,
    PixelRect PixelBounds,
    double PixelsPerDipX,
    double PixelsPerDipY);
