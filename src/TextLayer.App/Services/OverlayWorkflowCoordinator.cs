using System.Windows.Forms;
using TextLayer.App.Models;
using TextLayer.Application.Models;
using TextLayer.Application.UseCases;
using TextLayer.Domain.Models;
using ToolTipIcon = System.Windows.Forms.ToolTipIcon;

namespace TextLayer.App.Services;

public sealed class OverlayWorkflowCoordinator(
    RegionSelectionService regionSelectionService,
    ActiveWindowCaptureService activeWindowCaptureService,
    ScreenCaptureService screenCaptureService,
    OverlayWindowManager overlayWindowManager,
    ImageDocumentUseCase imageDocumentUseCase,
    Func<AppSettings> settingsAccessor,
    Action<string, string, ToolTipIcon> notificationSink,
    TextLayer.Application.Abstractions.ILogService logService)
{
    private CancellationTokenSource? currentOperation;

    public void StartRegionCapture() => _ = StartRegionCaptureAsync();

    public void StartActiveWindowCapture() => _ = StartActiveWindowCaptureAsync();

    public void CloseOverlay()
    {
        currentOperation?.Cancel();
        overlayWindowManager.CloseProcessing();
        overlayWindowManager.CloseActiveOverlay();
    }

    private async Task StartRegionCaptureAsync()
    {
        using var scope = BeginOperation();

        try
        {
            logService.Info("Starting OCR region capture.");
            overlayWindowManager.CloseActiveOverlay();
            var sourceWindowHandle = activeWindowCaptureService.GetForegroundWindowHandle();
            var selection = await regionSelectionService.SelectRegionAsync(scope.Token);
            if (selection is null)
            {
                logService.Info("OCR region capture cancelled.");
                return;
            }

            selection = selection with { SourceWindowHandle = sourceWindowHandle };
            logService.Info($"OCR region selected at {selection.PixelBounds.X},{selection.PixelBounds.Y} {selection.PixelBounds.Width}x{selection.PixelBounds.Height}.");
            await ProcessSelectionAsync(selection, scope.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logService.Error("Failed to create an OCR overlay from a captured region.", exception);
            notificationSink(UiTextService.Instance["Notification.OcrTitle"], exception.Message, ToolTipIcon.Error);
        }
    }

    private async Task StartActiveWindowCaptureAsync()
    {
        using var scope = BeginOperation();

        try
        {
            logService.Info("Starting OCR active-window capture.");
            overlayWindowManager.CloseActiveOverlay();
            var selection = await activeWindowCaptureService.CaptureForegroundWindowBoundsAsync(scope.Token);
            if (selection is null)
            {
                notificationSink(
                    UiTextService.Instance["Notification.OcrTitle"],
                    UiTextService.Instance["Notification.ActiveWindowMissing"],
                    ToolTipIcon.Info);
                return;
            }

            await ProcessSelectionAsync(selection, scope.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logService.Error("Failed to create an OCR overlay from the active window.", exception);
            notificationSink(UiTextService.Instance["Notification.OcrTitle"], exception.Message, ToolTipIcon.Error);
        }
    }

    private async Task ProcessSelectionAsync(ScreenSelectionResult selection, CancellationToken cancellationToken)
    {
        var snapshot = await screenCaptureService.CaptureRegionAsync(selection, cancellationToken);
        var settings = settingsAccessor();
        overlayWindowManager.ShowProcessing(selection);

        try
        {
            var document = await imageDocumentUseCase.RecognizeAsync(
                snapshot.SourcePath,
                new OcrRequestOptions(settings.OcrMode, settings.OcrLanguageMode),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (document.Words.Count == 0)
            {
                notificationSink(
                    UiTextService.Instance["Notification.OcrTitle"],
                    UiTextService.Instance["Notification.NoTextFound"],
                    ToolTipIcon.Info);
                return;
            }

            overlayWindowManager.ShowOverlay(document, selection, showDebugBounds: false, settings.CloseOverlayAfterCopy);
        }
        finally
        {
            overlayWindowManager.CloseProcessing();
        }
    }

    private OperationScope BeginOperation()
    {
        var previousOperation = currentOperation;
        currentOperation = new CancellationTokenSource();

        previousOperation?.Cancel();
        previousOperation?.Dispose();

        return new OperationScope(this, currentOperation);
    }

    private sealed class OperationScope(OverlayWorkflowCoordinator owner, CancellationTokenSource source) : IDisposable
    {
        public CancellationToken Token => source.Token;

        public void Dispose()
        {
            if (ReferenceEquals(owner.currentOperation, source))
            {
                owner.currentOperation = null;
            }

            source.Dispose();
        }
    }
}
