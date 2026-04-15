namespace TextLayer.Application.Models;

public sealed record AppSettings
{
    public bool IsOverlayEnabled { get; init; } = true;

    public bool LaunchAtStartup { get; init; }

    public bool CloseToTrayOnClose { get; init; } = true;

    public bool AutoRunOcrOnOpen { get; init; } = true;

    public OcrMode OcrMode { get; init; } = OcrMode.Auto;

    public OcrLanguageMode OcrLanguageMode { get; init; } = OcrLanguageMode.English;

    public UiLanguagePreference UiLanguagePreference { get; init; } = UiLanguagePreference.English;

    public ThemePreference ThemePreference { get; init; } = ThemePreference.System;

    public bool ShowDebugBoundsOverlay { get; init; }

    public bool IsSidePanelVisible { get; init; } = true;

    public bool CloseOverlayAfterCopy { get; init; } = true;

    public WindowPlacementSettings WindowPlacement { get; init; } = new();

    public static OcrLanguageMode NormalizeVisibleOcrLanguageMode(OcrLanguageMode languageMode)
        => languageMode == OcrLanguageMode.EnglishRussian
            ? OcrLanguageMode.Auto
            : languageMode;

    public static OcrMode NormalizeOcrModeForLanguage(OcrMode mode, OcrLanguageMode languageMode)
        => NormalizeVisibleOcrLanguageMode(languageMode) == OcrLanguageMode.Russian
            ? OcrMode.Accurate
            : mode;

    public static AppSettings NormalizeOcrBehavior(AppSettings settings)
        => NormalizeVisibleOcrLanguageMode(settings.OcrLanguageMode) == OcrLanguageMode.Russian
            && settings.OcrMode != OcrMode.Accurate
                ? settings with { OcrMode = OcrMode.Accurate }
                : settings;
}
