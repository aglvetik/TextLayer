using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Domain.Services;

namespace TextLayer.Tests.Domain;

public sealed class SelectionEngineTests
{
    [Fact]
    public void CreateRangeSelection_SelectsWordsInDocumentOrderAcrossLines()
    {
        var selectionEngine = new SelectionEngine(new TextNormalizer());
        var words = new[]
        {
            new RecognizedWord(Guid.NewGuid(), 0, 0, "First", "First", new RectD(0, 0, 20, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 1, 0, "line", "line", new RectD(30, 0, 18, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 2, 1, "Second", "Second", new RectD(0, 20, 28, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 3, 1, "line", "line", new RectD(36, 20, 18, 10), null, null),
        };
        var document = new RecognizedDocument(
            Guid.NewGuid(),
            "sample.png",
            100,
            100,
            string.Empty,
            [],
            words,
            DateTime.UtcNow,
            10,
            "test",
            null);

        var selection = selectionEngine.CreateRangeSelection(document, 1, 3);

        Assert.NotNull(selection);
        Assert.Equal("line" + Environment.NewLine + "Second line", selection!.SelectedText);
        Assert.Equal(3, selection.SelectedWordIds.Count);
    }

    [Fact]
    public void HitTest_ReturnsNearestWordInsideTolerance()
    {
        var selectionEngine = new SelectionEngine(new TextNormalizer());
        var word = new RecognizedWord(Guid.NewGuid(), 0, 0, "Hover", "Hover", new RectD(10, 10, 30, 10), null, null);
        var document = new RecognizedDocument(Guid.NewGuid(), "sample.png", 100, 100, string.Empty, [], [word], DateTime.UtcNow, 10, "test", null);

        var hit = selectionEngine.HitTest(document, new PointD(42, 16), 3d);

        Assert.True(hit.IsHit);
        Assert.Equal(word.WordId, hit.Word?.WordId);
    }

    [Fact]
    public void HitTest_CanReachSmallWordNearPointer()
    {
        var selectionEngine = new SelectionEngine(new TextNormalizer());
        var shortWord = new RecognizedWord(Guid.NewGuid(), 0, 0, "и", "и", new RectD(20, 10, 6, 10), null, null);
        var document = new RecognizedDocument(Guid.NewGuid(), "sample.png", 100, 100, string.Empty, [], [shortWord], DateTime.UtcNow, 10, "test", null);

        var hit = selectionEngine.HitTest(document, new PointD(28.5d, 15d), 3d);

        Assert.True(hit.IsHit);
        Assert.Equal(shortWord.WordId, hit.Word?.WordId);
    }
}
