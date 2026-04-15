using TextLayer.Domain.Geometry;

namespace TextLayer.Domain.Models;

public sealed record RecognizedWord(
    Guid WordId,
    int Index,
    int LineIndex,
    string Text,
    string NormalizedText,
    RectD BoundingRect,
    IReadOnlyList<PointD>? BoundingPolygon,
    double? Confidence);
