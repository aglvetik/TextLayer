using System.Windows;
using System.Windows.Media;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;

namespace TextLayer.App.Controls;

using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;

public sealed class OcrOverlayControl : FrameworkElement
{
    public RecognizedDocument? Document
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    public TextSelection? Selection
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    public RecognizedWord? HoveredWord
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    public bool ShowDebugBounds
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (Document is null)
        {
            return;
        }

        var debugPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 26, 188, 156)), 1d);
        debugPen.Freeze();

        if (Selection is not null && !Selection.IsEmpty)
        {
            var fillBrush = new SolidColorBrush(Color.FromArgb(112, 47, 111, 237));
            fillBrush.Freeze();
            var strokePen = new Pen(new SolidColorBrush(Color.FromArgb(214, 47, 111, 237)), 1.2d);
            strokePen.Freeze();

            foreach (var rect in GetSelectionVisualRects(Document, Selection))
            {
                drawingContext.DrawRoundedRectangle(fillBrush, strokePen, ToRect(rect), 2d, 2d);
            }
        }

        if (!ShowDebugBounds)
        {
            return;
        }

        foreach (var word in Document.Words)
        {
            drawingContext.DrawRectangle(null, debugPen, ToRect(word.BoundingRect));
        }
    }

    private static Rect ToRect(TextLayer.Domain.Geometry.RectD rect)
        => new(rect.X, rect.Y, rect.Width, rect.Height);

    private static IReadOnlyList<RectD> GetSelectionVisualRects(RecognizedDocument document, TextSelection selection)
    {
        var selectedWordIds = selection.SelectedWordIds.ToHashSet();
        var selectedWords = document.Words
            .Where(word => selectedWordIds.Contains(word.WordId))
            .OrderBy(word => word.LineIndex)
            .ThenBy(word => word.Index)
            .ToArray();

        if (selectedWords.Length == 0)
        {
            return selection.SelectedRects;
        }

        var mergedRects = new List<RectD>();
        foreach (var lineGroup in selectedWords.GroupBy(word => word.LineIndex).OrderBy(group => group.Key))
        {
            RectD? currentRect = null;
            foreach (var word in lineGroup.OrderBy(word => word.Index))
            {
                var rect = word.BoundingRect;
                if (currentRect is null)
                {
                    currentRect = rect;
                    continue;
                }

                var lineGapTolerance = Math.Max(10d, Math.Min(currentRect.Value.Height, rect.Height) * 0.9d);
                if (rect.Left - currentRect.Value.Right <= lineGapTolerance)
                {
                    var left = Math.Min(currentRect.Value.Left, rect.Left);
                    var top = Math.Min(currentRect.Value.Top, rect.Top);
                    var right = Math.Max(currentRect.Value.Right, rect.Right);
                    var bottom = Math.Max(currentRect.Value.Bottom, rect.Bottom);
                    currentRect = new RectD(left, top, right - left, bottom - top);
                    continue;
                }

                mergedRects.Add(currentRect.Value);
                currentRect = rect;
            }

            if (currentRect is not null)
            {
                mergedRects.Add(currentRect.Value);
            }
        }

        return mergedRects.Count == 0 ? selection.SelectedRects : mergedRects;
    }
}
