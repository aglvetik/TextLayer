namespace TextLayer.Application.Abstractions;

public interface IClipboardService
{
    Task CopyTextAsync(string text, CancellationToken cancellationToken);
}
