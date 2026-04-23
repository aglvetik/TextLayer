using System.Runtime.InteropServices.WindowsRuntime;
using TextLayer.Application.Models;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Foundation;

namespace TextLayer.Infrastructure.Ocr;

internal sealed class WindowsMixedFastOcrPipeline
{
    private static readonly string[] ProtectedTokens =
    [
        "TextLayer",
        "OCR",
        "Windows",
        "Tesseract",
        "GitHub",
        "Releases",
        "Download",
        "Setup",
        "Ctrl+Shift+O",
        "Esc",
        ".exe",
    ];

    private readonly ScriptAwareOcrPostProcessor postProcessor = new();
    private readonly RecognizedDocumentScoreCalculator scoreCalculator = new();
    private readonly MixedLanguageDocumentFusion fusion = new();

    public async Task<RecognizedDocument> RecognizeAsync(
        string sourcePath,
        SoftwareBitmap bitmap,
        int originalWidth,
        int originalHeight,
        bool isDarkBackground,
        bool preferEnhancedBitmap,
        CancellationToken cancellationToken)
    {
        using var lineDetectionBitmap = OcrScreenshotPreprocessor.CreateLineDetectionBitmap(bitmap, isDarkBackground);
        var detectionPixels = CopyPixels(lineDetectionBitmap);
        var ocrPixels = CopyPixels(bitmap);
        var rasterWidth = bitmap.PixelWidth;
        var rasterHeight = bitmap.PixelHeight;
        var scaleX = originalWidth / (double)rasterWidth;
        var scaleY = originalHeight / (double)rasterHeight;

        var regions = SegmentLineRegions(detectionPixels, rasterWidth, rasterHeight);
        if (regions.Count == 0)
        {
            regions = BuildFallbackRegions(rasterWidth, rasterHeight);
        }

        var englishEngine = CreateEngine(OcrLanguageMode.English);
        var russianEngine = CreateEngine(OcrLanguageMode.Russian);
        var recognizedLineDocuments = await RecognizeRegionsAsync(
                regions,
                sourcePath,
                ocrPixels,
                rasterWidth,
                rasterHeight,
                englishEngine,
                russianEngine,
                scaleX,
                scaleY,
                originalWidth,
                originalHeight,
                cancellationToken)
            .ConfigureAwait(false);

        if (recognizedLineDocuments.Count == 0)
        {
            var fallbackRegions = BuildFallbackRegions(rasterWidth, rasterHeight);
            recognizedLineDocuments = await RecognizeRegionsAsync(
                    fallbackRegions,
                    sourcePath,
                    ocrPixels,
                    rasterWidth,
                    rasterHeight,
                    englishEngine,
                    russianEngine,
                    scaleX,
                    scaleY,
                    originalWidth,
                    originalHeight,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (recognizedLineDocuments.Count == 0)
        {
            return new RecognizedDocument(
                Guid.NewGuid(),
                sourcePath,
                originalWidth,
                originalHeight,
                string.Empty,
                [],
                [],
                DateTime.UtcNow,
                0,
                OcrEngineSelector.FastEngineId,
                "eng+rus line-routed");
        }

        return CombineLineDocuments(sourcePath, originalWidth, originalHeight, recognizedLineDocuments);
    }

    private async Task<List<RecognizedDocument>> RecognizeRegionsAsync(
        IReadOnlyList<Rect> regions,
        string sourcePath,
        byte[] ocrPixels,
        int rasterWidth,
        int rasterHeight,
        OcrEngine englishEngine,
        OcrEngine russianEngine,
        double scaleX,
        double scaleY,
        int originalWidth,
        int originalHeight,
        CancellationToken cancellationToken)
    {
        var documents = new List<RecognizedDocument>(regions.Count);
        foreach (var region in regions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var lineBitmap = CropBitmap(ocrPixels, rasterWidth, rasterHeight, region);
            if (lineBitmap.PixelWidth == 0 || lineBitmap.PixelHeight == 0)
            {
                continue;
            }

            var lineDocument = await RecognizeLineAsync(
                    sourcePath,
                    lineBitmap,
                    region,
                    englishEngine,
                    russianEngine,
                    scaleX,
                    scaleY,
                    originalWidth,
                    originalHeight,
                    cancellationToken)
                .ConfigureAwait(false);

            if (lineDocument.Words.Count > 0)
            {
                documents.Add(lineDocument);
            }
        }

        return documents;
    }

    private async Task<RecognizedDocument> RecognizeLineAsync(
        string sourcePath,
        SoftwareBitmap lineBitmap,
        Rect region,
        OcrEngine englishEngine,
        OcrEngine russianEngine,
        double scaleX,
        double scaleY,
        int originalWidth,
        int originalHeight,
        CancellationToken cancellationToken)
    {
        var englishResult = await englishEngine.RecognizeAsync(lineBitmap).AsTask(cancellationToken).ConfigureAwait(false);
        var englishDocument = postProcessor.Process(
                MapResultToDocument(
                    sourcePath,
                    englishResult,
                    region,
                    scaleX,
                    scaleY,
                    originalWidth,
                    originalHeight,
                    OcrLanguageMode.English,
                    englishEngine.RecognizerLanguage?.LanguageTag),
                OcrLanguageMode.English,
                RecognizedDocumentNoiseFilter.NoiseFilterProfile.Standard)
            .Document;

        var englishScore = scoreCalculator.Score(englishDocument, OcrLanguageMode.English);
        var tendency = ClassifyLineTendency(englishDocument, englishScore);
        if (tendency == LineScriptTendency.English)
        {
            return englishDocument;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var russianResult = await russianEngine.RecognizeAsync(lineBitmap).AsTask(cancellationToken).ConfigureAwait(false);
        var russianDocument = postProcessor.Process(
                MapResultToDocument(
                    sourcePath,
                    russianResult,
                    region,
                    scaleX,
                    scaleY,
                    originalWidth,
                    originalHeight,
                    OcrLanguageMode.Russian,
                    russianEngine.RecognizerLanguage?.LanguageTag),
                OcrLanguageMode.Russian,
                RecognizedDocumentNoiseFilter.NoiseFilterProfile.Standard)
            .Document;

        if (englishDocument.Words.Count == 0)
        {
            return russianDocument;
        }

        if (russianDocument.Words.Count == 0)
        {
            return englishDocument;
        }

        var russianScore = scoreCalculator.Score(russianDocument, OcrLanguageMode.Russian);
        if (tendency == LineScriptTendency.Russian
            && !HasUsefulEnglishTokens(englishDocument, englishScore))
        {
            return russianDocument;
        }

        if (!ShouldFuseLine(englishDocument, englishScore, russianDocument, russianScore))
        {
            return ChooseBestSingleLanguageLine(englishDocument, englishScore, russianDocument, russianScore);
        }

        return fusion.Fuse(
            [
                new MixedLanguageRecognitionBranch(englishDocument, OcrLanguageMode.English),
                new MixedLanguageRecognitionBranch(russianDocument, OcrLanguageMode.Russian),
            ],
            OcrEngineSelector.FastEngineId,
            0);
    }

    private static LineScriptTendency ClassifyLineTendency(
        RecognizedDocument englishDocument,
        RecognizedDocumentScore englishScore)
    {
        if (englishDocument.Words.Count == 0)
        {
            return LineScriptTendency.Russian;
        }

        if (englishScore.SuspiciousPseudoLatinWordCount >= 1)
        {
            return LineScriptTendency.Russian;
        }

        if (englishScore.CyrillicCharacterCount > 0)
        {
            return LineScriptTendency.Mixed;
        }

        var protectedTokenHits = CountProtectedTokenHits(englishDocument.FullText);
        if (englishScore.LatinCharacterCount >= 6
            && englishScore.MixedScriptWordCount == 0
            && englishScore.SuspiciousPseudoLatinWordCount == 0
            && englishScore.CyrillicCharacterCount == 0)
        {
            return LineScriptTendency.English;
        }

        if (protectedTokenHits > 0
            && englishScore.SuspiciousPseudoLatinWordCount == 0
            && englishScore.CyrillicCharacterCount == 0
            && englishScore.LatinCharacterCount >= 3)
        {
            return LineScriptTendency.English;
        }

        if (englishScore.LatinCharacterCount >= 2
            && englishScore.SuspiciousPseudoLatinWordCount == 0
            && englishDocument.Words.Count <= 2)
        {
            return LineScriptTendency.English;
        }

        return LineScriptTendency.Mixed;
    }

    private static bool ShouldFuseLine(
        RecognizedDocument englishDocument,
        RecognizedDocumentScore englishScore,
        RecognizedDocument russianDocument,
        RecognizedDocumentScore russianScore)
        => russianScore.CyrillicCharacterCount >= 2
           && HasUsefulEnglishTokens(englishDocument, englishScore)
           && russianDocument.Words.Count > 0;

    private static RecognizedDocument ChooseBestSingleLanguageLine(
        RecognizedDocument englishDocument,
        RecognizedDocumentScore englishScore,
        RecognizedDocument russianDocument,
        RecognizedDocumentScore russianScore)
    {
        var englishAdjustedScore = englishScore.Value
            + (HasUsefulEnglishTokens(englishDocument, englishScore) ? 16d : 0d)
            - (englishScore.SuspiciousPseudoLatinWordCount * 90d);
        var russianAdjustedScore = russianScore.Value
            + (russianScore.CyrillicCharacterCount >= 2 ? 22d : 0d);

        return englishAdjustedScore >= russianAdjustedScore
            ? englishDocument
            : russianDocument;
    }

    private static bool HasUsefulEnglishTokens(RecognizedDocument englishDocument, RecognizedDocumentScore englishScore)
        => englishScore.LatinCharacterCount >= 2
           && englishScore.SuspiciousPseudoLatinWordCount == 0
           && (CountProtectedTokenHits(englishDocument.FullText) > 0 || englishScore.LatinCharacterCount >= 5);

    private static RecognizedDocument CombineLineDocuments(
        string sourcePath,
        int originalWidth,
        int originalHeight,
        IReadOnlyList<RecognizedDocument> lineDocuments)
    {
        var orderedLines = lineDocuments
            .SelectMany(document => document.Lines.Select(line => (Document: document, Line: line)))
            .OrderBy(item => item.Line.BoundingRect.Top)
            .ThenBy(item => item.Line.BoundingRect.Left)
            .ToArray();

        var combinedWords = new List<RecognizedWord>();
        var combinedLines = new List<RecognizedLine>();

        foreach (var (document, line) in orderedLines)
        {
            var wordsById = document.Words.ToDictionary(word => word.WordId);
            var lineWords = line.WordIds
                .Select(wordId => wordsById.GetValueOrDefault(wordId))
                .OfType<RecognizedWord>()
                .OrderBy(word => word.BoundingRect.Left)
                .ToArray();
            if (lineWords.Length == 0)
            {
                continue;
            }

            var lineIndex = combinedLines.Count;
            var remappedWords = lineWords
                .Select((word, index) => word with
                {
                    Index = combinedWords.Count + index,
                    LineIndex = lineIndex,
                })
                .ToArray();

            combinedWords.AddRange(remappedWords);
            combinedLines.Add(new RecognizedLine(
                Guid.NewGuid(),
                lineIndex,
                string.Join(' ', remappedWords.Select(word => word.Text)),
                BuildLineRect(remappedWords),
                null,
                remappedWords.Select(word => word.WordId).ToArray()));
        }

        return new RecognizedDocument(
            Guid.NewGuid(),
            sourcePath,
            originalWidth,
            originalHeight,
            combinedLines.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, combinedLines.Select(line => line.Text)),
            combinedLines,
            combinedWords,
            DateTime.UtcNow,
            0,
            OcrEngineSelector.FastEngineId,
            "eng+rus line-routed");
    }

    private static RecognizedDocument MapResultToDocument(
        string sourcePath,
        OcrResult result,
        Rect region,
        double scaleX,
        double scaleY,
        int originalWidth,
        int originalHeight,
        OcrLanguageMode languageMode,
        string? resolvedTag)
    {
        var words = new List<RecognizedWord>();
        var lines = new List<RecognizedLine>();

        for (var lineIndex = 0; lineIndex < result.Lines.Count; lineIndex++)
        {
            var line = result.Lines[lineIndex];
            var lineWords = new List<RecognizedWord>();
            var lineWordIds = new List<Guid>();

            foreach (var word in line.Words)
            {
                var normalized = NormalizeWord(word.Text);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                var wordId = Guid.NewGuid();
                lineWordIds.Add(wordId);
                var rect = word.BoundingRect;
                var translatedRect = new RectD(
                    (region.X + rect.X) * scaleX,
                    (region.Y + rect.Y) * scaleY,
                    rect.Width * scaleX,
                    rect.Height * scaleY);

                var recognizedWord = new RecognizedWord(
                    wordId,
                    words.Count,
                    lineIndex,
                    word.Text,
                    normalized,
                    translatedRect,
                    null,
                    null);
                words.Add(recognizedWord);
                lineWords.Add(recognizedWord);
            }

            if (lineWords.Count == 0)
            {
                continue;
            }

            lines.Add(new RecognizedLine(
                Guid.NewGuid(),
                lines.Count,
                string.IsNullOrWhiteSpace(line.Text) ? string.Join(' ', lineWords.Select(word => word.Text)) : line.Text,
                BuildLineRect(lineWords),
                null,
                lineWordIds.ToArray()));
        }

        return new RecognizedDocument(
            Guid.NewGuid(),
            sourcePath,
            originalWidth,
            originalHeight,
            string.IsNullOrWhiteSpace(result.Text)
                ? string.Join(Environment.NewLine, lines.Select(line => line.Text))
                : result.Text,
            lines,
            words,
            DateTime.UtcNow,
            0,
            OcrEngineSelector.FastEngineId,
            GetLanguageHint(languageMode, resolvedTag));
    }

    private static List<Rect> SegmentLineRegions(byte[] pixels, int width, int height)
    {
        var threshold = GetForegroundThreshold(pixels);
        var rowInk = new int[height];

        for (var y = 0; y < height; y++)
        {
            var rowCount = 0;
            for (var x = 0; x < width; x++)
            {
                var index = ((y * width) + x) * 4;
                if (pixels[index] <= threshold)
                {
                    rowCount++;
                }
            }

            rowInk[y] = rowCount;
        }

        var minActivePixelsPerRow = Math.Max(6, width / 90);
        var maxGapRows = Math.Clamp(height / 220, 1, 4);
        var verticalPadding = Math.Clamp(height / 180, 2, 6);
        var lineRegions = new List<Rect>();

        var startRow = -1;
        var gapCount = 0;
        for (var y = 0; y < height; y++)
        {
            if (rowInk[y] >= minActivePixelsPerRow)
            {
                if (startRow < 0)
                {
                    startRow = y;
                }

                gapCount = 0;
                continue;
            }

            if (startRow < 0)
            {
                continue;
            }

            gapCount++;
            if (gapCount <= maxGapRows)
            {
                continue;
            }

            AddLineRegion(startRow, y - gapCount, verticalPadding);
            startRow = -1;
            gapCount = 0;
        }

        if (startRow >= 0)
        {
            AddLineRegion(startRow, height - 1, verticalPadding);
        }

        return MergeCloseRegions(lineRegions, height);

        void AddLineRegion(int topRow, int bottomRow, int padding)
        {
            var paddedTop = Math.Max(0, topRow - padding);
            var paddedBottom = Math.Min(height - 1, bottomRow + padding);
            if (paddedBottom - paddedTop < 5)
            {
                return;
            }

            var colInk = new int[width];
            for (var y = paddedTop; y <= paddedBottom; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = ((y * width) + x) * 4;
                    if (pixels[index] <= threshold)
                    {
                        colInk[x]++;
                    }
                }
            }

            var minActivePixelsPerColumn = Math.Max(1, (paddedBottom - paddedTop + 1) / 8);
            var left = 0;
            while (left < width && colInk[left] < minActivePixelsPerColumn)
            {
                left++;
            }

            var right = width - 1;
            while (right >= left && colInk[right] < minActivePixelsPerColumn)
            {
                right--;
            }

            if (right < left)
            {
                left = 0;
                right = width - 1;
            }

            var horizontalPadding = Math.Clamp((paddedBottom - paddedTop + 1), 8, 24);
            left = Math.Max(0, left - horizontalPadding);
            right = Math.Min(width - 1, right + horizontalPadding);

            lineRegions.Add(new Rect(left, paddedTop, Math.Max(1, right - left + 1), paddedBottom - paddedTop + 1));
        }
    }

    private static List<Rect> MergeCloseRegions(IReadOnlyList<Rect> regions, int imageHeight)
    {
        if (regions.Count <= 1)
        {
            return regions.ToList();
        }

        var ordered = regions.OrderBy(region => region.Y).ToList();
        var merged = new List<Rect> { ordered[0] };
        var maxGap = Math.Clamp(imageHeight / 240, 1, 4);

        for (var index = 1; index < ordered.Count; index++)
        {
            var current = ordered[index];
            var previous = merged[^1];
            var gap = current.Y - (previous.Y + previous.Height);
            if (gap > maxGap)
            {
                merged.Add(current);
                continue;
            }

            var left = Math.Min(previous.X, current.X);
            var top = Math.Min(previous.Y, current.Y);
            var right = Math.Max(previous.X + previous.Width, current.X + current.Width);
            var bottom = Math.Max(previous.Y + previous.Height, current.Y + current.Height);
            merged[^1] = new Rect(left, top, right - left, bottom - top);
        }

        return merged;
    }

    private static List<Rect> BuildFallbackRegions(int width, int height)
    {
        var sliceHeight = Math.Clamp(height / 4, 96, 220);
        var overlap = Math.Clamp(sliceHeight / 6, 12, 32);
        var regions = new List<Rect>();

        for (var top = 0; top < height; top += Math.Max(32, sliceHeight - overlap))
        {
            var actualHeight = Math.Min(sliceHeight, height - top);
            if (actualHeight <= 0)
            {
                break;
            }

            regions.Add(new Rect(0, top, width, actualHeight));
            if (top + actualHeight >= height)
            {
                break;
            }
        }

        return regions;
    }

    private static int GetForegroundThreshold(byte[] pixels)
    {
        var min = 255;
        long sum = 0;
        var count = pixels.Length / 4;
        for (var index = 0; index < pixels.Length; index += 4)
        {
            var value = pixels[index];
            min = Math.Min(min, value);
            sum += value;
        }

        var average = count == 0 ? 255 : (int)Math.Round(sum / (double)count);
        return Math.Clamp((int)Math.Round((average * 0.78d) + (min * 0.22d)), 72, 176);
    }

    private static SoftwareBitmap CropBitmap(byte[] pixels, int sourceWidth, int sourceHeight, Rect region)
    {
        var x = Math.Max(0, (int)Math.Floor(region.X));
        var y = Math.Max(0, (int)Math.Floor(region.Y));
        var width = Math.Min(sourceWidth - x, Math.Max(1, (int)Math.Ceiling(region.Width)));
        var height = Math.Min(sourceHeight - y, Math.Max(1, (int)Math.Ceiling(region.Height)));

        var croppedPixels = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            var sourceOffset = (((y + row) * sourceWidth) + x) * 4;
            var targetOffset = row * width * 4;
            Array.Copy(pixels, sourceOffset, croppedPixels, targetOffset, width * 4);
        }

        return SoftwareBitmap.CreateCopyFromBuffer(
            croppedPixels.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Premultiplied);
    }

