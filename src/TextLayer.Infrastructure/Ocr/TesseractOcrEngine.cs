using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Tesseract;
using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TextLayer.Infrastructure.Ocr;

public sealed class TesseractOcrEngine(TesseractDataPathResolver dataPathResolver) : IOcrEngine
{
    private readonly TesseractPreprocessingPlanner preprocessingPlanner = new();
    private readonly ScriptAwareOcrPostProcessor postProcessor = new();

    public async Task<RecognizedDocument> RecognizeAsync(string sourcePath, OcrRequestOptions request, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException("The selected image could not be found.");
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var analysis = await new OcrImageAnalyzer().AnalyzeAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            var plan = preprocessingPlanner.CreatePlan(analysis, analysis.PixelWidth, analysis.PixelHeight);
            var raster = await LoadRasterAsync(sourcePath, plan, cancellationToken).ConfigureAwait(false);
            var dataPath = dataPathResolver.Resolve(request.LanguageMode);

            var document = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return RecognizeCore(sourcePath, raster, dataPath, request, plan);
            }, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            return postProcessor.Process(
                    document with { RecognitionDurationMs = stopwatch.ElapsedMilliseconds },
                    request.LanguageMode,
                    RecognizedDocumentNoiseFilter.NoiseFilterProfile.MaximumCoverage)
                .Document;
        }
        catch (TesseractConfigurationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "TextLayer could not recognize text with Accurate OCR. Check that the tessdata files are available and try again.",
                exception);
        }
    }

    private static RecognizedDocument RecognizeCore(
        string sourcePath,
        TesseractRaster raster,
        string dataPath,
        OcrRequestOptions request,
        TesseractPreprocessingPlan plan)
    {
        using var engine = new TesseractEngine(dataPath, GetLanguageCode(request.LanguageMode), EngineMode.Default);
        engine.DefaultPageSegMode = PageSegMode.Auto;
        engine.SetVariable("preserve_interword_spaces", 1);

        using var basePix = Pix.LoadFromMemory(raster.PngBytes);
        basePix.XRes = 300;
        basePix.YRes = 300;

        var candidates = BuildCandidates(basePix, plan, raster.AccentPngBytes);
        try
        {
            TesseractDocumentCandidate? bestCandidate = null;
            foreach (var candidate in candidates)
            {
                using var page = engine.Process(candidate.Image, PageSegMode.Auto);
                var mappedCandidate = MapPage(sourcePath, raster, request, page);
                if (bestCandidate is null || mappedCandidate.Score > bestCandidate.Score)
                {
                    bestCandidate = mappedCandidate;
                }
            }

            return bestCandidate?.Document
                ?? new RecognizedDocument(
                    DocumentId: Guid.NewGuid(),
                    SourcePath: sourcePath,
                    ImagePixelWidth: raster.OriginalWidth,
                    ImagePixelHeight: raster.OriginalHeight,
                    FullText: string.Empty,
                    Lines: [],
                    Words: [],
                    CreatedAtUtc: DateTime.UtcNow,
                    RecognitionDurationMs: 0,
                    OcrEngineId: OcrEngineSelector.AccurateEngineId,
                    LanguageHint: GetLanguageCode(request.LanguageMode));
        }
        finally
        {
            foreach (var candidate in candidates)
            {
                candidate.Dispose();
            }
        }
    }

    private static List<TesseractImageCandidate> BuildCandidates(Pix basePix, TesseractPreprocessingPlan plan, byte[]? accentPngBytes)
    {
        var candidates = new List<TesseractImageCandidate>
        {
            new("base", basePix.Clone()),
        };

        using var grayPreview = basePix.ConvertRGBToGray();
        if (plan.UseDarkUiPass)
        {
            var inverted = grayPreview.Invert();
            candidates.Add(new("dark-ui", inverted.BinarizeSauvola(25, 0.32f, true)));
            inverted.Dispose();
        }
        else if (plan.UseLowContrastPass)
        {
            candidates.Add(new("low-contrast", grayPreview.BinarizeSauvola(25, 0.28f, true)));
        }

        if (plan.UseSmallTextPass)
        {
            candidates.Add(new("small-text", grayPreview.Scale(1.1f, 1.1f)));
        }

        if (accentPngBytes is { Length: > 0 })
        {
            using var accentBasePix = Pix.LoadFromMemory(accentPngBytes);
            candidates.Add(new("accent-base", accentBasePix.Clone()));

            using var accentGrayPreview = accentBasePix.ConvertRGBToGray();
            if (plan.UseDarkUiPass)
            {
                var inverted = accentGrayPreview.Invert();
                candidates.Add(new("accent-dark-ui", inverted.BinarizeSauvola(25, 0.3f, true)));
                inverted.Dispose();
            }
            else if (plan.UseLowContrastPass || plan.UseSmallTextPass)
            {
                candidates.Add(new("accent-low-contrast", accentGrayPreview.BinarizeSauvola(25, 0.28f, true)));
            }
        }

        return candidates;
    }

    private static TesseractDocumentCandidate MapPage(string sourcePath, TesseractRaster raster, OcrRequestOptions request, Page page)
    {
        var words = new List<RecognizedWord>();
        var lines = new List<RecognizedLine>();

        using var iterator = page.GetIterator();
        iterator.Begin();

        var currentLineWords = new List<RecognizedWord>();
        var currentLineWordIds = new List<Guid>();
        var currentLineIndex = -1;

        do
        {
            if (iterator.IsAtBeginningOf(PageIteratorLevel.TextLine))
            {
                FinalizeCurrentLine(lines, currentLineWords, currentLineWordIds);
                currentLineIndex = lines.Count;
            }

            var rawWord = iterator.GetText(PageIteratorLevel.Word);
            if (string.IsNullOrWhiteSpace(rawWord) || !iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds))
            {
                continue;
            }

            var wordId = Guid.NewGuid();
            var recognizedWord = new RecognizedWord(
                WordId: wordId,
                Index: words.Count,
                LineIndex: currentLineIndex,
                Text: rawWord,
                NormalizedText: NormalizeWord(rawWord),
                BoundingRect: new RectD(
                    bounds.X1 * raster.ScaleX,
                    bounds.Y1 * raster.ScaleY,
                    bounds.Width * raster.ScaleX,
                    bounds.Height * raster.ScaleY),
                BoundingPolygon: null,
                Confidence: iterator.GetConfidence(PageIteratorLevel.Word));
            words.Add(recognizedWord);
            currentLineWords.Add(recognizedWord);
            currentLineWordIds.Add(wordId);

            if (iterator.IsAtFinalOf(PageIteratorLevel.TextLine, PageIteratorLevel.Word))
            {
                FinalizeCurrentLine(lines, currentLineWords, currentLineWordIds);
            }
        }
        while (iterator.Next(PageIteratorLevel.Word));

        FinalizeCurrentLine(lines, currentLineWords, currentLineWordIds);

        var fullText = page.GetText()?.Trim() ?? string.Empty;
        var score = GetPageScore(fullText, words.Count, lines.Count, page.GetMeanConfidence(), request.LanguageMode);

        return new TesseractDocumentCandidate(
            new RecognizedDocument(
                DocumentId: Guid.NewGuid(),
                SourcePath: sourcePath,
                ImagePixelWidth: raster.OriginalWidth,
                ImagePixelHeight: raster.OriginalHeight,
                FullText: fullText,
                Lines: lines,
                Words: words,
                CreatedAtUtc: DateTime.UtcNow,
                RecognitionDurationMs: 0,
                OcrEngineId: OcrEngineSelector.AccurateEngineId,
                LanguageHint: GetLanguageCode(request.LanguageMode)),
            score);
    }

    private static void FinalizeCurrentLine(
        List<RecognizedLine> lines,
        List<RecognizedWord> currentLineWords,
        List<Guid> currentLineWordIds)
    {
        if (currentLineWords.Count == 0)
        {
            currentLineWordIds.Clear();
            return;
        }

        lines.Add(new RecognizedLine(
            LineId: Guid.NewGuid(),
            Index: lines.Count,
            Text: string.Join(' ', currentLineWords.Select(word => word.Text)),
            BoundingRect: BuildLineRect(currentLineWords),
            BoundingPolygon: null,
            WordIds: currentLineWordIds.ToArray()));

        currentLineWords.Clear();
        currentLineWordIds.Clear();
    }

    private static RectD BuildLineRect(IReadOnlyList<RecognizedWord> lineWords)
    {
        var left = lineWords.Min(word => word.BoundingRect.Left);
        var top = lineWords.Min(word => word.BoundingRect.Top);
        var right = lineWords.Max(word => word.BoundingRect.Right);
        var bottom = lineWords.Max(word => word.BoundingRect.Bottom);
        return new RectD(left, top, right - left, bottom - top);
    }

    private static string NormalizeWord(string? rawWord)
        => string.IsNullOrWhiteSpace(rawWord)
            ? string.Empty
            : string.Join(' ', rawWord.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static double GetPageScore(
        string text,
        int wordCount,
        int lineCount,
        float meanConfidence,
        OcrLanguageMode languageMode)
    {
        var nonWhitespaceCount = text.Count(static character => !char.IsWhiteSpace(character));
        var hasLatin = text.Any(static character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
        var hasCyrillic = text.Any(static character => character is >= '\u0400' and <= '\u04FF');
        var languageBonus = languageMode switch
        {
            OcrLanguageMode.Russian when hasCyrillic => 20d,
            OcrLanguageMode.English when hasLatin => 20d,
            OcrLanguageMode.EnglishRussian when hasLatin && hasCyrillic => 30d,
            _ => 0d,
        };

        return (wordCount * 14d)
            + (lineCount * 5d)
            + (nonWhitespaceCount * 0.85d)
            + (meanConfidence * 1.15d)
            + languageBonus;
    }

    private static string GetLanguageCode(OcrLanguageMode languageMode)
        => string.Join('+', TesseractDataPathResolver.GetLanguageCodes(languageMode));

    private static async Task<TesseractRaster> LoadRasterAsync(string sourcePath, TesseractPreprocessingPlan plan, CancellationToken cancellationToken)
    {
        var file = await StorageFile.GetFileFromPathAsync(sourcePath).AsTask(cancellationToken).ConfigureAwait(false);
        using IRandomAccessStream stream = await file.OpenReadAsync().AsTask(cancellationToken).ConfigureAwait(false);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
        var originalWidth = (int)decoder.OrientedPixelWidth;
        var originalHeight = (int)decoder.OrientedPixelHeight;
        var scaleFactor = plan.ScaleFactor;
        var scaledWidth = Math.Max(1, (int)Math.Round(originalWidth * scaleFactor));
        var scaledHeight = Math.Max(1, (int)Math.Round(originalHeight * scaleFactor));

        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform
                {
                    ScaledWidth = (uint)scaledWidth,
                    ScaledHeight = (uint)scaledHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant,
                },
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);

        var pngBytes = await EncodePngAsync(softwareBitmap, cancellationToken).ConfigureAwait(false);
        byte[]? accentPngBytes = null;
        if (plan.UseAccentTextPass)
        {
            using var accentBitmap = OcrScreenshotPreprocessor.CreateAccentAwareGrayscaleBitmap(softwareBitmap, invertForDarkUi: false);
            accentPngBytes = await EncodePngAsync(accentBitmap, cancellationToken).ConfigureAwait(false);
        }

        return new TesseractRaster(
            OriginalWidth: originalWidth,
            OriginalHeight: originalHeight,
            RasterWidth: scaledWidth,
            RasterHeight: scaledHeight,
            PngBytes: pngBytes,
            AccentPngBytes: accentPngBytes);
    }

    private static async Task<byte[]> EncodePngAsync(SoftwareBitmap softwareBitmap, CancellationToken cancellationToken)
    {
        using var memoryStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, memoryStream).AsTask(cancellationToken).ConfigureAwait(false);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);

        memoryStream.Seek(0);
        using var reader = new DataReader(memoryStream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)memoryStream.Size).AsTask(cancellationToken).ConfigureAwait(false);
        var bytes = new byte[memoryStream.Size];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private sealed record TesseractRaster(
        int OriginalWidth,
        int OriginalHeight,
        int RasterWidth,
        int RasterHeight,
        byte[] PngBytes,
        byte[]? AccentPngBytes)
    {
        public double ScaleX => OriginalWidth / (double)RasterWidth;

        public double ScaleY => OriginalHeight / (double)RasterHeight;
    }

    private sealed record TesseractDocumentCandidate(RecognizedDocument Document, double Score);

    private sealed class TesseractImageCandidate(string name, Pix image) : IDisposable
    {
        public string Name { get; } = name;

        public Pix Image { get; } = image;

        public void Dispose() => Image.Dispose();
    }
}
