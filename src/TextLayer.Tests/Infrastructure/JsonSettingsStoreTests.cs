using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;
using TextLayer.Infrastructure.Settings;

namespace TextLayer.Tests.Infrastructure;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsSettings()
    {
        var originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        var tempLocalAppData = Path.Combine(Path.GetTempPath(), "TextLayerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempLocalAppData);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", tempLocalAppData);

        try
        {
            var store = new JsonSettingsStore(new TestLogService());
            var expected = new AppSettings
            {
                IsOverlayEnabled = false,
                LaunchAtStartup = true,
                CloseToTrayOnClose = true,
                AutoRunOcrOnOpen = false,
                OcrMode = OcrMode.Accurate,
                OcrLanguageMode = OcrLanguageMode.Russian,
                UiLanguagePreference = UiLanguagePreference.Russian,
                ThemePreference = ThemePreference.Dark,
                ShowDebugBoundsOverlay = true,
                IsSidePanelVisible = false,
                CloseOverlayAfterCopy = false,
                WindowPlacement = new WindowPlacementSettings(10, 20, 1200, 800, true),
            };

            await store.SaveAsync(expected, CancellationToken.None);
            var loaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(expected, loaded);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", originalLocalAppData);
            if (Directory.Exists(tempLocalAppData))
            {
                Directory.Delete(tempLocalAppData, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_UsesTrayFriendlyDefaults_WhenSettingsFileDoesNotExist()
    {
        var originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        var tempLocalAppData = Path.Combine(Path.GetTempPath(), "TextLayerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempLocalAppData);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", tempLocalAppData);

        try
        {
            var store = new JsonSettingsStore(new TestLogService());

            var loaded = await store.LoadAsync(CancellationToken.None);

            Assert.True(loaded.CloseToTrayOnClose);
            Assert.True(loaded.IsOverlayEnabled);
            Assert.Equal(OcrLanguageMode.English, loaded.OcrLanguageMode);
            Assert.Equal(UiLanguagePreference.English, loaded.UiLanguagePreference);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", originalLocalAppData);
            if (Directory.Exists(tempLocalAppData))
            {
                Directory.Delete(tempLocalAppData, recursive: true);
            }
        }
    }

    private sealed class TestLogService : ILogService
    {
        public void Error(string message, Exception? exception = null)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }
    }
}
