using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Domain.Services;

namespace TextLayer.Tests.Domain;

public sealed class TextNormalizerTests
{
    [Fact]
    public void NormalizeDocument_PreservesLineBreaksAndPunctuationSpacing()
    {
        var document = CreateDocument(
            new RecognizedWord(Guid.NewGuid(), 0, 0, "Hello", "Hello", new RectD(0, 0, 10, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 1, 0, ",", ",", new RectD(11, 0, 4, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 2, 0, "world", "world", new RectD(20, 0, 18, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 3, 1, "Next", "Next", new RectD(0, 20, 18, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 4, 1, "line", "line", new RectD(22, 20, 16, 10), null, null));

        var normalizer = new TextNormalizer();

        var result = normalizer.NormalizeDocument(document);

        Assert.Equal("Hello, world" + Environment.NewLine + "Next line", result);
    }

    [Fact]
    public void NormalizeDocument_JoinsWrappedBodyLinesForReadableClipboardText()
    {
        var document = CreateDocument(
            new RecognizedWord(Guid.NewGuid(), 0, 0, "This", "This", new RectD(0, 0, 16, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 1, 0, "line", "line", new RectD(20, 0, 16, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 2, 1, "continues", "continues", new RectD(0, 14, 36, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 3, 1, "naturally", "naturally", new RectD(40, 14, 38, 10), null, null));

        var normalizer = new TextNormalizer();

        var result = normalizer.NormalizeDocument(document);

        Assert.Equal("This line continues naturally", result);
    }

    [Fact]
    public void NormalizeDocument_PreservesListItems()
    {
        var document = CreateDocument(
            new RecognizedWord(Guid.NewGuid(), 0, 0, "Setup", "Setup", new RectD(0, 0, 22, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 1, 1, "-", "-", new RectD(0, 18, 6, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 2, 1, "Download", "Download", new RectD(10, 18, 32, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 3, 2, "-", "-", new RectD(0, 34, 6, 10), null, null),
            new RecognizedWord(Guid.NewGuid(), 4, 2, "Run", "Run", new RectD(10, 34, 16, 10), null, null));

        var normalizer = new TextNormalizer();

        var result = normalizer.NormalizeDocument(document);

        Assert.Equal("Setup" + Environment.NewLine + "- Download" + Environment.NewLine + "- Run", result);
    }

    private static RecognizedDocument CreateDocument(params RecognizedWord[] words)
        => new(
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
}
