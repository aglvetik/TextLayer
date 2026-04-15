namespace TextLayer.Application.Models;

public sealed record WindowPlacementSettings(
    double Left = 120d,
    double Top = 100d,
    double Width = 1360d,
    double Height = 860d,
    bool IsMaximized = false);
