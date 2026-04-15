using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;

namespace TextLayer.Application.UseCases;

public sealed class SettingsManager(
    ISettingsStore settingsStore,
    IStartupRegistrationService startupRegistrationService,
    ILogService logService)
{
    public async Task<AppSettings> LoadAsync(string executablePath, CancellationToken cancellationToken)
    {
        var settings = AppSettings.NormalizeOcrBehavior(
            await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false));
        try
        {
            var startupEnabled = await startupRegistrationService.IsEnabledAsync(executablePath, cancellationToken).ConfigureAwait(false);
            if (startupEnabled != settings.LaunchAtStartup)
            {
                settings = settings with { LaunchAtStartup = startupEnabled };
            }
        }
        catch (Exception exception)
        {
            logService.Error("Failed to check startup registration state.", exception);
        }

        return settings;
    }

    public async Task SaveAsync(AppSettings settings, string executablePath, CancellationToken cancellationToken)
    {
        settings = AppSettings.NormalizeOcrBehavior(settings);
        await settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        await startupRegistrationService.SetEnabledAsync(executablePath, settings.LaunchAtStartup, cancellationToken).ConfigureAwait(false);
    }
}
