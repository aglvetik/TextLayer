namespace TextLayer.Application.Models;

public sealed record AppSettings
{
    public bool IsOverlayEnabled { get; init; } = true;

    public bool LaunchAtStartup { get; init; }

    public bool CloseToTrayOnClose { get; init; } = true;

    public bool AutoRunOcrOnOpen { get; init; } = true;

    public OcrMode OcrMode { get; init; } = OcrMode.Fast;

    public OcrLanguageMode OcrLanguageMode { get; init; } = OcrLanguageMode.English;

    public UiLanguagePreference UiLanguagePreference { get; init; } = UiLanguagePreference.English;

    public ThemePreference ThemePreference { get; init; } = ThemePreference.System;

    public bool ShowDebugBoundsOverlay { get; init; }

    public bool IsSidePanelVisible { get; init; } = true;

    public bool CloseOverlayAfterCopy { get; init; } = true;

    public WindowPlacementSettings WindowPlacement { get; init; } = new();

    public static OcrLanguageMode NormalizeVisibleOcrLanguageMode(OcrLanguageMode languageMode)
        => languageMode switch
        {
            OcrLanguageMode.Auto => OcrLanguageMode.English,
            OcrLanguageMode.EnglishRussian => OcrLanguageMode.Russian,
            _ => languageMode,
        };

    public static OcrMode NormalizeOcrModeForLanguage(OcrMode mode, OcrLanguageMode languageMode)
    {
        var visibleLanguage = NormalizeVisibleOcrLanguageMode(languageMode);
        if (visibleLanguage == OcrLanguageMode.Russian)
        {
            return OcrMode.Accurate;
        }

        return mode == OcrMode.Auto ? OcrMode.Fast : mode;
    }

    public static AppSettings NormalizeOcrBehavior(AppSettings settings)
    {
        var visibleLanguage = NormalizeVisibleOcrLanguageMode(settings.OcrLanguageMode);
        return settings with
        {
            OcrLanguageMode = visibleLanguage,
            OcrMode = NormalizeOcrModeForLanguage(settings.OcrMode, visibleLanguage),
        };
    }
}
