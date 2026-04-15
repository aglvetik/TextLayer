using System.Windows;
using TextLayer.App.Models;
using TextLayer.Domain.Services;

namespace TextLayer.App.Views;

public partial class ProcessingOverlayWindow : Window
{
    public ProcessingOverlayWindow(ScreenSelectionResult selection)
    {
        InitializeComponent();

        const double overlayWidthDip = 250d;
        const double overlayHeightDip = 64d;
        const double edgeMarginDip = 10d;

        var mapper = new ScreenOverlayCoordinateMapper();
        var selectionBoundsDip = mapper.ToRelativeDipRect(
            selection.PixelBounds,
            selection.Monitor.PixelBounds,
            selection.Monitor.PixelsPerDipX,
            selection.Monitor.PixelsPerDipY);
        var monitorBoundsDip = mapper.ToDipRect(
            selection.Monitor.PixelBounds,
            selection.Monitor.PixelsPerDipX,
            selection.Monitor.PixelsPerDipY);

        Width = overlayWidthDip;
        Height = overlayHeightDip;

        var preferredLeft = monitorBoundsDip.X + Math.Max(edgeMarginDip, selectionBoundsDip.X);
        var maxLeft = monitorBoundsDip.Right - overlayWidthDip - edgeMarginDip;
        Left = Math.Clamp(preferredLeft, monitorBoundsDip.X + edgeMarginDip, maxLeft);

        var topOutside = monitorBoundsDip.Y + selectionBoundsDip.Y - overlayHeightDip - edgeMarginDip;
        var bottomOutside = monitorBoundsDip.Y + selectionBoundsDip.Bottom + edgeMarginDip;
        var minTop = monitorBoundsDip.Y + edgeMarginDip;
        var maxTop = monitorBoundsDip.Bottom - overlayHeightDip - edgeMarginDip;

        Top = topOutside >= minTop
            ? topOutside
            : bottomOutside <= maxTop
                ? bottomOutside
                : Math.Clamp(monitorBoundsDip.Y + selectionBoundsDip.Y + edgeMarginDip, minTop, maxTop);
    }
}
