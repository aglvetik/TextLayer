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
        settings = AppSettings.NormalizeOcrBehavior(settings);

        ThemeOptions =
        [
            new ThemeOption("Theme.System", ThemePreference.System),
            new ThemeOption("Theme.Light", ThemePreference.Light),
            new ThemeOption("Theme.Dark", ThemePreference.Dark),
        ];
        OcrLanguageOptions =
        [
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
        selectedOcrLanguage = OcrLanguageOptions.First(option => option.Value == AppSettings.NormalizeVisibleOcrLanguageMode(settings.OcrLanguageMode));
        OcrModeOptions = CreateOcrModeOptions(selectedOcrLanguage.Value);
        selectedOcrMode = OcrModeOptions.First(option => option.Value == AppSettings.NormalizeOcrModeForLanguage(settings.OcrMode, selectedOcrLanguage.Value));
        selectedUiLanguage = UiLanguageOptions.First(option => option.Value == settings.UiLanguagePreference);
        showDebugBoundsOverlay = settings.ShowDebugBoundsOverlay;
        isSidePanelVisible = settings.IsSidePanelVisible;
        closeOverlayAfterCopy = settings.CloseOverlayAfterCopy;
        selectedTheme = ThemeOptions.First(option => option.Value == settings.ThemePreference);
    }

    public IReadOnlyList<ThemeOption> ThemeOptions { get; }

    public IReadOnlyList<OcrModeOption> OcrModeOptions { get; private set; } = [];

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
        set
        {
            if (!value.IsEnabled)
            {
                return;
            }

            var normalizedMode = AppSettings.NormalizeOcrModeForLanguage(value.Value, SelectedOcrLanguage.Value);
            var resolvedOption = OcrModeOptions.First(option => option.Value == normalizedMode);
            SetProperty(ref selectedOcrMode, resolvedOption);
        }
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
                OcrModeOptions = CreateOcrModeOptions(value.Value);
                selectedOcrMode = OcrModeOptions.First(option => option.Value == AppSettings.NormalizeOcrModeForLanguage(selectedOcrMode.Value, value.Value));
                OnPropertyChanged(nameof(OcrModeOptions));
                OnPropertyChanged(nameof(SelectedOcrMode));
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
            OcrMode = AppSettings.NormalizeOcrModeForLanguage(SelectedOcrMode.Value, SelectedOcrLanguage.Value),
            OcrLanguageMode = SelectedOcrLanguage.Value,
            UiLanguagePreference = SelectedUiLanguage.Value,
            ThemePreference = SelectedTheme.Value,
            ShowDebugBoundsOverlay = ShowDebugBoundsOverlay,
            IsSidePanelVisible = IsSidePanelVisible,
            CloseOverlayAfterCopy = CloseOverlayAfterCopy,
            WindowPlacement = placement,
        };

    private static IReadOnlyList<OcrModeOption> CreateOcrModeOptions(OcrLanguageMode languageMode)
        => AppSettings.NormalizeVisibleOcrLanguageMode(languageMode) == OcrLanguageMode.Russian
            ?
            [
                new OcrModeOption(
                    "OcrMode.Fast",
                    OcrMode.Fast,
                    isEnabled: false,
                    descriptionKey: "Common.InDevelopmentForRussian"),
                new OcrModeOption("OcrMode.Accurate", OcrMode.Accurate),
            ]
            :
            [
                new OcrModeOption("OcrMode.Fast", OcrMode.Fast),
                new OcrModeOption("OcrMode.Accurate", OcrMode.Accurate),
            ];
}
