using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Domain.Results;

namespace TextLayer.Domain.Services;

public interface ISelectionEngine
{
    HitTestResult HitTest(RecognizedDocument? document, PointD point, double tolerance);

    TextSelection? CreateRangeSelection(RecognizedDocument document, int anchorWordIndex, int activeWordIndex);

    TextSelection CreateFullDocumentSelection(RecognizedDocument document);

    string NormalizeDocumentText(RecognizedDocument document);
}
