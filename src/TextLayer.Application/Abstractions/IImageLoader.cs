using TextLayer.Application.Models;

namespace TextLayer.Application.Abstractions;

public interface IImageLoader
{
    Task<LoadedImageData> LoadAsync(string sourcePath, CancellationToken cancellationToken);
}
