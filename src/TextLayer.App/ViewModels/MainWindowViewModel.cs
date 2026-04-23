using System.Reflection;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TextLayer.App.Models;
using TextLayer.App.Services;
using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;
using TextLayer.Application.UseCases;
using TextLayer.Domain.Models;
using TextLayer.Domain.Services;

namespace TextLayer.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ImageDocumentUseCase imageDocumentUseCase;
    private readonly SettingsManager settingsManager;
    private readonly IClipboardService clipboardService;
    private readonly ISelectionEngine selectionEngine;
    private readonly ILogService logService;
    private readonly IFileDialogService fileDialogService;
    private readonly ThemeService themeService;
    private readonly string executablePath;
    private CancellationTokenSource? openImageCancellation;
    private BitmapSource? displayImage;
    private RecognizedDocument? currentDocument;
    private TextSelection? currentSelection;
    private string recognizedText = string.Empty;
    private string? currentImagePath;
    private string statusText = UiTextService.Instance["Status.Ready"];
    private string hoverText = string.Empty;
    private string? currentHoverWord;
    private double zoom = 1d;
    private bool isSidePanelVisible = true;
    private bool showDebugBoundsOverlay;
    private DocumentViewStateKind currentState = DocumentViewStateKind.Empty;
    private AppSettings currentSettings = new();
    private bool isOverlayEnabled = true;
    private bool closeToTrayOnClose;
    private OcrModeOption selectedQuickOcrMode = new("OcrMode.Fast", OcrMode.Fast);
    private OcrLanguageOption selectedQuickOcrLanguage = new("OcrLanguage.English", OcrLanguageMode.English);
    private bool suppressQuickSettingsPersistence;
    private readonly SemaphoreSlim settingsSaveGate = new(1, 1);

    public MainWindowViewModel(
        ImageDocumentUseCase imageDocumentUseCase,
        SettingsManager settingsManager,
        IClipboardService clipboardService,
        ISelectionEngine selectionEngine,
        ILogService logService,
        IFileDialogService fileDialogService,
        ThemeService themeService,
        string executablePath)
    {
        this.imageDocumentUseCase = imageDocumentUseCase;
        this.settingsManager = settingsManager;
        this.clipboardService = clipboardService;
        this.selectionEngine = selectionEngine;
        this.logService = logService;
        this.fileDialogService = fileDialogService;
        this.themeService = themeService;
        this.executablePath = executablePath;

        BuildQuickOptionLists();

        OpenImageCommand = new AsyncRelayCommand(OpenImageAsync);
        ReRecognizeCommand = new AsyncRelayCommand(ReRecognizeAsync, () => HasImage);
        CopyAllTextCommand = new AsyncRelayCommand(CopyAllTextAsync, () => CurrentDocument is not null);
        CopySelectionCommand = new AsyncRelayCommand(CopySelectionAsync, () => HasSelection);
        ClearImageCommand = new RelayCommand(ClearImage, () => HasImage);
        ToggleSidePanelCommand = new RelayCommand(ToggleSidePanel);
    }

    public event EventHandler? SettingsRequested;

    public ICommand OpenImageCommand { get; }

    public ICommand ReRecognizeCommand { get; }

    public ICommand CopyAllTextCommand { get; }

    public ICommand CopySelectionCommand { get; }

    public ICommand ClearImageCommand { get; }

    public ICommand ToggleSidePanelCommand { get; }

    public IReadOnlyList<OcrModeOption> QuickOcrModeOptions { get; private set; } = [];

    public IReadOnlyList<OcrLanguageOption> QuickOcrLanguageOptions { get; private set; } = [];

    public BitmapSource? DisplayImage
    {
        get => displayImage;
        private set
        {
            if (SetProperty(ref displayImage, value))
            {
                OnPropertyChanged(nameof(HasImage));
                OnPropertyChanged(nameof(IsEmptyState));
                OnPropertyChanged(nameof(ShowStateBanner));
                OnPropertyChanged(nameof(WindowTitle));
                NotifyCommandStates();
            }
        }
    }

    public RecognizedDocument? CurrentDocument
    {
        get => currentDocument;
        private set
        {
            if (SetProperty(ref currentDocument, value))
            {
                OnPropertyChanged(nameof(HasRecognizedDocument));
                NotifyCommandStates();
            }
        }
    }

    public TextSelection? CurrentSelection
    {
        get => currentSelection;
        private set
        {
            if (SetProperty(ref currentSelection, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                NotifyCommandStates();
            }
        }
    }

    public string RecognizedText
    {
        get => recognizedText;
        private set => SetProperty(ref recognizedText, value);
    }

    public string? CurrentImagePath
    {
        get => currentImagePath;
        private set
        {
            if (SetProperty(ref currentImagePath, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string HoverText
    {
        get => hoverText;
        private set => SetProperty(ref hoverText, value);
    }

    public string ZoomText => $"{Math.Round(zoom * 100d)}%";

    public bool IsBusy => CurrentState is DocumentViewStateKind.LoadingImage or DocumentViewStateKind.Recognizing;

    public bool HasImage => DisplayImage is not null;

    public bool HasRecognizedDocument => CurrentDocument is not null;

    public bool HasSelection => CurrentSelection is not null && !CurrentSelection.IsEmpty;

    public bool ShowStateBanner => HasImage && CurrentState is DocumentViewStateKind.NoTextFound or DocumentViewStateKind.Error;

    public bool IsEmptyState => CurrentState == DocumentViewStateKind.Empty && !HasImage;

    public bool IsSidePanelVisible
    {
        get => isSidePanelVisible;
        private set => SetProperty(ref isSidePanelVisible, value);
    }

    public bool IsOverlayEnabled
    {
        get => isOverlayEnabled;
        set
        {
            if (!SetProperty(ref isOverlayEnabled, value))
            {
                return;
            }

            CurrentSettings = CurrentSettings with { IsOverlayEnabled = value };
            OnPropertyChanged(nameof(OverlayStatusText));
            OnPropertyChanged(nameof(StateDescription));
            _ = PersistQuickSettingsAsync();
        }
    }

    public bool CloseToTrayOnCloseSetting
    {
        get => closeToTrayOnClose;
        set
        {
            if (!SetProperty(ref closeToTrayOnClose, value))
            {
                return;
            }

            CurrentSettings = CurrentSettings with { CloseToTrayOnClose = value };
            _ = PersistQuickSettingsAsync();
        }
    }

    public OcrModeOption SelectedQuickOcrMode
    {
        get => selectedQuickOcrMode;
        set
        {
            if (!value.IsEnabled)
            {
                return;
            }

            var resolvedMode = AppSettings.NormalizeOcrModeForLanguage(value.Value, SelectedQuickOcrLanguage.Value);
            var resolvedOption = QuickOcrModeOptions.First(option => option.Value == resolvedMode);
            if (!SetProperty(ref selectedQuickOcrMode, resolvedOption))
            {
                return;
            }

            CurrentSettings = AppSettings.NormalizeOcrBehavior(CurrentSettings with { OcrMode = resolvedMode });
            OnPropertyChanged(nameof(StateDescription));
            _ = PersistQuickSettingsAsync();
        }
    }

    public OcrLanguageOption SelectedQuickOcrLanguage
    {
        get => selectedQuickOcrLanguage;
        set
        {
            if (!suppressQuickSettingsPersistence && !value.IsEnabled)
            {
                return;
            }

            if (!SetProperty(ref selectedQuickOcrLanguage, value))
            {
                return;
            }

            BuildQuickOptionLists(value.Value);
            selectedQuickOcrLanguage = QuickOcrLanguageOptions.First(option => option.Value == value.Value);
            OnPropertyChanged(nameof(SelectedQuickOcrLanguage));
            selectedQuickOcrMode = QuickOcrModeOptions.First(option => option.Value == AppSettings.NormalizeOcrModeForLanguage(selectedQuickOcrMode.Value, value.Value));
            OnPropertyChanged(nameof(SelectedQuickOcrMode));
            CurrentSettings = AppSettings.NormalizeOcrBehavior(CurrentSettings with
            {
                OcrLanguageMode = value.Value,
                OcrMode = selectedQuickOcrMode.Value,
            });
            OnPropertyChanged(nameof(OcrRecommendationText));
            OnPropertyChanged(nameof(StateDescription));
            _ = PersistQuickSettingsAsync();
        }
    }

    public bool ShowDebugBoundsOverlay
    {
        get => showDebugBoundsOverlay;
        private set => SetProperty(ref showDebugBoundsOverlay, value);
    }

    public DocumentViewStateKind CurrentState
    {
        get => currentState;
        private set
        {
            if (SetProperty(ref currentState, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsEmptyState));
                OnPropertyChanged(nameof(ShowStateBanner));
                OnPropertyChanged(nameof(StateTitle));
                OnPropertyChanged(nameof(StateDescription));
            }
        }
    }

    public AppSettings CurrentSettings
    {
        get => currentSettings;
        private set => SetProperty(ref currentSettings, value);
    }

    private static UiTextService Localizer => UiTextService.Instance;

    public string OverlayStatusText => IsOverlayEnabled
        ? Localizer["Overlay.Status.Enabled"]
        : Localizer["Overlay.Status.Disabled"];

    public string OcrRecommendationText => GetOcrRecommendationText(SelectedQuickOcrLanguage.Value);

    public string StateTitle => CurrentState switch
    {
        DocumentViewStateKind.Empty => Localizer["State.EmptyTitle"],
        DocumentViewStateKind.LoadingImage => Localizer["State.LoadingImageTitle"],
        DocumentViewStateKind.Recognizing => Localizer["State.RecognizingTitle"],
        DocumentViewStateKind.NoTextFound => Localizer["State.NoTextFoundTitle"],
        DocumentViewStateKind.Error => Localizer["State.ErrorTitle"],
        _ => string.Empty,
    };

    public string StateDescription => CurrentState switch
    {
        DocumentViewStateKind.Empty => IsOverlayEnabled
            ? Localizer["State.EmptyDescription.Enabled"]
            : Localizer["State.EmptyDescription.Disabled"],
        DocumentViewStateKind.LoadingImage => Localizer["State.LoadingImageDescription"],
        DocumentViewStateKind.Recognizing => GetRecognitionDescription(),
        DocumentViewStateKind.NoTextFound => Localizer["State.NoTextFoundDescription"],
        DocumentViewStateKind.Error => StatusText,
        _ => string.Empty,
    };

    public string WindowTitle => string.IsNullOrWhiteSpace(CurrentImagePath)
        ? Localizer["App.Name"]
        : Localizer.Format("Window.Title.WithFile", Path.GetFileName(CurrentImagePath));

    public async Task InitializeAsync()
    {
        CurrentSettings = AppSettings.NormalizeOcrBehavior(
            await settingsManager.LoadAsync(executablePath, CancellationToken.None));
        Localizer.ApplyLanguage(CurrentSettings.UiLanguagePreference);
        BuildQuickOptionLists();
        IsSidePanelVisible = CurrentSettings.IsSidePanelVisible;
        ShowDebugBoundsOverlay = CurrentSettings.ShowDebugBoundsOverlay;
        ApplySettingsToQuickControls(CurrentSettings);
        themeService.ApplyTheme(CurrentSettings.ThemePreference);
        RefreshLocalizedText();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        logService.Info($"TextLayer startup. Version: {version}");
    }

    public async Task OpenImageFromPathAsync(string sourcePath, bool forceRecognition = false)
    {
        openImageCancellation?.Cancel();
        openImageCancellation?.Dispose();
        openImageCancellation = new CancellationTokenSource();
        var cancellationToken = openImageCancellation.Token;

        try
        {
            CurrentSelection = null;
            HoverText = string.Empty;
            CurrentState = DocumentViewStateKind.LoadingImage;
            StatusText = Localizer["Status.LoadingImage"];

            var imageData = await imageDocumentUseCase.LoadImageAsync(sourcePath, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            DisplayImage = BitmapSourceFactory.Create(imageData);
            CurrentImagePath = sourcePath;
            CurrentDocument = null;
            RecognizedText = string.Empty;
            StatusText = Localizer.Format("Status.ImageLoaded", Path.GetFileName(sourcePath));

            // Let the viewer render the image before OCR begins so the app stays responsive.
            await Task.Yield();

            if (CurrentSettings.AutoRunOcrOnOpen || forceRecognition)
            {
                await RecognizeAsyncInternal(sourcePath, cancellationToken);
            }
            else
            {
                CurrentState = DocumentViewStateKind.Ready;
                StatusText = Localizer["Status.ImageReady"];
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logService.Error("Failed to open image.", exception);
            CurrentState = DocumentViewStateKind.Error;
            StatusText = exception.Message;
        }
        finally
        {
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public async Task ApplySettingsAsync(AppSettings updatedSettings)
    {
        updatedSettings = AppSettings.NormalizeOcrBehavior(updatedSettings);
        Localizer.ApplyLanguage(updatedSettings.UiLanguagePreference);
        CurrentSettings = updatedSettings;
        BuildQuickOptionLists();
        IsSidePanelVisible = updatedSettings.IsSidePanelVisible;
        ShowDebugBoundsOverlay = updatedSettings.ShowDebugBoundsOverlay;
        ApplySettingsToQuickControls(updatedSettings);
        RefreshLocalizedText();
        OnPropertyChanged(nameof(StateDescription));

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(
            () => themeService.ApplyTheme(updatedSettings.ThemePreference),
            System.Windows.Threading.DispatcherPriority.Background);

        await PersistSettingsAsync(updatedSettings);
    }

    public AppSettings SnapshotSettingsWithWindowPlacement(WindowPlacementSettings placement)
        => CurrentSettings with
        {
            IsOverlayEnabled = IsOverlayEnabled,
            CloseToTrayOnClose = CloseToTrayOnCloseSetting,
            OcrMode = SelectedQuickOcrMode.Value,
            OcrLanguageMode = SelectedQuickOcrLanguage.Value,
            WindowPlacement = placement,
            IsSidePanelVisible = IsSidePanelVisible,
            ShowDebugBoundsOverlay = ShowDebugBoundsOverlay,
        };

    public async Task PersistWindowPlacementAsync(WindowPlacementSettings placement)
        => await ApplySettingsAsync(SnapshotSettingsWithWindowPlacement(placement));

    public void UpdateSelection(TextSelection? selection) => CurrentSelection = selection;

    public void UpdateHoverWord(string? wordText)
    {
        currentHoverWord = string.IsNullOrWhiteSpace(wordText) ? null : wordText;
        HoverText = currentHoverWord is null ? string.Empty : Localizer.Format("Hover.Word", currentHoverWord);
    }

    public void UpdateZoom(double zoomLevel)
    {
        zoom = zoomLevel;
        OnPropertyChanged(nameof(ZoomText));
    }

    public void ToggleSidePanel()
    {
        IsSidePanelVisible = !IsSidePanelVisible;
        CurrentSettings = CurrentSettings with { IsSidePanelVisible = IsSidePanelVisible };
    }

    public void RequestSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private async Task OpenImageAsync()
    {
        var selectedPath = fileDialogService.OpenImageFile();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            await OpenImageFromPathAsync(selectedPath);
        }
    }

    private async Task ReRecognizeAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentImagePath))
        {
            return;
        }

        openImageCancellation?.Cancel();
        openImageCancellation?.Dispose();
        openImageCancellation = new CancellationTokenSource();
        await RecognizeAsyncInternal(CurrentImagePath, openImageCancellation.Token);
    }

    private async Task RecognizeAsyncInternal(string sourcePath, CancellationToken cancellationToken)
    {
        CurrentState = DocumentViewStateKind.Recognizing;
        StatusText = Localizer["Status.Recognizing"];
        CurrentSelection = null;
        currentHoverWord = null;
        HoverText = string.Empty;
        await Task.Yield();

        var document = await imageDocumentUseCase.RecognizeAsync(sourcePath, CreateOcrRequest(), cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        CurrentDocument = document;
        RecognizedText = document.Words.Count == 0
            ? string.Empty
            : selectionEngine.NormalizeDocumentText(document);

        if (document.Words.Count == 0)
        {
            IsSidePanelVisible = false;
            CurrentState = DocumentViewStateKind.NoTextFound;
            StatusText = Localizer["Status.NoTextDetected"];
            return;
        }

        IsSidePanelVisible = true;
        CurrentState = DocumentViewStateKind.Ready;
        StatusText = Localizer.Format(
            "Status.RecognizedSummary",
            document.Words.Count,
            document.Lines.Count,
            GetEngineDisplayName(document.OcrEngineId));
    }

    private async Task CopyAllTextAsync()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var textToCopy = string.IsNullOrWhiteSpace(RecognizedText)
            ? selectionEngine.NormalizeDocumentText(CurrentDocument)
            : RecognizedText;

        await clipboardService.CopyTextAsync(textToCopy, CancellationToken.None);
        StatusText = Localizer["Status.CopiedAll"];
    }

    private async Task CopySelectionAsync()
    {
        if (CurrentSelection is null || CurrentSelection.IsEmpty)
        {
            return;
        }

        await clipboardService.CopyTextAsync(CurrentSelection.SelectedText, CancellationToken.None);
        StatusText = Localizer.Format("Status.CopiedSelection", CurrentSelection.SelectedWordCount);
    }

    private void ClearImage()
    {
        openImageCancellation?.Cancel();
        CurrentImagePath = null;
        DisplayImage = null;
        CurrentDocument = null;
        CurrentSelection = null;
        RecognizedText = string.Empty;
        currentHoverWord = null;
        HoverText = string.Empty;
        StatusText = Localizer["Status.ImageCleared"];
        CurrentState = DocumentViewStateKind.Empty;
    }

    private void NotifyCommandStates()
    {
        ((AsyncRelayCommand)ReRecognizeCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)CopyAllTextCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)CopySelectionCommand).NotifyCanExecuteChanged();
        ((RelayCommand)ClearImageCommand).NotifyCanExecuteChanged();
    }

    private OcrRequestOptions CreateOcrRequest()
        => new(CurrentSettings.OcrMode, CurrentSettings.OcrLanguageMode);

    private string GetRecognitionDescription()
        => CurrentSettings.OcrMode switch
        {
            OcrMode.Fast => Localizer["State.RecognizingDescription.Fast"],
            OcrMode.Accurate => Localizer["State.RecognizingDescription.Accurate"],
            _ => Localizer["State.RecognizingDescription.Auto"],
        };

    private void BuildQuickOptionLists()
        => BuildQuickOptionLists(CurrentSettings.OcrLanguageMode);

    private void BuildQuickOptionLists(OcrLanguageMode languageMode)
    {
        QuickOcrModeOptions = CreateOcrModeOptions(languageMode);
        QuickOcrLanguageOptions =
        [
            new OcrLanguageOption("OcrLanguage.English", OcrLanguageMode.English),
            new OcrLanguageOption("OcrLanguage.Russian", OcrLanguageMode.Russian),
        ];

        OnPropertyChanged(nameof(QuickOcrModeOptions));
        OnPropertyChanged(nameof(QuickOcrLanguageOptions));
    }

    private void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(OverlayStatusText));
        OnPropertyChanged(nameof(OcrRecommendationText));
        OnPropertyChanged(nameof(StateTitle));
        OnPropertyChanged(nameof(StateDescription));
        OnPropertyChanged(nameof(WindowTitle));

        if (CurrentState == DocumentViewStateKind.Empty && !HasImage)
        {
            StatusText = Localizer["Status.Ready"];
        }
        else if (CurrentState == DocumentViewStateKind.LoadingImage)
        {
            StatusText = Localizer["Status.LoadingImage"];
        }
        else if (CurrentState == DocumentViewStateKind.Recognizing)
        {
            StatusText = Localizer["Status.Recognizing"];
        }
        else if (CurrentState == DocumentViewStateKind.NoTextFound)
        {
            StatusText = Localizer["Status.NoTextDetected"];
        }
        else if (CurrentState == DocumentViewStateKind.Ready && CurrentDocument is not null && CurrentDocument.Words.Count > 0)
        {
            StatusText = Localizer.Format(
                "Status.RecognizedSummary",
                CurrentDocument.Words.Count,
                CurrentDocument.Lines.Count,
                GetEngineDisplayName(CurrentDocument.OcrEngineId));
        }
        else if (CurrentState == DocumentViewStateKind.Ready && HasImage)
        {
            StatusText = Localizer["Status.ImageReady"];
        }

        HoverText = string.IsNullOrWhiteSpace(currentHoverWord)
            ? string.Empty
            : Localizer.Format("Hover.Word", currentHoverWord);
    }

    private void ApplySettingsToQuickControls(AppSettings settings)
    {
        settings = AppSettings.NormalizeOcrBehavior(settings);
        suppressQuickSettingsPersistence = true;
        try
        {
            isOverlayEnabled = settings.IsOverlayEnabled;
            closeToTrayOnClose = settings.CloseToTrayOnClose;
            var visibleLanguageMode = AppSettings.NormalizeVisibleOcrLanguageMode(settings.OcrLanguageMode);
            BuildQuickOptionLists(visibleLanguageMode);
            selectedQuickOcrMode = QuickOcrModeOptions.First(option => option.Value == AppSettings.NormalizeOcrModeForLanguage(settings.OcrMode, visibleLanguageMode));
            selectedQuickOcrLanguage = QuickOcrLanguageOptions.First(option => option.Value == visibleLanguageMode);
        }
        finally
        {
            suppressQuickSettingsPersistence = false;
        }

        OnPropertyChanged(nameof(IsOverlayEnabled));
        OnPropertyChanged(nameof(CloseToTrayOnCloseSetting));
        OnPropertyChanged(nameof(SelectedQuickOcrMode));
        OnPropertyChanged(nameof(SelectedQuickOcrLanguage));
        OnPropertyChanged(nameof(OcrRecommendationText));
        OnPropertyChanged(nameof(OverlayStatusText));
        OnPropertyChanged(nameof(StateDescription));
    }

    private async Task PersistQuickSettingsAsync()
    {
        if (suppressQuickSettingsPersistence)
        {
            return;
        }

        try
        {
            await PersistSettingsAsync(CurrentSettings);
        }
        catch (Exception exception)
        {
            logService.Error("Failed to save quick control center settings.", exception);
            StatusText = exception.Message;
        }
    }

    private async Task PersistSettingsAsync(AppSettings settings)
    {
        await settingsSaveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await settingsManager.SaveAsync(settings, executablePath, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            settingsSaveGate.Release();
        }
    }

    private static string GetEngineDisplayName(string engineId)
        => engineId switch
        {
            "tesseract" => Localizer["OcrMode.Accurate"],
            "windows-media-ocr" => Localizer["OcrMode.Fast"],
            _ => engineId,
        };

    private static string GetOcrRecommendationText(OcrLanguageMode languageMode)
        => languageMode switch
        {
            OcrLanguageMode.Russian => Localizer["ControlCenter.OcrRecommendation.Russian"],
            _ => Localizer["ControlCenter.OcrRecommendation.English"],
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
