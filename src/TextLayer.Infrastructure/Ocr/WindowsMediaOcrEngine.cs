using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TextLayer.Infrastructure.Ocr;

public sealed class WindowsMediaOcrEngine : IOcrEngine
{
    private readonly ScriptAwareOcrPostProcessor postProcessor = new();
    private readonly WindowsMixedFastOcrPipeline mixedFastPipeline = new();

    public async Task<RecognizedDocument> RecognizeAsync(string sourcePath, OcrRequestOptions request, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException("The selected image could not be found.");
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var file = await StorageFile.GetFileFromPathAsync(sourcePath).AsTask(cancellationToken).ConfigureAwait(false);
            using IRandomAccessStream stream = await file.OpenReadAsync().AsTask(cancellationToken).ConfigureAwait(false);
            var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
            var orientedWidth = (int)decoder.OrientedPixelWidth;
            var orientedHeight = (int)decoder.OrientedPixelHeight;

            var ocrScaleFactor = GetOcrScaleFactor(orientedWidth, orientedHeight);
            var scaledWidth = Math.Max(1, (int)Math.Round(orientedWidth * ocrScaleFactor));
            var scaledHeight = Math.Max(1, (int)Math.Round(orientedHeight * ocrScaleFactor));
            var bitmapTransform = new BitmapTransform
            {
                ScaledWidth = (uint)scaledWidth,
                ScaledHeight = (uint)scaledHeight,
                InterpolationMode = BitmapInterpolationMode.Fant,
            };

            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    bitmapTransform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var tone = AnalyzeBitmapTone(softwareBitmap);
            if (request.Mode == OcrMode.Fast && request.LanguageMode == OcrLanguageMode.EnglishRussian)
            {
                var mixedDocument = await mixedFastPipeline.RecognizeAsync(
                        sourcePath,
                        softwareBitmap,
                        orientedWidth,
                        orientedHeight,
                        tone.IsDarkBackground,
                        preferEnhancedBitmap: tone.IsDarkBackground || tone.ContrastRange < 165,
                        cancellationToken)
                    .ConfigureAwait(false);

                stopwatch.Stop();
                return mixedDocument with { RecognitionDurationMs = stopwatch.ElapsedMilliseconds };
            }

            var engine = CreateEngine(request.LanguageMode);
            var result = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken).ConfigureAwait(false);

