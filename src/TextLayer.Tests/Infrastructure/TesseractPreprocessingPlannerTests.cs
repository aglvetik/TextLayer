using TextLayer.Infrastructure.Ocr;

namespace TextLayer.Tests.Infrastructure;

public sealed class TesseractPreprocessingPlannerTests
{
    [Fact]
    public void CreatePlan_UsesAggressiveProcessing_ForDarkSmallTextScreenshots()
    {
        var planner = new TesseractPreprocessingPlanner();
        var analysis = new OcrImageAnalysis(
            PixelWidth: 1800,
            PixelHeight: 1000,
            AverageLuminance: 76,
            ContrastRange: 88,
            EdgeDensity: 0.16,
            IsDarkBackground: true,
            IsLowContrast: true,
            LikelySmallText: true,
            LikelyChatScreenshot: true);

        var plan = planner.CreatePlan(analysis, analysis.PixelWidth, analysis.PixelHeight);

        Assert.True(plan.UseDarkUiPass);
        Assert.True(plan.UseSmallTextPass);
        Assert.True(plan.UseAccentTextPass);
        Assert.True(plan.ScaleFactor > 1d);
    }

    [Fact]
    public void CreatePlan_KeepsCleanImagesLightweight()
    {
        var planner = new TesseractPreprocessingPlanner();
        var analysis = new OcrImageAnalysis(
            PixelWidth: 900,
            PixelHeight: 700,
            AverageLuminance: 210,
            ContrastRange: 170,
            EdgeDensity: 0.03,
            IsDarkBackground: false,
            IsLowContrast: false,
            LikelySmallText: false,
            LikelyChatScreenshot: false);

        var plan = planner.CreatePlan(analysis, analysis.PixelWidth, analysis.PixelHeight);

        Assert.False(plan.UseDarkUiPass);
        Assert.False(plan.UseLowContrastPass);
        Assert.False(plan.UseSmallTextPass);
        Assert.False(plan.UseAccentTextPass);
        Assert.Equal(1.2d, plan.ScaleFactor, 3);
    }
}
