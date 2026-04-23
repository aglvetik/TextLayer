using TextLayer.Application.Models;
using TextLayer.Infrastructure.Ocr;

namespace TextLayer.Tests.Infrastructure;

public sealed class OcrEngineSelectorTests
{
    private readonly OcrEngineSelector selector = new();

    [Fact]
    public void AutoMode_PrefersAccurateEngine_ForDarkLowContrastScreenshot()
    {
        var analysis = new OcrImageAnalysis(
            PixelWidth: 1440,
            PixelHeight: 900,
            AverageLuminance: 82,
            ContrastRange: 95,
            EdgeDensity: 0.14,
            IsDarkBackground: true,
            IsLowContrast: true,
            LikelySmallText: true,
            LikelyChatScreenshot: true);

        var selected = selector.SelectEngineId(
            "discord-capture.png",
            new OcrRequestOptions(OcrMode.Auto, OcrLanguageMode.Auto),
            analysis);

        Assert.Equal(OcrEngineSelector.AccurateEngineId, selected);
    }

    [Fact]
    public void AutoMode_PrefersFastEngine_ForCleanBrightImage()
    {
        var analysis = new OcrImageAnalysis(
            PixelWidth: 900,
            PixelHeight: 600,
            AverageLuminance: 220,
            ContrastRange: 180,
            EdgeDensity: 0.03,
            IsDarkBackground: false,
            IsLowContrast: false,
            LikelySmallText: false,
            LikelyChatScreenshot: false);

        var selected = selector.SelectEngineId(
            "scan.png",
            new OcrRequestOptions(OcrMode.Auto, OcrLanguageMode.English),
            analysis);

        Assert.Equal(OcrEngineSelector.FastEngineId, selected);
    }

    [Fact]
    public void ExplicitAccurateMode_AlwaysChoosesTesseract()
    {
        var analysis = new OcrImageAnalysis(800, 600, 200, 180, 0.02, false, false, false, false);

        var selected = selector.SelectEngineId(
            "clean-image.png",
            new OcrRequestOptions(OcrMode.Accurate, OcrLanguageMode.English),
            analysis);

        Assert.Equal(OcrEngineSelector.AccurateEngineId, selected);
    }

    [Fact]
    public void FastMode_RoutesRussianToReliableCyrillicPath()
    {
        var analysis = new OcrImageAnalysis(
            PixelWidth: 1280,
            PixelHeight: 720,
            AverageLuminance: 214,
            ContrastRange: 176,
            EdgeDensity: 0.03,
            IsDarkBackground: false,
            IsLowContrast: false,
            LikelySmallText: false,
            LikelyChatScreenshot: false);

        var selected = selector.SelectEngineId(
            "russian-ui.png",
            new OcrRequestOptions(OcrMode.Fast, OcrLanguageMode.Russian),
            analysis);

        Assert.Equal(OcrEngineSelector.AccurateEngineId, selected);
    }

    [Fact]
    public void MixedLanguageFast_UsesFastBranchOrchestration()
    {
        var analysis = new OcrImageAnalysis(
            PixelWidth: 900,
            PixelHeight: 600,
            AverageLuminance: 220,
            ContrastRange: 180,
            EdgeDensity: 0.03,
            IsDarkBackground: false,
            IsLowContrast: false,
            LikelySmallText: false,
            LikelyChatScreenshot: false);

        var selected = selector.SelectEngineId(
            "mixed-ui.png",
            new OcrRequestOptions(OcrMode.Fast, OcrLanguageMode.EnglishRussian),
            analysis);

        Assert.Equal(OcrEngineSelector.FastEngineId, selected);
    }
}
