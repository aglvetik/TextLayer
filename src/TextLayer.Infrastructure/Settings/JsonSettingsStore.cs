using System.Text.Json;
using System.Text.Json.Serialization;
using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;

namespace TextLayer.Infrastructure.Settings;

public sealed class JsonSettingsStore(ILogService logService) : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.BaseDirectory);
            if (!File.Exists(AppDataPaths.SettingsFilePath))
            {
                return new AppSettings();
            }

            await using var stream = new FileStream(
                AppDataPaths.SettingsFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            return settings ?? new AppSettings();
        }
        catch (Exception exception)
        {
            logService.Error("Failed to load settings. Falling back to defaults.", exception);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.BaseDirectory);
            await using var stream = new FileStream(
                AppDataPaths.SettingsFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logService.Error("Failed to save settings.", exception);
            throw new InvalidOperationException("TextLayer could not save your settings. Please try again.", exception);
        }
    }
}
