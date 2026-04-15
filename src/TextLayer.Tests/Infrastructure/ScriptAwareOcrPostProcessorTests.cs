using TextLayer.Application.Models;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Infrastructure.Ocr;

namespace TextLayer.Tests.Infrastructure;

public sealed class ScriptAwareOcrPostProcessorTests
{
    private readonly ScriptAwareOcrPostProcessor postProcessor = new();

    [Fact]
    public void Process_CorrectsMixedScriptCyrillicWord_WithoutChangingBounds()
    {
        var originalRect = new RectD(12, 18, 88, 20);
        var document = CreateDocument(
            new[] { CreateWord("привeт", 0, 0, originalRect), CreateWord("мир", 1, 0, new RectD(108, 18, 46, 20)) });

        var processed = postProcessor.Process(document, OcrLanguageMode.Russian).Document;

        Assert.Equal("привет", processed.Words[0].Text);
        Assert.Equal(originalRect, processed.Words[0].BoundingRect);
        Assert.Equal("привет мир", processed.FullText);
    }

    [Fact]
    public void Process_CorrectsPseudoLatinWord_InRussianLineContext()
    {
        var document = CreateDocument(
            new[] { CreateWord("Bce", 0, 0, new RectD(12, 18, 34, 20)), CreateWord("сообщения", 1, 0, new RectD(56, 18, 124, 20)) });

        var processed = postProcessor.Process(document, OcrLanguageMode.EnglishRussian);

        Assert.Equal("Все", processed.Document.Words[0].Text);
        Assert.Equal(1, processed.Analysis.CorrectedWordCount);
        Assert.Equal(ScriptDominance.Cyrillic, processed.Analysis.DominantScript);
    }

    [Fact]
    public void Process_LeavesEnglishWordCorrect_InLatinContext()
    {
        var document = CreateDocument(
            new[] { CreateWord("Code", 0, 0, new RectD(12, 18, 44, 20)), CreateWord("review", 1, 0, new RectD(62, 18, 66, 20)) });

        var processed = postProcessor.Process(document, OcrLanguageMode.English).Document;

        Assert.Equal("Code", processed.Words[0].Text);
        Assert.Equal("Code review", processed.FullText);
    }

    [Fact]
    public void Process_MergesConnectedFragmentsIntoSingleSelectableWord()
    {
        var document = CreateDocument(
            new[]
            {
                CreateWord("\u043F\u0440\u0438", 0, 0, new RectD(12, 18, 26, 20)),
                CreateWord("v\u0435\u0442", 1, 0, new RectD(39, 18, 28, 20)),
            });

        var processed = postProcessor.Process(document, OcrLanguageMode.Auto).Document;

        Assert.Single(processed.Words);
        Assert.Equal("\u043F\u0440\u0438\u0432\u0435\u0442", processed.Words[0].Text);
        Assert.Equal(new RectD(12, 18, 55, 20), processed.Words[0].BoundingRect);
    }

    [Fact]
    public void Process_RecoversLowercaseLookalikesInMostlyCyrillicWord()
    {
        var document = CreateDocument(
            new[]
            {
                CreateWord("\u0434o\u043C", 0, 0, new RectD(12, 18, 32, 20)),
                CreateWord("\u0442e\u043A\u0441t", 1, 0, new RectD(50, 18, 42, 20)),
            });

        var processed = postProcessor.Process(document, OcrLanguageMode.Russian).Document;

        Assert.Equal("\u0434\u043E\u043C", processed.Words[0].Text);
        Assert.Equal("\u0442\u0435\u043A\u0441\u0442", processed.Words[1].Text);
    }

    [Fact]
    public void Process_DoesNotMergeStandaloneShortWord_WithNeighboringWord()
    {
        var document = CreateDocument(
            new[]
            {
                CreateWord("\u0438", 0, 0, new RectD(12, 18, 7, 20)),
                CreateWord("\u043c\u0438\u0440", 1, 0, new RectD(23.8d, 18, 28, 20)),
            });

        var processed = postProcessor.Process(document, OcrLanguageMode.Russian).Document;

        Assert.Equal(2, processed.Words.Count);
        Assert.Equal("\u0438", processed.Words[0].Text);
        Assert.Equal("\u043c\u0438\u0440", processed.Words[1].Text);
    }

    private static RecognizedDocument CreateDocument(params IReadOnlyList<RecognizedWord>[] lines)
    {
        var words = new List<RecognizedWord>();
        var recognizedLines = new List<RecognizedLine>();

        foreach (var lineWords in lines)
        {
            var remappedWords = lineWords
                .Select((word, index) => word with
                {
                    Index = words.Count + index,
                    LineIndex = recognizedLines.Count,
                })
                .ToArray();

            words.AddRange(remappedWords);
            recognizedLines.Add(new RecognizedLine(
                Guid.NewGuid(),
                recognizedLines.Count,
                string.Join(' ', remappedWords.Select(word => word.Text)),
                BuildLineRect(remappedWords),
                null,
                remappedWords.Select(word => word.WordId).ToArray()));
        }

        return new RecognizedDocument(
            Guid.NewGuid(),
            "test.png",
            900,
            240,
            string.Join(Environment.NewLine, recognizedLines.Select(line => line.Text)),
            recognizedLines,
            words,
            DateTime.UtcNow,
            10,
            "test",
            null);
    }

    private static RecognizedWord CreateWord(string text, int index, int lineIndex, RectD rect)
        => new(
            Guid.NewGuid(),
            index,
            lineIndex,
            text,
            text,
            rect,
            null,
            88);

    private static RectD BuildLineRect(IReadOnlyList<RecognizedWord> lineWords)
    {
        var left = lineWords.Min(word => word.BoundingRect.Left);
        var top = lineWords.Min(word => word.BoundingRect.Top);
        var right = lineWords.Max(word => word.BoundingRect.Right);
        var bottom = lineWords.Max(word => word.BoundingRect.Bottom);
        return new RectD(left, top, right - left, bottom - top);
    }
}
