namespace TextLayer.Infrastructure.Ocr;

public sealed class TesseractPreprocessingPlanner
{
    public TesseractPreprocessingPlan CreatePlan(OcrImageAnalysis analysis, int imageWidth, int imageHeight)
    {
        var preferredScale = analysis.LikelySmallText || analysis.IsLowContrast
            ? 1.75d
            : 1.2d;
        var maxDimension = Math.Max(imageWidth, imageHeight) * preferredScale;
        var scaleFactor = maxDimension <= 4096d
            ? preferredScale
            : Math.Min(1d, 4096d / Math.Max(imageWidth, imageHeight));

        return new TesseractPreprocessingPlan(
            ScaleFactor: scaleFactor,
            UseDarkUiPass: analysis.IsDarkBackground,
            UseLowContrastPass: !analysis.IsDarkBackground && (analysis.IsLowContrast || analysis.LikelySmallText),
            UseSmallTextPass: analysis.LikelySmallText,
            UseAccentTextPass: analysis.IsDarkBackground && (analysis.IsLowContrast || analysis.LikelySmallText || analysis.LikelyChatScreenshot));
    }
}
