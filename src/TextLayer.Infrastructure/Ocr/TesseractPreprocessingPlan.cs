namespace TextLayer.Infrastructure.Ocr;

public sealed record TesseractPreprocessingPlan(
    double ScaleFactor,
    bool UseNeutralGrayscalePass,
    bool UseBinarizedPass,
    bool UseDarkUiPass,
    bool UseLowContrastPass,
    bool UseSmallTextPass,
    bool UseAccentTextPass,
    bool UseInvertedAccentPass);
