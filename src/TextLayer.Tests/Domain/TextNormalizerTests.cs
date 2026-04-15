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
