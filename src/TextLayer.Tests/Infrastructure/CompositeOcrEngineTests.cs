using System.Drawing;
using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using TextLayer.Infrastructure.Ocr;

namespace TextLayer.Tests.Infrastructure;

public sealed class CompositeOcrEngineTests
{
    [Fact]
    public async Task AutoModeWithRussianPreference_RetriesAndChoosesCandidateWithProperCyrillic()
    {
        var sourcePath = CreateTempImage();
        try
        {
            var fastEngine = new FakeOcrEngine((request, _) => CreateDocument(
                request.LanguageMode == OcrLanguageMode.English
                    ? "Message overlay"
                    : "Bce coobshcheniya"));
            var accurateEngine = new FakeOcrEngine((request, _) => CreateDocument(
                request.LanguageMode == OcrLanguageMode.English
                    ? "Message overlay"
                    : "\u0412\u0441\u0435 \u0441\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u044f",
                OcrEngineSelector.AccurateEngineId));

            var engine = new CompositeOcrEngine(
                fastEngine,
                accurateEngine,
                new OcrImageAnalyzer(),
                new OcrEngineSelector(),
                new TestLogService());

            var document = await engine.RecognizeAsync(
                sourcePath,
                new OcrRequestOptions(OcrMode.Auto, OcrLanguageMode.Russian),
                CancellationToken.None);

            Assert.Equal(OcrEngineSelector.AccurateEngineId, document.OcrEngineId);
            Assert.Contains("\u0412\u0441\u0435", document.FullText);
            Assert.DoesNotContain("Bce", document.FullText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task AutoLanguage_PrefersMixedCandidate_WhenCaptureContainsBothScripts()
    {
        var sourcePath = CreateTempImage();
        try
        {
            var fastEngine = new FakeOcrEngine((request, _) => CreateDocument(request.LanguageMode switch
            {
                OcrLanguageMode.English => "Server message overlay",
                OcrLanguageMode.Russian => "\u0421\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u0435 \u043e\u0432\u0435\u0440\u043b\u0435\u044f",
                OcrLanguageMode.EnglishRussian => "Server \u043e\u0432\u0435\u0440\u043b\u0435\u0439 ready",
                _ => "Server \u043e\u0432\u0435\u0440\u043b\u0435\u0439 ready",
            }));
            var accurateEngine = new FakeOcrEngine((request, _) => CreateDocument(request.LanguageMode switch
            {
                OcrLanguageMode.English => "Server status ready",
                OcrLanguageMode.Russian => "\u0421\u0435\u0440\u0432\u0435\u0440 \u0441\u0442\u0430\u0442\u0443\u0441 \u0433\u043e\u0442\u043e\u0432",
                OcrLanguageMode.EnglishRussian => "Server \u0441\u0442\u0430\u0442\u0443\u0441 ready",
                _ => "Server \u0441\u0442\u0430\u0442\u0443\u0441 ready",
            }, OcrEngineSelector.AccurateEngineId));

            var engine = new CompositeOcrEngine(
                fastEngine,
                accurateEngine,
                new OcrImageAnalyzer(),
                new OcrEngineSelector(),
                new TestLogService());

            var document = await engine.RecognizeAsync(
                sourcePath,
                new OcrRequestOptions(OcrMode.Auto, OcrLanguageMode.Auto),
                CancellationToken.None);

            Assert.Equal(OcrEngineSelector.AccurateEngineId, document.OcrEngineId);
            Assert.Contains("Server", document.FullText);
            Assert.Contains("ready", document.FullText);
            Assert.Contains("\u0441\u0442\u0430\u0442\u0443\u0441", document.FullText);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task FastMode_KeepsExplicitRussianRequest_OnFastEngine()
    {
        var sourcePath = CreateTempImage();
        try
        {
            var fastEngine = new FakeOcrEngine((request, _) => CreateDocument(
                request.LanguageMode == OcrLanguageMode.Russian
                    ? "\u041f\u0440\u0438\u0432\u0435\u0442 \u043c\u0438\u0440"
                    : "Hello world"));
            var accurateEngine = new FakeOcrEngine((request, _) => CreateDocument(
                "\u041f\u0440\u0438\u0432\u0435\u0442 \u043c\u0438\u0440",
                OcrEngineSelector.AccurateEngineId));

            var engine = new CompositeOcrEngine(
                fastEngine,
                accurateEngine,
                new OcrImageAnalyzer(),
                new OcrEngineSelector(),
                new TestLogService());

            var document = await engine.RecognizeAsync(
                sourcePath,
                new OcrRequestOptions(OcrMode.Fast, OcrLanguageMode.Russian),
                CancellationToken.None);

            Assert.Equal(OcrEngineSelector.FastEngineId, document.OcrEngineId);
            Assert.Contains("\u041f\u0440\u0438\u0432\u0435\u0442", document.FullText);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task AccurateMode_EvaluatesMixedLanguageCandidate_ForMoreCoverage()
    {
        var sourcePath = CreateTempImage();
        try
        {
            var fastEngine = new FakeOcrEngine((request, _) => CreateDocument("unused"));
            var accurateEngine = new FakeOcrEngine((request, _) => CreateDocument(request.LanguageMode switch
            {
                OcrLanguageMode.Russian => "\u0412\u0430\u0436\u043d\u043e\u0435 \u0441\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u0435",
                OcrLanguageMode.EnglishRussian => "\u0412\u0430\u0436\u043d\u043e\u0435 \u0441\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u0435 Release notes beta 2",
                _ => "Release notes beta 2",
            }, OcrEngineSelector.AccurateEngineId));

            var engine = new CompositeOcrEngine(
                fastEngine,
                accurateEngine,
                new OcrImageAnalyzer(),
                new OcrEngineSelector(),
                new TestLogService());

            var document = await engine.RecognizeAsync(
                sourcePath,
                new OcrRequestOptions(OcrMode.Accurate, OcrLanguageMode.Russian),
                CancellationToken.None);

            Assert.Equal(OcrEngineSelector.AccurateEngineId, document.OcrEngineId);
            Assert.Contains("Release", document.FullText);
            Assert.Contains("\u0412\u0430\u0436\u043d\u043e\u0435", document.FullText);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    private static string CreateTempImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        using var bitmap = new Bitmap(320, 140);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(26, 28, 34));
        bitmap.Save(path);
        return path;
    }

    private static RecognizedDocument CreateDocument(string text, string engineId = OcrEngineSelector.FastEngineId)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((word, index) => new RecognizedWord(
                Guid.NewGuid(),
                index,
                0,
                word,
                word,
                new RectD(20 + (index * 56), 28, Math.Max(18, word.Length * 8), 22),
                null,
                86))
            .ToArray();

        var line = new RecognizedLine(
            Guid.NewGuid(),
            0,
            text,
            new RectD(20, 28, words.Sum(word => word.BoundingRect.Width) + ((words.Length - 1) * 8), 22),
            null,
            words.Select(word => word.WordId).ToArray());

        return new RecognizedDocument(
            Guid.NewGuid(),
            "ocr.png",
            320,
            140,
            text,
            [line],
            words,
            DateTime.UtcNow,
            12,
            engineId,
            null);
    }

    private sealed class FakeOcrEngine(Func<OcrRequestOptions, string, RecognizedDocument> factory) : IOcrEngine
    {
        public Task<RecognizedDocument> RecognizeAsync(string sourcePath, OcrRequestOptions request, CancellationToken cancellationToken)
            => Task.FromResult(factory(request, sourcePath));
    }

    private sealed class TestLogService : ILogService
    {
        public void Error(string message, Exception? exception = null)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }
    }
}
