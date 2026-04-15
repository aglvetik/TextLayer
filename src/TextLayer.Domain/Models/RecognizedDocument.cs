namespace TextLayer.Domain.Models;

public sealed record RecognizedDocument(
    Guid DocumentId,
    string SourcePath,
    int ImagePixelWidth,
    int ImagePixelHeight,
    string FullText,
    IReadOnlyList<RecognizedLine> Lines,
    IReadOnlyList<RecognizedWord> Words,
    DateTime CreatedAtUtc,
    long RecognitionDurationMs,
    string OcrEngineId,
    string? LanguageHint);
