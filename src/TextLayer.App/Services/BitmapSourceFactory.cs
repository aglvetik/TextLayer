using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextLayer.Application.Models;

namespace TextLayer.App.Services;

public static class BitmapSourceFactory
{
    public static BitmapSource Create(LoadedImageData imageData)
    {
        // The OCR model and viewer math use original image pixels as the shared coordinate space.
        // Displaying the bitmap at 96 DPI keeps WPF's natural image size aligned to pixel dimensions.
        var bitmap = BitmapSource.Create(
            imageData.PixelWidth,
            imageData.PixelHeight,
            96d,
            96d,
            PixelFormats.Pbgra32,
            palette: null,
            imageData.PixelData,
            imageData.Stride);

        bitmap.Freeze();
        return bitmap;
    }
}
