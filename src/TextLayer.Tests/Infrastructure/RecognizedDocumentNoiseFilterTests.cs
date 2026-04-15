using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Infrastructure.Ocr;

namespace TextLayer.Tests.Infrastructure;

public sealed class RecognizedDocumentNoiseFilterTests
{
    private readonly RecognizedDocumentNoiseFilter filter = new();

    [Fact]
    public void Filter_RemovesIconLikeAndEmojiLikeNoise()
    {
        var iconWord = CreateWord(0, 0, "◆", new RectD(18, 20, 24, 24), 22);
        var helloWord = CreateWord(1, 1, "Hello", new RectD(60, 18, 62, 22), 88);
        var worldWord = CreateWord(2, 1, "world", new RectD(128, 18, 68, 22), 90);

        var document = CreateDocument(
            [iconWord],
            [helloWord, worldWord]);

        var filtered = filter.Filter(document);

        Assert.Equal(2, filtered.Words.Count);
        Assert.Equal(["Hello", "world"], filtered.Words.Select(word => word.Text));
        Assert.Equal("Hello world", filtered.FullText);
    }

    [Fact]
    public void Filter_PreservesShortRealTextTokens()
    {
        var csharpWord = CreateWord(0, 0, "C#", new RectD(24, 18, 40, 18), 82);
        var versionWord = CreateWord(1, 0, "9", new RectD(70, 18, 18, 18), 84);

        var document = CreateDocument([csharpWord, versionWord]);

        var filtered = filter.Filter(document);

        Assert.Equal(["C#", "9"], filtered.Words.Select(word => word.Text));
        Assert.Equal("C# 9", filtered.FullText);
    }

    [Fact]
    public void Filter_PreservesShortHighConfidenceStandaloneText()
    {
        var word = CreateWord(0, 0, "I", new RectD(24, 18, 10, 18), 92);
        var document = CreateDocument([word]);

        var filtered = filter.Filter(document);

        Assert.Single(filtered.Words);
        Assert.Equal("I", filtered.Words[0].Text);
    }

    [Fact]
    public void Filter_PreservesShortConnectedFragment_NextToRealText()
    {
        var fragment = CreateWord(0, 0, "v", new RectD(24, 18, 10, 18), 51);
        var word = CreateWord(1, 0, "ет", new RectD(35, 18, 20, 18), 88);

        var document = CreateDocument([fragment, word]);

        var filtered = filter.Filter(document);

        Assert.Equal(["v", "ет"], filtered.Words.Select(candidate => candidate.Text));
    }

    [Fact]
    public void Filter_PreservesLowConfidenceShortWord_WhenItContainsText()
    {
        var word = CreateWord(0, 0, "OK", new RectD(24, 18, 16, 14), 21);
        var document = CreateDocument([word]);

        var filtered = filter.Filter(document);

        Assert.Single(filtered.Words);
        Assert.Equal("OK", filtered.Words[0].Text);
    }

    [Fact]
    public void Filter_PreservesMixedLanguageWordFragments()
    {
        var russianFragment = CreateWord(0, 0, "Пр", new RectD(18, 18, 18, 18), 42);
        var mixedWord = CreateWord(1, 0, "world", new RectD(39, 18, 38, 18), 44);
        var tail = CreateWord(2, 0, "42", new RectD(82, 18, 18, 18), 33);

        var document = CreateDocument([russianFragment, mixedWord, tail]);

        var filtered = filter.Filter(document);

        Assert.Equal(["Пр", "world", "42"], filtered.Words.Select(candidate => candidate.Text));
    }

    private static RecognizedDocument CreateDocument(params IReadOnlyList<RecognizedWord>[] lineWords)
    {
        var lines = new List<RecognizedLine>();
        var words = new List<RecognizedWord>();

        foreach (var sourceLine in lineWords)
        {
            var lineIndex = lines.Count;
            var remappedWords = sourceLine
                .Select((word, index) => word with
                {
                    Index = words.Count + index,
                    LineIndex = lineIndex,
                })
                .ToArray();

            words.AddRange(remappedWords);
            lines.Add(new RecognizedLine(
                Guid.NewGuid(),
                lineIndex,
                string.Join(' ', remappedWords.Select(word => word.Text)),
                BuildLineRect(remappedWords),
                null,
                remappedWords.Select(word => word.WordId).ToArray()));
        }

        return new RecognizedDocument(
            Guid.NewGuid(),
            "capture.png",
            1200,
            900,
            string.Join(Environment.NewLine, lines.Select(line => line.Text)),
            lines,
            words,
            DateTime.UtcNow,
            12,
            "test",
            "eng+rus");
    }

    private static RecognizedWord CreateWord(int index, int lineIndex, string text, RectD rect, double confidence)
        => new(
            Guid.NewGuid(),
            index,
            lineIndex,
            text,
            text,
            rect,
            null,
            confidence);

    private static RectD BuildLineRect(IReadOnlyList<RecognizedWord> lineWords)
    {
        var left = lineWords.Min(word => word.BoundingRect.Left);
        var top = lineWords.Min(word => word.BoundingRect.Top);
        var right = lineWords.Max(word => word.BoundingRect.Right);
        var bottom = lineWords.Max(word => word.BoundingRect.Bottom);
        return new RectD(left, top, right - left, bottom - top);
    }
}
