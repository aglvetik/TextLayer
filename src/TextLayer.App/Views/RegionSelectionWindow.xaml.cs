using System.Windows;
using TextLayer.App.Models;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Services;

namespace TextLayer.App.Views;

using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;
using Canvas = System.Windows.Controls.Canvas;

public partial class RegionSelectionWindow : Window
{
    private readonly ScreenSelectionResult monitorSelection;
    private readonly ScreenOverlayCoordinateMapper coordinateMapper = new();
    private Point dragStartDip;
    private bool isDragging;

    public RegionSelectionWindow(MonitorInfo monitor)
    {
        InitializeComponent();
        monitorSelection = new ScreenSelectionResult(monitor.PixelBounds, monitor);

        var boundsDip = coordinateMapper.ToDipRect(monitor.PixelBounds, monitor.PixelsPerDipX, monitor.PixelsPerDipY);
        Left = boundsDip.X;
        Top = boundsDip.Y;
        Width = boundsDip.Width;
        Height = boundsDip.Height;

        Loaded += (_, _) => Focus();
    }

    public event EventHandler<ScreenSelectionResult>? SelectionCompleted;

    public event EventHandler? Cancelled;

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        dragStartDip = e.GetPosition(SelectionCanvas);
        isDragging = true;
        CaptureMouse();
        UpdateSelectionVisual(dragStartDip, dragStartDip);
        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (!isDragging)
        {
            return;
        }

        UpdateSelectionVisual(dragStartDip, e.GetPosition(SelectionCanvas));
        e.Handled = true;
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (!isDragging)
        {
            return;
        }

        ReleaseMouseCapture();
        isDragging = false;

        var selection = coordinateMapper.CreatePixelRect(
            new PointD(dragStartDip.X, dragStartDip.Y),
            new PointD(e.GetPosition(SelectionCanvas).X, e.GetPosition(SelectionCanvas).Y),
            monitorSelection.Monitor.PixelBounds,
            monitorSelection.Monitor.PixelsPerDipX,
            monitorSelection.Monitor.PixelsPerDipY);

        if (selection.Width < 8 || selection.Height < 8)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        SelectionCompleted?.Invoke(this, new ScreenSelectionResult(selection, monitorSelection.Monitor));
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void UpdateSelectionVisual(Point startDip, Point currentDip)
    {
        var left = Math.Min(startDip.X, currentDip.X);
        var top = Math.Min(startDip.Y, currentDip.Y);
        var width = Math.Abs(currentDip.X - startDip.X);
        var height = Math.Abs(currentDip.Y - startDip.Y);

        Canvas.SetLeft(SelectionBorder, left);
        Canvas.SetTop(SelectionBorder, top);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
        SelectionBorder.Visibility = Visibility.Visible;
    }
}
