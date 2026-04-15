using System.Windows;
using TextLayer.App.Models;
using TextLayer.App.Views;
using TextLayer.Application.Abstractions;
using TextLayer.Domain.Models;

namespace TextLayer.App.Services;

public sealed class OverlayWindowManager(IClipboardService clipboardService)
{
    private ScreenOverlayWindow? activeOverlayWindow;
    private ProcessingOverlayWindow? processingOverlayWindow;

    public void ShowProcessing(ScreenSelectionResult selection)
    {
        CloseProcessing();
        processingOverlayWindow = new ProcessingOverlayWindow(selection);
        processingOverlayWindow.Show();
    }

    public void ShowOverlay(RecognizedDocument document, ScreenSelectionResult selection, bool showDebugBounds, bool closeAfterCopy)
    {
        CloseActiveOverlay();
        CloseProcessing();

        activeOverlayWindow = new ScreenOverlayWindow(document, selection, clipboardService, showDebugBounds, closeAfterCopy);
        activeOverlayWindow.Closed += ActiveOverlayWindow_OnClosed;
        activeOverlayWindow.Show();
    }

    public void CloseActiveOverlay()
    {
        if (activeOverlayWindow is null)
        {
            return;
        }

        activeOverlayWindow.Closed -= ActiveOverlayWindow_OnClosed;
        activeOverlayWindow.Close();
        activeOverlayWindow = null;
    }

    public void CloseProcessing()
    {
        if (processingOverlayWindow is null)
        {
            return;
        }

        processingOverlayWindow.Close();
        processingOverlayWindow = null;
    }

    private void ActiveOverlayWindow_OnClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, activeOverlayWindow))
        {
            activeOverlayWindow = null;
        }
    }
}
