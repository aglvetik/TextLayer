namespace TextLayer.Infrastructure.Ocr;

public sealed record TesseractPreprocessingPlan(
    double ScaleFactor,
    bool UseDarkUiPass,
    bool UseLowContrastPass,
    bool UseSmallTextPass,
    bool UseAccentTextPass);
