using TextLayer.Application.Models;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Infrastructure.Ocr;

namespace TextLayer.Tests.Infrastructure;

public sealed class RecognizedDocumentScoreCalculatorTests
{
    private readonly RecognizedDocumentScoreCalculator calculator = new();

    [Fact]
    public void EnglishText_SuggestsEnglish()
    {
        var document = CreateDocument("Selection should copy OCR text correctly across a clean English screenshot.");

        var score = calculator.Score(document, OcrLanguageMode.English);

        Assert.Equal(OcrLanguageMode.English, score.SuggestedLanguageMode);
        Assert.True(score.Value > 0);
    }

    [Fact]
    public void CyrillicText_SuggestsRussian()
    {
        var document = CreateDocument("Выделение должно корректно копировать текст с русского скриншота.");

        var score = calculator.Score(document, OcrLanguageMode.Russian);

        Assert.Equal(OcrLanguageMode.Russian, score.SuggestedLanguageMode);
        Assert.True(score.Value > 0);
    }

    [Fact]
    public void MixedText_SuggestsCombinedRecognition()
    {
        var document = CreateDocument("Select текст from Discord and copy OCR without changing 2026-04-15.");

        var score = calculator.Score(document, OcrLanguageMode.EnglishRussian);

        Assert.Equal(OcrLanguageMode.EnglishRussian, score.SuggestedLanguageMode);
        Assert.True(score.Value > 0);
    }

    private static RecognizedDocument CreateDocument(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((word, index) => new RecognizedWord(
                Guid.NewGuid(),
                index,
                0,
                word,
                word,
                new RectD(index * 24, 0, Math.Max(10, word.Length * 8), 18),
                null,
                85))
            .ToArray();

        var line = new RecognizedLine(
            Guid.NewGuid(),
            0,
            text,
            new RectD(0, 0, words.Sum(word => word.BoundingRect.Width), 18),
            null,
            words.Select(word => word.WordId).ToArray());

        return new RecognizedDocument(
            Guid.NewGuid(),
            "test.png",
            800,
            200,
            text,
            [line],
            words,
            DateTime.UtcNow,
            10,
            "test",
            null);
    }
}
