using System.IO;
using System.Drawing;
using System.Windows.Input;
using TextLayer.App.Services;
using TextLayer.App.ViewModels;
using TextLayer.Application.UseCases;
using TextLayer.Domain.Services;
using TextLayer.Infrastructure.Clipboard;
using TextLayer.Infrastructure.Imaging;
using TextLayer.Infrastructure.Logging;
using TextLayer.Infrastructure.Ocr;
using TextLayer.Infrastructure.Settings;
using TextLayer.Infrastructure.Startup;

namespace TextLayer.App;

public partial class App : System.Windows.Application
{
    private MainWindow? mainWindow;
    private TrayIconService? trayIconService;
    private GlobalHotkeyService? globalHotkeyService;
    private OverlayWorkflowCoordinator? overlayWorkflowCoordinator;
    private bool isShuttingDown;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        var logService = new FileLogService();
        var themeService = new ThemeService();
        var fileDialogService = new OpenImageFileDialogService();
        var imageLoader = new WicImageLoader();
        var imageAnalyzer = new OcrImageAnalyzer();
        var ocrSelector = new OcrEngineSelector();
        var fastOcrEngine = new WindowsMediaOcrEngine();
        var accurateOcrEngine = new TesseractOcrEngine(new TesseractDataPathResolver());
        var ocrEngine = new CompositeOcrEngine(fastOcrEngine, accurateOcrEngine, imageAnalyzer, ocrSelector, logService);
        var clipboardService = new WpfClipboardService();
        var selectionEngine = new SelectionEngine(new TextNormalizer());
        var settingsStore = new JsonSettingsStore(logService);
        var startupRegistrationService = new RegistryStartupRegistrationService();
        var settingsManager = new SettingsManager(settingsStore, startupRegistrationService, logService);
        var imageDocumentUseCase = new ImageDocumentUseCase(imageLoader, ocrEngine, logService);
        var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("The application executable path is unavailable.");
        var screenGeometryService = new ScreenGeometryService();
        var regionSelectionService = new RegionSelectionService(screenGeometryService);
        var screenCaptureService = new ScreenCaptureService();
        var activeWindowCaptureService = new ActiveWindowCaptureService(screenGeometryService);

        var mainWindowViewModel = new MainWindowViewModel(
            imageDocumentUseCase,
            settingsManager,
            clipboardService,
            selectionEngine,
            logService,
            fileDialogService,
            themeService,
            executablePath);

        mainWindow = new MainWindow(mainWindowViewModel);
        MainWindow = mainWindow;
        await mainWindow.InitializeAsync();
        mainWindow.Opacity = 0d;
        mainWindow.Show();
        mainWindow.Hide();
        mainWindow.Opacity = 1d;

        var overlayWindowManager = new OverlayWindowManager(clipboardService);
        overlayWorkflowCoordinator = new OverlayWorkflowCoordinator(
            regionSelectionService,
            activeWindowCaptureService,
            screenCaptureService,
            overlayWindowManager,
            imageDocumentUseCase,
            () => mainWindowViewModel.CurrentSettings,
            (title, text, icon) => trayIconService?.ShowNotification(title, text, icon),
            logService);

        void StartRegionCapture()
        {
            if (!mainWindowViewModel.CurrentSettings.IsOverlayEnabled)
            {
                trayIconService?.ShowNotification(
                    UiTextService.Instance["Notification.AppTitle"],
                    UiTextService.Instance["Notification.OverlayPaused"]);
                mainWindow.Dispatcher.Invoke(mainWindow.RestoreFromTray);
                return;
            }

            overlayWorkflowCoordinator.StartRegionCapture();
        }

        void StartActiveWindowCapture()
        {
            if (!mainWindowViewModel.CurrentSettings.IsOverlayEnabled)
            {
                trayIconService?.ShowNotification(
                    UiTextService.Instance["Notification.AppTitle"],
                    UiTextService.Instance["Notification.OverlayPaused"]);
                mainWindow.Dispatcher.Invoke(mainWindow.RestoreFromTray);
                return;
            }

            overlayWorkflowCoordinator.StartActiveWindowCapture();
        }

        var trayIcon = Icon.ExtractAssociatedIcon(executablePath) ?? SystemIcons.Application;
        trayIconService = new TrayIconService(
            trayIcon,
            captureRegionAction: StartRegionCapture,
            captureActiveWindowAction: StartActiveWindowCapture,
            openAction: () => mainWindow.Dispatcher.Invoke(mainWindow.RestoreFromTray),
            openImageAction: () => mainWindow.Dispatcher.Invoke(mainWindow.OpenImageUsingDialog),
            settingsAction: () => mainWindow.Dispatcher.Invoke(mainWindow.ShowSettingsDialog),
            aboutAction: () => mainWindow.Dispatcher.Invoke(mainWindow.ShowAboutDialog),
            exitAction: () => _ = mainWindow.Dispatcher.BeginInvoke(new Action(() => _ = RequestFullShutdownAsync())));

        globalHotkeyService = new GlobalHotkeyService();
        globalHotkeyService.Attach(mainWindow);
        globalHotkeyService.Register(ModifierKeys.Control | ModifierKeys.Shift, Key.O, StartRegionCapture);
        logService.Info("Global OCR hotkey registered: Ctrl+Shift+O.");

        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            mainWindow.RestoreFromTray();
            await mainWindowViewModel.OpenImageFromPathAsync(e.Args[0]);
            return;
        }

        trayIconService.ShowNotification(
            UiTextService.Instance["Notification.AppTitle"],
            UiTextService.Instance["Notification.Startup"]);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        overlayWorkflowCoordinator?.CloseOverlay();
        overlayWorkflowCoordinator = null;
        globalHotkeyService?.Dispose();
        globalHotkeyService = null;
        trayIconService?.Dispose();
        trayIconService = null;
        base.OnExit(e);
    }

    public Task RequestFullShutdownAsync()
    {
        if (Dispatcher.CheckAccess())
        {
            return RequestFullShutdownCoreAsync();
        }

        return Dispatcher.InvokeAsync(RequestFullShutdownCoreAsync).Task.Unwrap();
    }

    private async Task RequestFullShutdownCoreAsync()
    {
        if (isShuttingDown)
        {
            return;
        }

        isShuttingDown = true;
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        try
        {
            overlayWorkflowCoordinator?.CloseOverlay();
            globalHotkeyService?.Dispose();
            globalHotkeyService = null;

            if (mainWindow is not null)
            {
                await mainWindow.PersistStateAsync();
            }

            trayIconService?.Dispose();
            trayIconService = null;

            foreach (var window in Windows.OfType<System.Windows.Window>().ToArray())
            {
                if (window is MainWindow shell)
                {
                    shell.PrepareForExit();
                }

                window.Close();
            }
        }
        finally
        {
            Shutdown();
        }
    }
}