            if (ShouldTryEnhancedPass(tone, result))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var enhancedBitmap = CreateScreenshotOptimizedBitmap(softwareBitmap, tone.IsDarkBackground);
                var enhancedResult = await engine.RecognizeAsync(enhancedBitmap).AsTask(cancellationToken).ConfigureAwait(false);
                if (IsBetterResult(enhancedResult, result))
                {
                    result = enhancedResult;
                }
            }

            stopwatch.Stop();

            var scaleX = orientedWidth / (double)softwareBitmap.PixelWidth;
            var scaleY = orientedHeight / (double)softwareBitmap.PixelHeight;
            var words = new List<RecognizedWord>();
            var lines = new List<RecognizedLine>();

            for (var lineIndex = 0; lineIndex < result.Lines.Count; lineIndex++)
            {
                var line = result.Lines[lineIndex];
                var lineWordIds = new List<Guid>();
                var lineWords = new List<RecognizedWord>();

                foreach (var word in line.Words)
                {
                    var normalized = NormalizeWord(word.Text);
                    if (string.IsNullOrWhiteSpace(normalized))
                    {
                        continue;
                    }

                    var wordId = Guid.NewGuid();
                    lineWordIds.Add(wordId);
                    var recognizedWord = new RecognizedWord(
                        WordId: wordId,
                        Index: words.Count,
                        LineIndex: lineIndex,
                        Text: word.Text,
                        NormalizedText: normalized,
                        BoundingRect: ScaleRect(word.BoundingRect, scaleX, scaleY),
                        BoundingPolygon: null,
                        Confidence: null);
                    words.Add(recognizedWord);
                    lineWords.Add(recognizedWord);
                }

                lines.Add(new RecognizedLine(
                    LineId: Guid.NewGuid(),
                    Index: lineIndex,
                    Text: string.IsNullOrWhiteSpace(line.Text) ? string.Join(' ', lineWords.Select(w => w.Text)) : line.Text,
                    BoundingRect: BuildLineRect(lineWords),
                    BoundingPolygon: null,
                    WordIds: lineWordIds));
            }

            var document = new RecognizedDocument(
                DocumentId: Guid.NewGuid(),
                SourcePath: sourcePath,
                ImagePixelWidth: orientedWidth,
                ImagePixelHeight: orientedHeight,
                FullText: string.IsNullOrWhiteSpace(result.Text)
                    ? string.Join(Environment.NewLine, lines.Select(line => line.Text))
                    : result.Text,
                Lines: lines,
                Words: words,
                CreatedAtUtc: DateTime.UtcNow,
                RecognitionDurationMs: stopwatch.ElapsedMilliseconds,
                OcrEngineId: OcrEngineSelector.FastEngineId,
                LanguageHint: GetLanguageHint(request.LanguageMode, engine.RecognizerLanguage?.LanguageTag));
            return postProcessor.Process(
                    document,
                    request.LanguageMode,
                    RecognizedDocumentNoiseFilter.NoiseFilterProfile.Standard)
                .Document;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "TextLayer could not recognize text from this image. Windows OCR may be unavailable or the file could not be processed.",
                exception);
        }
    }

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

    private static double GetOcrScaleFactor(int width, int height)
    {
        var maxDimension = (double)OcrEngine.MaxImageDimension;
        var largestDimension = Math.Max(width, height);
        var preferredScale = largestDimension switch
        {
            <= 1600 => 1.45d,
            <= 2400 => 1.2d,
            _ => 1d,
        };

        var limitedScale = Math.Min(maxDimension / width, maxDimension / height);
        if (limitedScale >= preferredScale)
        {
            return preferredScale;
        }

        return Math.Min(1d, limitedScale);
    }

    private static RectD ScaleRect(Windows.Foundation.Rect rect, double scaleX, double scaleY)
        => new(rect.X * scaleX, rect.Y * scaleY, rect.Width * scaleX, rect.Height * scaleY);

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

    private static string NormalizeWord(string? rawWord)
        => string.IsNullOrWhiteSpace(rawWord)
            ? string.Empty
            : string.Join(' ', rawWord.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static BitmapToneAnalysis AnalyzeBitmapTone(SoftwareBitmap bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyToBuffer(pixels.AsBuffer());

        var sampleStride = Math.Max(4, Math.Min(bitmap.PixelWidth, bitmap.PixelHeight) / 120);
        double luminanceSum = 0d;
        var sampleCount = 0;
        byte minLuminance = byte.MaxValue;
        byte maxLuminance = byte.MinValue;

        for (var y = 0; y < bitmap.PixelHeight; y += sampleStride)
        {
            for (var x = 0; x < bitmap.PixelWidth; x += sampleStride)
            {
                var pixelIndex = ((y * bitmap.PixelWidth) + x) * 4;
                var luminance = GetLuminance(pixels[pixelIndex + 2], pixels[pixelIndex + 1], pixels[pixelIndex]);
                luminanceSum += luminance;
                sampleCount++;
                minLuminance = (byte)Math.Min(minLuminance, luminance);
                maxLuminance = (byte)Math.Max(maxLuminance, luminance);
            }
        }

        var averageLuminance = sampleCount == 0 ? 255d : luminanceSum / sampleCount;
        return new BitmapToneAnalysis(
            AverageLuminance: averageLuminance,
            ContrastRange: maxLuminance - minLuminance,
            IsDarkBackground: averageLuminance < 120d);
    }

    private static bool ShouldTryEnhancedPass(BitmapToneAnalysis tone, OcrResult result)
        => tone.IsDarkBackground
           || tone.ContrastRange < 160
           || GetWordCount(result) < 24;

    private static SoftwareBitmap CreateScreenshotOptimizedBitmap(SoftwareBitmap source, bool invert)
        => OcrScreenshotPreprocessor.CreateAccentAwareGrayscaleBitmap(source, invert);

    private static double GetResultScore(OcrResult result)
        => (GetWordCount(result) * 12d) + (result.Text?.Count(static character => !char.IsWhiteSpace(character)) ?? 0);

    private static bool IsBetterResult(OcrResult candidate, OcrResult baseline)
    {
        var baselineWords = GetWordCount(baseline);
        var candidateWords = GetWordCount(candidate);
        if (baselineWords == 0)
        {
            return candidateWords > 0;
        }

        var scoreDelta = GetResultScore(candidate) - GetResultScore(baseline);
        var wordGain = candidateWords - baselineWords;
        return wordGain >= 2 && scoreDelta > 10d;
    }

    private static int GetWordCount(OcrResult result)
        => result.Lines.Sum(line => line.Words.Count);

    private static int GetLuminance(byte red, byte green, byte blue)
        => (int)Math.Round((red * 0.299d) + (green * 0.587d) + (blue * 0.114d));

    private static string? GetLanguageHint(OcrLanguageMode languageMode, string? resolvedTag)
        => languageMode is OcrLanguageMode.EnglishRussian or OcrLanguageMode.Auto
            ? $"eng+rus ({resolvedTag ?? "user-profile"})"
            : resolvedTag;

    private sealed record BitmapToneAnalysis(double AverageLuminance, int ContrastRange, bool IsDarkBackground);
}
