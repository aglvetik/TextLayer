using TextLayer.Application.Models;

namespace TextLayer.Infrastructure.Ocr;

public sealed class OcrEngineSelector
{
    public const string FastEngineId = "windows-media-ocr";
    public const string AccurateEngineId = "tesseract";

    public string SelectEngineId(string sourcePath, OcrRequestOptions request, OcrImageAnalysis analysis)
    {
        if (request.Mode == OcrMode.Fast && request.LanguageMode == OcrLanguageMode.Russian)
        {
            return AccurateEngineId;
        }

        if (request.Mode == OcrMode.Fast)
        {
            return FastEngineId;
        }

        if (request.Mode == OcrMode.Accurate)
        {
            return AccurateEngineId;
        }

        if (request.LanguageMode == OcrLanguageMode.EnglishRussian)
        {
            return AccurateEngineId;
        }

        var sourceHint = sourcePath.ToLowerInvariant();
        var hasChatFilenameHint = sourceHint.Contains("discord", StringComparison.Ordinal)
            || sourceHint.Contains("telegram", StringComparison.Ordinal)
            || sourceHint.Contains("slack", StringComparison.Ordinal)
            || sourceHint.Contains("chat", StringComparison.Ordinal)
            || sourceHint.Contains("message", StringComparison.Ordinal);

        var prefersAccurate = analysis.IsDarkBackground
            || analysis.IsLowContrast
            || analysis.LikelySmallText
            || analysis.LikelyChatScreenshot
            || hasChatFilenameHint
            || (request.LanguageMode == OcrLanguageMode.EnglishRussian
                && (analysis.PixelWidth >= 1100 || analysis.PixelHeight >= 700));

        return prefersAccurate
            ? AccurateEngineId
            : FastEngineId;
    }

    public IReadOnlyList<string> GetEnginePreferenceOrder(string sourcePath, OcrRequestOptions request, OcrImageAnalysis analysis)
    {
        if (request.Mode == OcrMode.Fast && request.LanguageMode == OcrLanguageMode.Russian)
        {
            return [AccurateEngineId];
        }

        if (request.Mode == OcrMode.Fast)
        {
            return [FastEngineId];
        }

        if (request.Mode == OcrMode.Accurate)
        {
            return [AccurateEngineId];
        }

        if (request.LanguageMode == OcrLanguageMode.EnglishRussian)
        {
            return [AccurateEngineId];
        }

        var primaryEngine = SelectEngineId(sourcePath, request, analysis);
        return primaryEngine == AccurateEngineId
            ? [AccurateEngineId, FastEngineId]
            : [FastEngineId, AccurateEngineId];
    }
}
