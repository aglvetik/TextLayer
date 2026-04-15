using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TextLayer.Domain.Enums;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Domain.Services;

namespace TextLayer.App.Controls;

using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using Mouse = System.Windows.Input.Mouse;
using MouseButton = System.Windows.Input.MouseButton;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;
using Cursors = System.Windows.Input.Cursors;

public partial class ImageViewerControl : UserControl
{
    private const double DragThreshold = 5d;
    private readonly ISelectionEngine selectionEngine = new SelectionEngine(new TextNormalizer());
    private readonly PointerInteractionClassifier interactionClassifier = new();
    private readonly ViewportCalculator viewportCalculator = new();
    private ViewportState viewportState = ViewportState.FitToWindow;
    private TextSelection? currentSelection;
    private RecognizedWord? hoveredWord;
    private Point pointerDownViewportPoint;
    private RecognizedWord? pointerDownWord;
    private bool leftButtonPressed;
    private bool isSelecting;
    private bool isPanning;
    private Point lastPanViewportPoint;

    public ImageViewerControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ApplyTransform();
        Loaded += (_, _) => ApplyDocumentState();
    }

    public event EventHandler<TextSelection?>? SelectionChanged;

    public event EventHandler<RecognizedWord?>? HoverWordChanged;

    public event EventHandler<double>? ZoomChanged;

    public event EventHandler? CopyRequested;

    public static readonly DependencyProperty DisplayImageSourceProperty =
        DependencyProperty.Register(
            nameof(DisplayImageSource),
            typeof(ImageSource),
            typeof(ImageViewerControl),
            new PropertyMetadata(null, OnDisplayImageChanged));

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(
            nameof(Document),
            typeof(RecognizedDocument),
            typeof(ImageViewerControl),
            new PropertyMetadata(null, OnDocumentChanged));

    public static readonly DependencyProperty ShowDebugBoundsProperty =
        DependencyProperty.Register(
            nameof(ShowDebugBounds),
            typeof(bool),
            typeof(ImageViewerControl),
            new PropertyMetadata(false, OnShowDebugBoundsChanged));

    public ImageSource? DisplayImageSource
    {
        get => (ImageSource?)GetValue(DisplayImageSourceProperty);
        set => SetValue(DisplayImageSourceProperty, value);
    }

    public RecognizedDocument? Document
    {
        get => (RecognizedDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public bool ShowDebugBounds
    {
        get => (bool)GetValue(ShowDebugBoundsProperty);
        set => SetValue(ShowDebugBoundsProperty, value);
    }

    public double CurrentZoom => viewportState.Zoom;

    public void FitToWindow()
    {
        if (TryGetImageSize(out var imageSize))
        {
            viewportState = viewportCalculator.CreateFitToWindow(GetViewportSize(), imageSize);
            ApplyTransform();
        }
    }

    public void ActualSize()
    {
        viewportState = viewportCalculator.CreateActualSize();
        ApplyTransform();
    }

    public void ResetView() => FitToWindow();

    public void ZoomIn() => ZoomBy(1.15d);

    public void ZoomOut() => ZoomBy(1d / 1.15d);

    public void SelectAll()
    {
        if (Document is null || Document.Words.Count == 0)
        {
            return;
        }

        UpdateSelection(selectionEngine.CreateFullDocumentSelection(Document));
    }

    public void ClearSelection()
    {
        UpdateSelection(null);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.A)
        {
            SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.C && currentSelection is not null && !currentSelection.IsEmpty)
        {
            CopyRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (!TryGetImageSize(out var imageSize))
        {
            return;
        }

        var zoomFactor = e.Delta > 0 ? 1.12d : 1d / 1.12d;
        viewportState = viewportCalculator.ZoomAroundPoint(
            viewportState,
            GetViewportSize(),
            imageSize,
            new PointD(e.GetPosition(ViewportHost).X, e.GetPosition(ViewportHost).Y),
            zoomFactor);

        ApplyTransform();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Focus();

        var viewportPoint = e.GetPosition(ViewportHost);

        if (isPanning)
        {
            var delta = viewportPoint - lastPanViewportPoint;
            lastPanViewportPoint = viewportPoint;
            viewportState = viewportCalculator.PanBy(viewportState, delta.X, delta.Y);
            Cursor = Cursors.SizeAll;
            ApplyTransform();
            return;
        }

        UpdateHoverWord(viewportPoint);

        if (!leftButtonPressed || pointerDownWord is null || Document is null)
        {
            return;
        }

        var intent = interactionClassifier.ClassifyDragIntent(
            new PointD(pointerDownViewportPoint.X, pointerDownViewportPoint.Y),
            new PointD(viewportPoint.X, viewportPoint.Y),
            startedOverText: true,
            panRequested: false,
            dragThreshold: DragThreshold);

        if (!isSelecting && intent == PointerDragIntent.TextSelection)
        {
            isSelecting = true;
            Mouse.Capture(this);
        }

        if (!isSelecting)
        {
            return;
        }

        var activeWord = hoveredWord ?? pointerDownWord;
        var selection = selectionEngine.CreateRangeSelection(Document, pointerDownWord.Index, activeWord.Index);
        UpdateSelection(selection);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!leftButtonPressed && !isPanning)
        {
            SetHoverWord(null);
            Cursor = Cursors.Arrow;
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();

        var viewportPoint = e.GetPosition(ViewportHost);
        if (Keyboard.IsKeyDown(Key.Space))
        {
            StartPan(viewportPoint);
            e.Handled = true;
            return;
        }

        leftButtonPressed = true;
        pointerDownViewportPoint = viewportPoint;
        pointerDownWord = GetHitTestWord(viewportPoint);

        if (pointerDownWord is null)
        {
            ClearSelection();
        }

        Mouse.Capture(this);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (isPanning)
        {
            EndPan();
            e.Handled = true;
            return;
        }

        leftButtonPressed = false;
        isSelecting = false;
        pointerDownWord = null;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Middle)
        {
            StartPan(e.GetPosition(ViewportHost));
            e.Handled = true;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton == MouseButton.Middle && isPanning)
        {
            EndPan();
            e.Handled = true;
        }
    }

    private static void OnDisplayImageChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        => ((ImageViewerControl)dependencyObject).ApplyDocumentState();

    private static void OnDocumentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        => ((ImageViewerControl)dependencyObject).ApplyDocumentState();

    private static void OnShowDebugBoundsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        => ((ImageViewerControl)dependencyObject).OverlayElement.ShowDebugBounds = (bool)e.NewValue;

    private void ApplyDocumentState()
    {
        ImageElement.Source = DisplayImageSource;
        OverlayElement.Document = Document;
        OverlayElement.ShowDebugBounds = ShowDebugBounds;
        OverlayElement.HoveredWord = null;
        OverlayElement.Selection = null;
        currentSelection = null;
        hoveredWord = null;
        UpdateCanvasSize();
        if (DisplayImageSource is not null)
        {
            FitToWindow();
        }
    }

    private void UpdateCanvasSize()
    {
        if (!TryGetImageSize(out var imageSize))
        {
            SceneCanvas.Width = 0;
            SceneCanvas.Height = 0;
            ImageElement.Width = 0;
            ImageElement.Height = 0;
            OverlayElement.Width = 0;
            OverlayElement.Height = 0;
            return;
        }

        SceneCanvas.Width = imageSize.Width;
        SceneCanvas.Height = imageSize.Height;
        ImageElement.Width = imageSize.Width;
        ImageElement.Height = imageSize.Height;
        OverlayElement.Width = imageSize.Width;
        OverlayElement.Height = imageSize.Height;
    }

    private void ApplyTransform()
    {
        if (!TryGetImageSize(out var imageSize))
        {
            SceneCanvas.RenderTransform = Transform.Identity;
            return;
        }

        var viewportSize = GetViewportSize();
        if (viewportState.FitMode == FitMode.FitToWindow)
        {
            viewportState = viewportCalculator.CreateFitToWindow(viewportSize, imageSize);
        }

        var offset = viewportCalculator.GetImageOffset(viewportSize, imageSize, viewportState);
        SceneCanvas.RenderTransform = new MatrixTransform(new Matrix(
            viewportState.Zoom,
            0d,
            0d,
            viewportState.Zoom,
            offset.X,
            offset.Y));

        ZoomChanged?.Invoke(this, viewportState.Zoom);
    }

    private void ZoomBy(double factor)
    {
        if (!TryGetImageSize(out var imageSize))
        {
            return;
        }

        var center = new PointD(ViewportHost.ActualWidth / 2d, ViewportHost.ActualHeight / 2d);
        viewportState = viewportCalculator.ZoomAroundPoint(viewportState, GetViewportSize(), imageSize, center, factor);
        ApplyTransform();
    }

    private void StartPan(Point viewportPoint)
    {
        isPanning = true;
        leftButtonPressed = false;
        isSelecting = false;
        lastPanViewportPoint = viewportPoint;
        Cursor = Cursors.SizeAll;
        Mouse.Capture(this);
    }

    private void EndPan()
    {
        isPanning = false;
        Cursor = hoveredWord is null ? Cursors.Arrow : Cursors.IBeam;
        ReleaseMouseCapture();
    }

    private void UpdateSelection(TextSelection? selection)
    {
        currentSelection = selection;
        OverlayElement.Selection = selection;
        SelectionChanged?.Invoke(this, selection);
    }

    private void UpdateHoverWord(Point viewportPoint)
    {
        var hitWord = GetHitTestWord(viewportPoint);
        SetHoverWord(hitWord);
        Cursor = isPanning ? Cursors.SizeAll : hitWord is null ? Cursors.Arrow : Cursors.IBeam;
    }

    private void SetHoverWord(RecognizedWord? word)
    {
        if (hoveredWord?.WordId == word?.WordId)
        {
            return;
        }

        hoveredWord = word;
        OverlayElement.HoveredWord = word;
        HoverWordChanged?.Invoke(this, word);
    }

    private RecognizedWord? GetHitTestWord(Point viewportPoint)
    {
        if (Document is null || !TryGetImageSize(out var imageSize))
        {
            return null;
        }

        var imagePoint = viewportCalculator.ToImageSpace(
            new PointD(viewportPoint.X, viewportPoint.Y),
            GetViewportSize(),
            imageSize,
            viewportState);

        var tolerance = Math.Max(2.25d, 6.75d / Math.Max(viewportState.Zoom, 0.1d));
        return selectionEngine.HitTest(Document, imagePoint, tolerance).Word;
    }

    private SizeD GetViewportSize() => new(ViewportHost.ActualWidth, ViewportHost.ActualHeight);

    private bool TryGetImageSize(out SizeD imageSize)
    {
        if (Document is not null)
        {
            imageSize = new SizeD(Document.ImagePixelWidth, Document.ImagePixelHeight);
            return true;
        }

        if (DisplayImageSource is not null)
        {
            if (DisplayImageSource is System.Windows.Media.Imaging.BitmapSource bitmapSource)
            {
                imageSize = new SizeD(bitmapSource.PixelWidth, bitmapSource.PixelHeight);
            }
            else
            {
                imageSize = new SizeD(DisplayImageSource.Width, DisplayImageSource.Height);
            }

            return true;
        }

        imageSize = default;
        return false;
    }
}
