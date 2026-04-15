using System.Windows;
using TextLayer.Domain.Enums;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Domain.Services;

namespace TextLayer.App.Controls;

using Mouse = System.Windows.Input.Mouse;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using ScaleTransform = System.Windows.Media.ScaleTransform;
using UserControl = System.Windows.Controls.UserControl;
using Cursors = System.Windows.Input.Cursors;

public partial class ScreenOverlayControl : UserControl
{
    private const double DragThreshold = 5d;
    private readonly ISelectionEngine selectionEngine = new SelectionEngine(new TextNormalizer());
    private readonly PointerInteractionClassifier interactionClassifier = new();
    private readonly ScreenOverlayCoordinateMapper coordinateMapper = new();
    private TextSelection? currentSelection;
    private RecognizedWord? hoveredWord;
    private Point pointerDownPoint;
    private RecognizedWord? pointerDownWord;
    private RecognizedWord? selectionAnchorWord;
    private bool leftButtonPressed;
    private bool isSelecting;

    public ScreenOverlayControl()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyDocumentState();
    }

    public event EventHandler<TextSelection?>? SelectionChanged;

    public event EventHandler<RecognizedWord?>? HoverWordChanged;

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(
            nameof(Document),
            typeof(RecognizedDocument),
            typeof(ScreenOverlayControl),
            new PropertyMetadata(null, OnDocumentChanged));

    public static readonly DependencyProperty PixelsPerDipXProperty =
        DependencyProperty.Register(
            nameof(PixelsPerDipX),
            typeof(double),
            typeof(ScreenOverlayControl),
            new PropertyMetadata(1d, OnScaleChanged));

    public static readonly DependencyProperty PixelsPerDipYProperty =
        DependencyProperty.Register(
            nameof(PixelsPerDipY),
            typeof(double),
            typeof(ScreenOverlayControl),
            new PropertyMetadata(1d, OnScaleChanged));

    public static readonly DependencyProperty ShowDebugBoundsProperty =
        DependencyProperty.Register(
            nameof(ShowDebugBounds),
            typeof(bool),
            typeof(ScreenOverlayControl),
            new PropertyMetadata(false, OnShowDebugBoundsChanged));

    public RecognizedDocument? Document
    {
        get => (RecognizedDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public double PixelsPerDipX
    {
        get => (double)GetValue(PixelsPerDipXProperty);
        set => SetValue(PixelsPerDipXProperty, value);
    }

    public double PixelsPerDipY
    {
        get => (double)GetValue(PixelsPerDipYProperty);
        set => SetValue(PixelsPerDipYProperty, value);
    }

    public bool ShowDebugBounds
    {
        get => (bool)GetValue(ShowDebugBoundsProperty);
        set => SetValue(ShowDebugBoundsProperty, value);
    }

    public TextSelection? CurrentSelection => currentSelection;

    public void SelectAll()
    {
        if (Document is null || Document.Words.Count == 0)
        {
            return;
        }

        UpdateSelection(selectionEngine.CreateFullDocumentSelection(Document));
    }

    public void ClearSelection() => UpdateSelection(null);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Focus();

        var viewportPoint = e.GetPosition(this);
        UpdateHoverWord(viewportPoint);

        if (!leftButtonPressed || Document is null)
        {
            return;
        }

        var selectionWord = GetHitTestWord(viewportPoint, includeSelectionTolerance: true);
        var intent = interactionClassifier.ClassifyDragIntent(
            new PointD(pointerDownPoint.X, pointerDownPoint.Y),
            new PointD(viewportPoint.X, viewportPoint.Y),
            startedOverText: pointerDownWord is not null || selectionWord is not null,
            panRequested: false,
            dragThreshold: DragThreshold);

        if (!isSelecting && intent == PointerDragIntent.TextSelection)
        {
            selectionAnchorWord ??= pointerDownWord ?? selectionWord;
            if (selectionAnchorWord is null)
            {
                return;
            }

            isSelecting = true;
            Mouse.Capture(this);
        }

        if (!isSelecting || selectionAnchorWord is null)
        {
            return;
        }

        var activeWord = selectionWord ?? hoveredWord ?? selectionAnchorWord;
        UpdateSelection(selectionEngine.CreateRangeSelection(Document, selectionAnchorWord.Index, activeWord.Index));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!leftButtonPressed)
        {
            SetHoverWord(null);
            Cursor = Cursors.Arrow;
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();

        leftButtonPressed = true;
        pointerDownPoint = e.GetPosition(this);
        pointerDownWord = GetHitTestWord(pointerDownPoint, includeSelectionTolerance: false);
        selectionAnchorWord = pointerDownWord;

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
        leftButtonPressed = false;
        isSelecting = false;
        pointerDownWord = null;
        selectionAnchorWord = null;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private static void OnDocumentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        => ((ScreenOverlayControl)dependencyObject).ApplyDocumentState();

    private static void OnScaleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        => ((ScreenOverlayControl)dependencyObject).ApplyScale();

    private static void OnShowDebugBoundsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        => ((ScreenOverlayControl)dependencyObject).OverlayElement.ShowDebugBounds = (bool)e.NewValue;

    private void ApplyDocumentState()
    {
        OverlayElement.Document = Document;
        OverlayElement.Selection = null;
        OverlayElement.HoveredWord = null;
        OverlayElement.ShowDebugBounds = ShowDebugBounds;
        currentSelection = null;
        hoveredWord = null;

        if (Document is null)
        {
            SceneCanvas.Width = 0;
            SceneCanvas.Height = 0;
            OverlayElement.Width = 0;
            OverlayElement.Height = 0;
            return;
        }

        SceneCanvas.Width = Document.ImagePixelWidth;
        SceneCanvas.Height = Document.ImagePixelHeight;
        OverlayElement.Width = Document.ImagePixelWidth;
        OverlayElement.Height = Document.ImagePixelHeight;
        ApplyScale();
    }

    private void ApplyScale()
        => SceneCanvas.RenderTransform = new ScaleTransform(
            1d / Math.Max(PixelsPerDipX, 0.0001d),
            1d / Math.Max(PixelsPerDipY, 0.0001d));

    private void UpdateSelection(TextSelection? selection)
    {
        currentSelection = selection;
        OverlayElement.Selection = selection;
        SelectionChanged?.Invoke(this, selection);
    }

    private void UpdateHoverWord(Point viewportPoint)
    {
        var hitWord = GetHitTestWord(viewportPoint, includeSelectionTolerance: false);
        SetHoverWord(hitWord);
        Cursor = hitWord is null ? Cursors.Arrow : Cursors.IBeam;
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

    private RecognizedWord? GetHitTestWord(Point viewportPoint, bool includeSelectionTolerance)
    {
        if (Document is null)
        {
            return null;
        }

        var imagePoint = coordinateMapper.ToImageSpace(
            new PointD(viewportPoint.X, viewportPoint.Y),
            PixelsPerDipX,
            PixelsPerDipY);

        var tolerance = includeSelectionTolerance
            ? Math.Max(9d, 12.5d * Math.Max(PixelsPerDipX, PixelsPerDipY))
            : Math.Max(3d, 5d * Math.Max(PixelsPerDipX, PixelsPerDipY));
        return selectionEngine.HitTest(Document, imagePoint, tolerance).Word;
    }
}
