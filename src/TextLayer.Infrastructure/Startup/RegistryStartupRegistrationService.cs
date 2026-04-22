using Microsoft.Win32;
using TextLayer.Application.Abstractions;

namespace TextLayer.Infrastructure.Startup;

public sealed class RegistryStartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TextLayer";
    public const string StartupArgument = "--startup";

    public async Task<bool> IsEnabledAsync(string executablePath, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                var value = key?.GetValue(ValueName) as string;
                var expectedCommand = CreateCommand(executablePath);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                if (string.Equals(value, expectedCommand, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // If an older TextLayer Run value exists, keep the user's enabled intent but repair
                // the command so Windows login starts the current published app silently.
                key?.SetValue(ValueName, expectedCommand);
                return true;
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

    private static string CreateCommand(string executablePath)
        => $"\"{ResolveStartupExecutablePath(executablePath)}\" {StartupArgument}";

    private static string ResolveStartupExecutablePath(string executablePath)
    {
        var fullPath = Path.GetFullPath(executablePath);
        var canonicalPublishedPath = TryFindCanonicalPublishedExecutable(fullPath);

        return IsDevelopmentBuildPath(fullPath) && canonicalPublishedPath is not null
            ? canonicalPublishedPath
            : fullPath;
    }

    private static string? TryFindCanonicalPublishedExecutable(string executablePath)
    {
        var directory = Directory.GetParent(executablePath);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "TextLayer.sln");
            if (File.Exists(solutionPath))
            {
                var canonicalPath = Path.Combine(directory.FullName, "dist", "TextLayer", "TextLayer.exe");
                return File.Exists(canonicalPath)
                    ? Path.GetFullPath(canonicalPath)
                    : null;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static bool IsDevelopmentBuildPath(string executablePath)
    {
        var normalizedPath = executablePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return normalizedPath.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            && normalizedPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }
}