    private static byte[] CopyPixels(SoftwareBitmap bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyToBuffer(pixels.AsBuffer());
        return pixels;
    }

    private static string NormalizeWord(string? rawWord)
        => string.IsNullOrWhiteSpace(rawWord)
            ? string.Empty
            : string.Join(' ', rawWord.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static RectD BuildLineRect(IReadOnlyList<RecognizedWord> lineWords)
    {
        if (lineWords.Count == 0)
        {
            return new RectD(0d, 0d, 0d, 0d);
        }

        var left = lineWords.Min(word => word.BoundingRect.Left);
        var top = lineWords.Min(word => word.BoundingRect.Top);
        var right = lineWords.Max(word => word.BoundingRect.Right);
        var bottom = lineWords.Max(word => word.BoundingRect.Bottom);
        return new RectD(left, top, right - left, bottom - top);
    }

    private static int CountProtectedTokenHits(string text)
        => ProtectedTokens.Count(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string? GetLanguageHint(OcrLanguageMode languageMode, string? resolvedTag)
        => languageMode is OcrLanguageMode.EnglishRussian or OcrLanguageMode.Auto
            ? $"eng+rus ({resolvedTag ?? "line-routed"})"
            : resolvedTag;

    private static OcrEngine CreateEngine(OcrLanguageMode languageMode)
    {
        var languageHint = languageMode switch
        {
            OcrLanguageMode.English => "en-US",
            OcrLanguageMode.Russian => "ru-RU",
            _ => null,
        };

        if (!string.IsNullOrWhiteSpace(languageHint))
        {
            var requestedLanguage = new Language(languageHint);
            if (OcrEngine.IsLanguageSupported(requestedLanguage))
            {
                var engineForLanguage = OcrEngine.TryCreateFromLanguage(requestedLanguage);
                if (engineForLanguage is not null)
                {
                    return engineForLanguage;
                }
            }
        }

        return OcrEngine.TryCreateFromUserProfileLanguages()
            ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"))
            ?? throw new InvalidOperationException("Windows OCR is not available on this machine.");
    }

    private enum LineScriptTendency
    {
        English = 0,
        Russian = 1,
        Mixed = 2,
    }
}
