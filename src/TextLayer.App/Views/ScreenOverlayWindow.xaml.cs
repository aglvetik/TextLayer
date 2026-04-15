using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using TextLayer.App.Models;
using TextLayer.Application.Abstractions;
using TextLayer.Domain.Models;
using TextLayer.Domain.Services;

namespace TextLayer.App.Views;

using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using VerticalAlignment = System.Windows.VerticalAlignment;

public partial class ScreenOverlayWindow : Window
{
    private const double ActionBarReservedHeightDip = 46d;
    private const double ActionBarReservedWidthDip = 332d;
    private readonly IClipboardService clipboardService;
    private readonly RecognizedDocument document;
    private readonly bool closeAfterCopy;
    private readonly nint sourceWindowHandle;
    private readonly double pixelsPerDipX;
    private readonly double pixelsPerDipY;
    private readonly DispatcherTimer lifecycleTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private TextSelection? currentSelection;
    private nint overlayWindowHandle;
    private bool allowDeactivateClose;

    public ScreenOverlayWindow(
        RecognizedDocument document,
        ScreenSelectionResult selection,
        IClipboardService clipboardService,
        bool showDebugBounds,
        bool closeAfterCopy)
    {
        InitializeComponent();
        this.document = document;
        this.clipboardService = clipboardService;
        this.closeAfterCopy = closeAfterCopy;
        sourceWindowHandle = selection.SourceWindowHandle;
        pixelsPerDipX = selection.Monitor.PixelsPerDipX;
        pixelsPerDipY = selection.Monitor.PixelsPerDipY;

        var coordinateMapper = new ScreenOverlayCoordinateMapper();
        var boundsDip = coordinateMapper.ToDipRect(
            selection.PixelBounds,
            pixelsPerDipX,
            pixelsPerDipY);

        Left = boundsDip.X;
        Top = boundsDip.Y;
        Width = boundsDip.Width;
        Height = boundsDip.Height;

        OverlayControl.Document = document;
        OverlayControl.PixelsPerDipX = pixelsPerDipX;
        OverlayControl.PixelsPerDipY = pixelsPerDipY;
        OverlayControl.ShowDebugBounds = showDebugBounds;
        OverlayControl.SelectionChanged += (_, activeSelection) =>
        {
            currentSelection = activeSelection;
            UpdateCopyButtonState();
        };

        ApplyActionBarPlacement();
        SourceInitialized += (_, _) => overlayWindowHandle = new WindowInteropHelper(this).Handle;
        Loaded += (_, _) =>
        {
            Activate();
            OverlayControl.Focus();
            Dispatcher.BeginInvoke(
                () => allowDeactivateClose = true,
                DispatcherPriority.ApplicationIdle);
        };
        Deactivated += (_, _) =>
        {
            if (!allowDeactivateClose)
            {
                return;
            }

            Dispatcher.BeginInvoke(Close, DispatcherPriority.Background);
        };
        lifecycleTimer.Tick += LifecycleTimer_OnTick;
        if (sourceWindowHandle != 0)
        {
            lifecycleTimer.Start();
        }

        Closed += (_, _) => lifecycleTimer.Stop();
        UpdateCopyButtonState();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.A)
        {
            OverlayControl.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.C && currentSelection is not null && !currentSelection.IsEmpty)
        {
            _ = CopySelectionAsync(currentSelection.SelectedText);
            e.Handled = true;
            return;
        }
    }

    private void SelectAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        OverlayControl.SelectAll();
        OverlayControl.Focus();
    }

    private async void CopyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (currentSelection is null || currentSelection.IsEmpty)
        {
            OverlayControl.SelectAll();
        }

        if (currentSelection is not null && !currentSelection.IsEmpty)
        {
            await CopySelectionAsync(currentSelection.SelectedText);
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private async Task CopySelectionAsync(string selectionText)
    {
        await clipboardService.CopyTextAsync(selectionText, CancellationToken.None);
        if (closeAfterCopy)
        {
            Close();
            return;
        }

        OverlayControl.Focus();
    }

    private void UpdateCopyButtonState()
        => CopyButton.IsEnabled = currentSelection is not null && !currentSelection.IsEmpty;

    private void ApplyActionBarPlacement()
    {
        var rightBandLeft = document.ImagePixelWidth - (ActionBarReservedWidthDip * pixelsPerDipX) - (12d * pixelsPerDipX);
        var topBandBottom = ActionBarReservedHeightDip * pixelsPerDipY;
        var bottomBandTop = document.ImagePixelHeight - (ActionBarReservedHeightDip * pixelsPerDipY);

        var topHasText = document.Lines.Any(line =>
            line.BoundingRect.Right >= rightBandLeft
            && line.BoundingRect.Top <= topBandBottom);
        var bottomHasText = document.Lines.Any(line =>
            line.BoundingRect.Right >= rightBandLeft
            && line.BoundingRect.Bottom >= bottomBandTop);

        ActionBarBorder.VerticalAlignment = topHasText && !bottomHasText
            ? VerticalAlignment.Bottom
            : VerticalAlignment.Top;
    }

    private void LifecycleTimer_OnTick(object? sender, EventArgs e)
    {
        if (sourceWindowHandle == 0)
        {
            return;
        }

        if (!NativeMethods.IsWindow(sourceWindowHandle)
            || !NativeMethods.IsWindowVisible(sourceWindowHandle)
            || NativeMethods.IsIconic(sourceWindowHandle))
        {
            Close();
            return;
        }

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (allowDeactivateClose
            && foregroundWindow != 0
            && overlayWindowHandle != 0
            && foregroundWindow != overlayWindowHandle
            && foregroundWindow != sourceWindowHandle)
        {
            Close();
        }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool IsWindow(nint hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(nint hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool IsIconic(nint hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern nint GetForegroundWindow();
    }
}
