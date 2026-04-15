using TextLayer.Domain.Geometry;

namespace TextLayer.Domain.Models;

public sealed record TextSelection(
    int StartWordIndex,
    int EndWordIndex,
    IReadOnlyList<Guid> SelectedWordIds,
    IReadOnlyList<RectD> SelectedRects,
    string SelectedText)
{
    public int SelectedWordCount => SelectedWordIds.Count;

    public bool IsEmpty => SelectedWordIds.Count == 0;
}
