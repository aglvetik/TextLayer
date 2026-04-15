namespace TextLayer.Infrastructure.Ocr;

public sealed record OcrImageAnalysis(
    int PixelWidth,
    int PixelHeight,
    double AverageLuminance,
    int ContrastRange,
    double EdgeDensity,
    bool IsDarkBackground,
    bool IsLowContrast,
    bool LikelySmallText,
    bool LikelyChatScreenshot);
