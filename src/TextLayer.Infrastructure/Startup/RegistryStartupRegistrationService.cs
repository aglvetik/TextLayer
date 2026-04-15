using Microsoft.Win32;
using TextLayer.Application.Abstractions;

namespace TextLayer.Infrastructure.Startup;

public sealed class RegistryStartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TextLayer";

    public async Task<bool> IsEnabledAsync(string executablePath, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var value = key?.GetValue(ValueName) as string;
                var expectedCommand = CreateCommand(executablePath);
                return string.Equals(value, expectedCommand, StringComparison.OrdinalIgnoreCase);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("TextLayer could not read the Windows startup setting.", exception);
        }
    }

    public async Task SetEnabledAsync(string executablePath, bool enabled, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                    ?? throw new InvalidOperationException("The Windows startup registry key is not available.");

                if (enabled)
                {
                    key.SetValue(ValueName, CreateCommand(executablePath));
                }
                else
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("TextLayer could not update the Windows startup setting.", exception);
        }
    }

    private static string CreateCommand(string executablePath) => $"\"{executablePath}\"";
}
