using TextLayer.Application.Models;
using TextLayer.App.Services;

namespace TextLayer.App.ViewModels;

public sealed class SettingsWindowViewModel : ObservableObject
{
    private bool isOverlayEnabled;
    private bool launchAtStartup;
    private bool closeToTrayOnClose;
    private bool autoRunOcrOnOpen;
    private OcrModeOption selectedOcrMode;
    private OcrLanguageOption selectedOcrLanguage;
    private UiLanguageOption selectedUiLanguage;
    private bool showDebugBoundsOverlay;
    private bool isSidePanelVisible;
    private bool closeOverlayAfterCopy;
    private ThemeOption selectedTheme;

    public SettingsWindowViewModel(AppSettings settings)
    {
        ThemeOptions =
        [
            new ThemeOption("Theme.System", ThemePreference.System),
            new ThemeOption("Theme.Light", ThemePreference.Light),
            new ThemeOption("Theme.Dark", ThemePreference.Dark),
        ];
        OcrModeOptions =
        [
            new OcrModeOption("OcrMode.Auto", OcrMode.Auto),
            new OcrModeOption("OcrMode.Fast", OcrMode.Fast),
            new OcrModeOption("OcrMode.Accurate", OcrMode.Accurate),
        ];
        OcrLanguageOptions =
        [
            new OcrLanguageOption("OcrLanguage.Auto", OcrLanguageMode.Auto, isEnabled: false, descriptionKey: "Common.InActiveDevelopment"),
            new OcrLanguageOption("OcrLanguage.English", OcrLanguageMode.English),
            new OcrLanguageOption("OcrLanguage.Russian", OcrLanguageMode.Russian),
        ];
        UiLanguageOptions =
        [
            new UiLanguageOption("UiLanguage.English", UiLanguagePreference.English),
            new UiLanguageOption("UiLanguage.Russian", UiLanguagePreference.Russian),
        ];

        isOverlayEnabled = settings.IsOverlayEnabled;
        launchAtStartup = settings.LaunchAtStartup;
        closeToTrayOnClose = settings.CloseToTrayOnClose;
        autoRunOcrOnOpen = settings.AutoRunOcrOnOpen;
        selectedOcrMode = OcrModeOptions.First(option => option.Value == settings.OcrMode);
        selectedOcrLanguage = OcrLanguageOptions.First(option => option.Value == NormalizeVisibleLanguageMode(settings.OcrLanguageMode));
        selectedUiLanguage = UiLanguageOptions.First(option => option.Value == settings.UiLanguagePreference);
        showDebugBoundsOverlay = settings.ShowDebugBoundsOverlay;
        isSidePanelVisible = settings.IsSidePanelVisible;
        closeOverlayAfterCopy = settings.CloseOverlayAfterCopy;
        selectedTheme = ThemeOptions.First(option => option.Value == settings.ThemePreference);
    }

    public IReadOnlyList<ThemeOption> ThemeOptions { get; }

    public IReadOnlyList<OcrModeOption> OcrModeOptions { get; }

    public IReadOnlyList<OcrLanguageOption> OcrLanguageOptions { get; }

    public IReadOnlyList<UiLanguageOption> UiLanguageOptions { get; }

    public bool IsOverlayEnabled
    {
        get => isOverlayEnabled;
        set => SetProperty(ref isOverlayEnabled, value);
    }

    public bool LaunchAtStartup
    {
        get => launchAtStartup;
        set => SetProperty(ref launchAtStartup, value);
    }

    public bool CloseToTrayOnClose
    {
        get => closeToTrayOnClose;
        set => SetProperty(ref closeToTrayOnClose, value);
    }

    public bool AutoRunOcrOnOpen
    {
        get => autoRunOcrOnOpen;
        set => SetProperty(ref autoRunOcrOnOpen, value);
    }

    public OcrModeOption SelectedOcrMode
    {
        get => selectedOcrMode;
        set => SetProperty(ref selectedOcrMode, value);
    }

    public OcrLanguageOption SelectedOcrLanguage
    {
        get => selectedOcrLanguage;
        set
        {
            if (!value.IsEnabled)
            {
                return;
            }

            if (SetProperty(ref selectedOcrLanguage, value))
            {
                OnPropertyChanged(nameof(OcrRecommendationText));
            }
        }
    }

    public UiLanguageOption SelectedUiLanguage
    {
        get => selectedUiLanguage;
        set => SetProperty(ref selectedUiLanguage, value);
    }

    public bool ShowDebugBoundsOverlay
    {
        get => showDebugBoundsOverlay;
        set => SetProperty(ref showDebugBoundsOverlay, value);
    }

    public bool IsSidePanelVisible
    {
        get => isSidePanelVisible;
        set => SetProperty(ref isSidePanelVisible, value);
    }

    public bool CloseOverlayAfterCopy
    {
        get => closeOverlayAfterCopy;
        set => SetProperty(ref closeOverlayAfterCopy, value);
    }

    public ThemeOption SelectedTheme
    {
        get => selectedTheme;
        set => SetProperty(ref selectedTheme, value);
    }

    public string OcrRecommendationText => SelectedOcrLanguage.Value switch
    {
        OcrLanguageMode.Auto => UiTextService.Instance["ControlCenter.OcrRecommendation.Auto"],
        OcrLanguageMode.Russian => UiTextService.Instance["ControlCenter.OcrRecommendation.Russian"],
        _ => UiTextService.Instance["ControlCenter.OcrRecommendation.English"],
    };

    public AppSettings ToSettings(WindowPlacementSettings placement)
        => new()
        {
            IsOverlayEnabled = IsOverlayEnabled,
            LaunchAtStartup = LaunchAtStartup,
            CloseToTrayOnClose = CloseToTrayOnClose,
            AutoRunOcrOnOpen = AutoRunOcrOnOpen,
            OcrMode = SelectedOcrMode.Value,
            OcrLanguageMode = SelectedOcrLanguage.Value,
            UiLanguagePreference = SelectedUiLanguage.Value,
            ThemePreference = SelectedTheme.Value,
            ShowDebugBoundsOverlay = ShowDebugBoundsOverlay,
            IsSidePanelVisible = IsSidePanelVisible,
            CloseOverlayAfterCopy = CloseOverlayAfterCopy,
            WindowPlacement = placement,
        };

    private static OcrLanguageMode NormalizeVisibleLanguageMode(OcrLanguageMode languageMode)
        => languageMode == OcrLanguageMode.EnglishRussian
            ? OcrLanguageMode.Auto
            : languageMode;
}
