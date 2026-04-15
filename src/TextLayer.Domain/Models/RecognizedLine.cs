using TextLayer.Domain.Geometry;

namespace TextLayer.Domain.Models;

public sealed record RecognizedLine(
    Guid LineId,
    int Index,
    string Text,
    RectD BoundingRect,
    IReadOnlyList<PointD>? BoundingPolygon,
    IReadOnlyList<Guid> WordIds);
