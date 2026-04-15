namespace TextLayer.Application.Abstractions;

public interface IStartupRegistrationService
{
    Task<bool> IsEnabledAsync(string executablePath, CancellationToken cancellationToken);

    Task SetEnabledAsync(string executablePath, bool enabled, CancellationToken cancellationToken);
}
