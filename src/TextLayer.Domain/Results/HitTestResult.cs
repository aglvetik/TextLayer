using TextLayer.Domain.Models;

namespace TextLayer.Domain.Results;

public sealed record HitTestResult(RecognizedWord? Word, bool IsHit, double Distance);
