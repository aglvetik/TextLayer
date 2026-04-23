namespace TextLayer.Infrastructure.Ocr;

public sealed class TesseractPreprocessingPlanner
{
    public TesseractPreprocessingPlan CreatePlan(OcrImageAnalysis analysis, int imageWidth, int imageHeight)
    {
        var largestDimension = Math.Max(imageWidth, imageHeight);
        var imageArea = imageWidth * (double)imageHeight;
        var isLargeCapture = largestDimension >= 2200 || imageArea >= 2_400_000d;
        var preferredScale = analysis.LikelySmallText
            ? 2.05d
            : analysis.IsLowContrast || analysis.IsDarkBackground || analysis.LikelyChatScreenshot
                ? 1.55d
                : 1.2d;
        var maxAllowedDimension = analysis.LikelySmallText || analysis.IsLowContrast || analysis.IsDarkBackground
            ? 5200d
            : 4600d;
        var scaleFactor = Math.Min(preferredScale, maxAllowedDimension / Math.Max(1d, largestDimension));
        if (!isLargeCapture && scaleFactor < 1d)
        {
            scaleFactor = 1d;
        }

        return new TesseractPreprocessingPlan(
            ScaleFactor: scaleFactor,
            UseNeutralGrayscalePass: analysis.IsDarkBackground || analysis.IsLowContrast || analysis.LikelySmallText || isLargeCapture,
            UseBinarizedPass: analysis.IsLowContrast || analysis.LikelySmallText || isLargeCapture,
            UseDarkUiPass: analysis.IsDarkBackground,
            UseLowContrastPass: !analysis.IsDarkBackground && (analysis.IsLowContrast || analysis.LikelySmallText),
            UseSmallTextPass: analysis.LikelySmallText,
            UseAccentTextPass: analysis.IsDarkBackground && (analysis.IsLowContrast || analysis.LikelySmallText || analysis.LikelyChatScreenshot || isLargeCapture),
            UseInvertedAccentPass: analysis.IsDarkBackground && (analysis.LikelySmallText || analysis.LikelyChatScreenshot));
    }
}
