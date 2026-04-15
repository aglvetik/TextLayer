namespace TextLayer.App.Models;

public sealed record ScreenCaptureSnapshot(
    string SourcePath,
    ScreenSelectionResult Selection);
