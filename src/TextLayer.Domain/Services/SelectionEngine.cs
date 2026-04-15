using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Domain.Results;

namespace TextLayer.Domain.Services;

public sealed class SelectionEngine(TextNormalizer normalizer) : ISelectionEngine
{
    public HitTestResult HitTest(RecognizedDocument? document, PointD point, double tolerance)
    {
        if (document is null || document.Words.Count == 0)
        {
            return new HitTestResult(null, false, double.PositiveInfinity);
        }

        RecognizedWord? closest = null;
        var closestDistance = double.PositiveInfinity;

        foreach (var word in document.Words)
        {
            var distance = word.BoundingRect.DistanceTo(point);
            if (distance < closestDistance || (Math.Abs(distance - closestDistance) < 0.001d && word.Index < closest?.Index))
            {
                closest = word;
                closestDistance = distance;
            }
        }

        return closest is null
            ? new HitTestResult(null, false, double.PositiveInfinity)
            : new HitTestResult(closest, closestDistance <= tolerance, closestDistance);
    }

    public TextSelection? CreateRangeSelection(RecognizedDocument document, int anchorWordIndex, int activeWordIndex)
    {
        if (document.Words.Count == 0)
        {
            return null;
        }

        var startIndex = Math.Min(anchorWordIndex, activeWordIndex);
        var endIndex = Math.Max(anchorWordIndex, activeWordIndex);
        var selectedWords = document.Words
            .Where(word => word.Index >= startIndex && word.Index <= endIndex)
            .OrderBy(word => word.Index)
            .ToArray();

        if (selectedWords.Length == 0)
        {
            return null;
        }

        return new TextSelection(
            StartWordIndex: startIndex,
            EndWordIndex: endIndex,
            SelectedWordIds: selectedWords.Select(word => word.WordId).ToArray(),
            SelectedRects: selectedWords.Select(word => word.BoundingRect).ToArray(),
            SelectedText: normalizer.NormalizeSelection(selectedWords));
    }

    public TextSelection CreateFullDocumentSelection(RecognizedDocument document)
        => new(
            StartWordIndex: document.Words[0].Index,
            EndWordIndex: document.Words[^1].Index,
            SelectedWordIds: document.Words.Select(word => word.WordId).ToArray(),
            SelectedRects: document.Words.Select(word => word.BoundingRect).ToArray(),
            SelectedText: normalizer.NormalizeSelection(document.Words));

    public string NormalizeDocumentText(RecognizedDocument document) => normalizer.NormalizeDocument(document);
}
