using System.Runtime.InteropServices.WindowsRuntime;
using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TextLayer.Infrastructure.Imaging;

public sealed class WicImageLoader : IImageLoader
{
    private static readonly HashSet<string> SupportedExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp",
    ];

    public async Task<LoadedImageData> LoadAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(sourcePath);
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This image format is not supported. Please open a PNG, JPG, JPEG, BMP, or WebP image.");
        }

        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException("The selected image could not be found.");
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(sourcePath).AsTask(cancellationToken).ConfigureAwait(false);
            using IRandomAccessStream stream = await file.OpenReadAsync().AsTask(cancellationToken).ConfigureAwait(false);
            var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
            var orientedWidth = (int)decoder.OrientedPixelWidth;
            var orientedHeight = (int)decoder.OrientedPixelHeight;
            var pixelProvider = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform(),
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            return new LoadedImageData(
                SourcePath: sourcePath,
                PixelWidth: orientedWidth,
                PixelHeight: orientedHeight,
                DpiX: decoder.DpiX,
                DpiY: decoder.DpiY,
                Stride: orientedWidth * 4,
                PixelData: pixelProvider.DetachPixelData(),
                OrientationNormalized: true,
                FileExtension: extension);
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "TextLayer could not load this image. The file may be corrupted, locked, or not supported on this system.",
                exception);
        }
    }
}
