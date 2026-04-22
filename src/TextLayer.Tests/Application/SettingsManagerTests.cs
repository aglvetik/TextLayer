using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;
using TextLayer.Application.UseCases;

namespace TextLayer.Tests.Application;

public sealed class SettingsManagerTests
{
    [Fact]
    public async Task LoadAsync_NormalizesRussianFastToAccurate()
    {
        var store = new FakeSettingsStore(new AppSettings
        {
            OcrLanguageMode = OcrLanguageMode.Russian,
            OcrMode = OcrMode.Fast,
        });
        var manager = new SettingsManager(store, new FakeStartupRegistrationService(), new FakeLogService());

        var settings = await manager.LoadAsync("TextLayer.exe", CancellationToken.None);

        Assert.Equal(OcrLanguageMode.Russian, settings.OcrLanguageMode);
        Assert.Equal(OcrMode.Accurate, settings.OcrMode);
    }

    [Fact]
    public async Task SaveAsync_NormalizesRussianFastToAccurateBeforePersisting()
    {
        var store = new FakeSettingsStore();
        var manager = new SettingsManager(store, new FakeStartupRegistrationService(), new FakeLogService());

        await manager.SaveAsync(new AppSettings
        {
            OcrLanguageMode = OcrLanguageMode.Russian,
            OcrMode = OcrMode.Fast,
        }, "TextLayer.exe", CancellationToken.None);

        Assert.NotNull(store.SavedSettings);
        Assert.Equal(OcrLanguageMode.Russian, store.SavedSettings!.OcrLanguageMode);
        Assert.Equal(OcrMode.Accurate, store.SavedSettings!.OcrMode);
    }

    [Fact]
    public async Task SaveAsync_PreservesEnglishFast()
    {
        var store = new FakeSettingsStore();
        var manager = new SettingsManager(store, new FakeStartupRegistrationService(), new FakeLogService());

        await manager.SaveAsync(new AppSettings
        {
            OcrLanguageMode = OcrLanguageMode.English,
            OcrMode = OcrMode.Fast,
        }, "TextLayer.exe", CancellationToken.None);

        Assert.NotNull(store.SavedSettings);
        Assert.Equal(OcrLanguageMode.English, store.SavedSettings!.OcrLanguageMode);
        Assert.Equal(OcrMode.Fast, store.SavedSettings!.OcrMode);
    }

    [Fact]
    public async Task SaveAsync_UpdatesStartupRegistrationBeforePersistingSettings()
    {
        var store = new FakeSettingsStore();
        var startupRegistration = new FakeStartupRegistrationService(store);
        var manager = new SettingsManager(store, startupRegistration, new FakeLogService());

        await manager.SaveAsync(new AppSettings
        {
            LaunchAtStartup = true,
        }, "TextLayer.exe", CancellationToken.None);

        Assert.True(startupRegistration.RegistrationWasUpdatedBeforeSettingsSave);
        Assert.True(startupRegistration.Enabled);
        Assert.True(store.SavedSettings?.LaunchAtStartup);
    }

    private sealed class FakeSettingsStore(AppSettings? initialSettings = null) : ISettingsStore
    {
        public AppSettings CurrentSettings { get; private set; } = initialSettings ?? new AppSettings();

        public AppSettings? SavedSettings { get; private set; }

        public bool WasSaved { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
            => Task.FromResult(CurrentSettings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            SavedSettings = settings;
            CurrentSettings = settings;
            WasSaved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStartupRegistrationService(FakeSettingsStore? settingsStore = null) : IStartupRegistrationService
    {
        public bool Enabled { get; private set; }

        public bool RegistrationWasUpdatedBeforeSettingsSave { get; private set; }

        public Task<bool> IsEnabledAsync(string executablePath, CancellationToken cancellationToken)
            => Task.FromResult(Enabled);

        public Task SetEnabledAsync(string executablePath, bool enabled, CancellationToken cancellationToken)
        {
            Enabled = enabled;
            RegistrationWasUpdatedBeforeSettingsSave = settingsStore is null || !settingsStore.WasSaved;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogService : ILogService
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
