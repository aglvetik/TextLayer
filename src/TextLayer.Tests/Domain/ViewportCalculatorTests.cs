using TextLayer.Domain.Enums;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Domain.Services;

namespace TextLayer.Tests.Domain;

public sealed class ViewportCalculatorTests
{
    [Fact]
    public void CreateFitToWindow_UsesSmallestAxisScale()
    {
        var calculator = new ViewportCalculator();

        var state = calculator.CreateFitToWindow(new SizeD(1000, 500), new SizeD(400, 400));

        Assert.Equal(FitMode.FitToWindow, state.FitMode);
        Assert.Equal(1.25d, state.Zoom, 3);
    }

    [Fact]
    public void ZoomAroundPoint_KeepsAnchorStable()
    {
        var calculator = new ViewportCalculator();
        var viewport = new SizeD(1000, 800);
        var image = new SizeD(400, 300);
        var state = calculator.CreateFitToWindow(viewport, image);
        var anchor = new PointD(400, 320);
        var imagePointBefore = calculator.ToImageSpace(anchor, viewport, image, state);

        var zoomed = calculator.ZoomAroundPoint(state with { FitMode = FitMode.Custom }, viewport, image, anchor, 1.5d);
        var anchorAfter = calculator.ToViewportSpace(imagePointBefore, viewport, image, zoomed);

        Assert.Equal(anchor.X, anchorAfter.X, 3);
        Assert.Equal(anchor.Y, anchorAfter.Y, 3);
    }

    [Fact]
    public void CreateFitToWindow_CentersImageWithinViewport()
    {
        var calculator = new ViewportCalculator();
        var viewport = new SizeD(1200, 800);
        var image = new SizeD(600, 300);

        var state = calculator.CreateFitToWindow(viewport, image);
        var offset = calculator.GetImageOffset(viewport, image, state);

        Assert.Equal(0d, offset.X, 3);
        Assert.Equal(100d, offset.Y, 3);
    }

    [Fact]
    public void ViewportRoundTrip_PreservesPointDuringPanAndZoom()
    {
        var calculator = new ViewportCalculator();
        var viewport = new SizeD(1280, 720);
        var image = new SizeD(1600, 900);
        var state = new ViewportState(1.75d, 42d, -36d, FitMode.Custom);
        var imagePoint = new PointD(320, 180);

        var viewportPoint = calculator.ToViewportSpace(imagePoint, viewport, image, state);
        var roundTripped = calculator.ToImageSpace(viewportPoint, viewport, image, state);

        Assert.Equal(imagePoint.X, roundTripped.X, 3);
        Assert.Equal(imagePoint.Y, roundTripped.Y, 3);
    }
}
