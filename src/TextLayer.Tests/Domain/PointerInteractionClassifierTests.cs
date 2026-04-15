using TextLayer.Domain.Enums;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Services;

namespace TextLayer.Tests.Domain;

public sealed class PointerInteractionClassifierTests
{
    [Fact]
    public void IsClick_ReturnsTrueInsideThreshold()
    {
        var classifier = new PointerInteractionClassifier();

        var result = classifier.IsClick(new PointD(0, 0), new PointD(3, 3), 5d);

        Assert.True(result);
    }

    [Fact]
    public void ClassifyDragIntent_ReturnsSelectionWhenStartedOverTextAndThresholdExceeded()
    {
        var classifier = new PointerInteractionClassifier();

        var result = classifier.ClassifyDragIntent(new PointD(0, 0), new PointD(10, 0), startedOverText: true, panRequested: false, dragThreshold: 5d);

        Assert.Equal(PointerDragIntent.TextSelection, result);
    }
}
