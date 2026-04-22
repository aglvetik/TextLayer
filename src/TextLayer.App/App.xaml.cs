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
    private const string SingleInstanceMutexName = @"Local\TextLayer.App.SingleInstance";
    private const string ActivationEventName = @"Local\TextLayer.App.Activate";
    private Mutex? singleInstanceMutex;
    private EventWaitHandle? activationEvent;
    private CancellationTokenSource? activationSignalCancellation;
    private Task? activationSignalTask;
    private bool ownsSingleInstanceMutex;
    private MainWindow? mainWindow;
    private TrayIconService? trayIconService;
    private GlobalHotkeyService? globalHotkeyService;
    private OverlayWorkflowCoordinator? overlayWorkflowCoordinator;
    private bool isShuttingDown;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        var launchOptions = LaunchOptions.Parse(e.Args);
        activationEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ActivationEventName);
        singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            if (!launchOptions.IsWindowsStartup)
            {
                SignalExistingInstance();
            }

            Shutdown();
            return;
        }

        ownsSingleInstanceMutex = true;
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
        StartActivationSignalListener();

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

        if (launchOptions.ImagePath is not null)
        {
            mainWindow.RestoreFromTray();
            await mainWindowViewModel.OpenImageFromPathAsync(launchOptions.ImagePath);
            return;
        }

        if (launchOptions.IsWindowsStartup)
        {
            logService.Info("TextLayer launched from Windows startup in background mode.");
        }
        else
        {
            mainWindow.RestoreFromTray();
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        StopActivationSignalListener();
        overlayWorkflowCoordinator?.CloseOverlay();
        overlayWorkflowCoordinator = null;
        globalHotkeyService?.Dispose();
        globalHotkeyService = null;
        trayIconService?.Dispose();
        trayIconService = null;
        if (ownsSingleInstanceMutex)
        {
            singleInstanceMutex?.ReleaseMutex();
            ownsSingleInstanceMutex = false;
        }

        singleInstanceMutex?.Dispose();
        singleInstanceMutex = null;
        base.OnExit(e);
    }

    private void SignalExistingInstance()
    {
        try
        {
            activationEvent?.Set();
        }
        catch
        {
            // If the first instance is already exiting, there is nothing useful to activate.
        }
    }

    private void StartActivationSignalListener()
    {
        if (activationEvent is null)
        {
            return;
        }

        activationSignalCancellation = new CancellationTokenSource();
        var token = activationSignalCancellation.Token;
        var signal = activationEvent;
        activationSignalTask = Task.Run(() =>
        {
            var handles = new WaitHandle[] { signal, token.WaitHandle };
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var index = WaitHandle.WaitAny(handles);
                    if (index == 0 && !token.IsCancellationRequested)
                    {
                        _ = Dispatcher.BeginInvoke(new Action(ShowMainWindowFromExternalLaunch));
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }, token);
    }

    private void StopActivationSignalListener()
    {
        activationSignalCancellation?.Cancel();

        try
        {
            activationSignalTask?.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch
        {
            // Shutdown should continue even if the activation listener is already unwinding.
        }

        activationSignalTask = null;
        activationSignalCancellation?.Dispose();
        activationSignalCancellation = null;
        activationEvent?.Dispose();
        activationEvent = null;
    }

    private void ShowMainWindowFromExternalLaunch()
    {
        if (isShuttingDown || Dispatcher.HasShutdownStarted)
        {
            return;
        }

        mainWindow?.RestoreFromTray();
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

    private sealed record LaunchOptions(bool IsWindowsStartup, string? ImagePath)
    {
        public static LaunchOptions Parse(IReadOnlyList<string> args)
        {
            var isWindowsStartup = false;
            string? imagePath = null;

            foreach (var arg in args)
            {
                if (string.Equals(arg, RegistryStartupRegistrationService.StartupArgument, StringComparison.OrdinalIgnoreCase))
                {
                    isWindowsStartup = true;
                    continue;
                }

                if (imagePath is null && File.Exists(arg))
                {
                    imagePath = arg;
                }
            }

            return new LaunchOptions(isWindowsStartup, imagePath);
        }
    }
}
