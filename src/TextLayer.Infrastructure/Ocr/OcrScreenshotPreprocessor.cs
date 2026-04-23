using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;

namespace TextLayer.Infrastructure.Ocr;

internal static class OcrScreenshotPreprocessor
{
    public static SoftwareBitmap CreateLineDetectionBitmap(SoftwareBitmap source, bool invertForDarkUi)
        => CreateAccentAwareGrayscaleBitmap(source, invertForDarkUi);

    public static SoftwareBitmap CreateAccentAwareGrayscaleBitmap(SoftwareBitmap source, bool invertForDarkUi)
    {
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyToBuffer(pixels.AsBuffer());

        var minLuminance = 255;
        var maxLuminance = 0;

        for (var pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex += 4)
        {
            var luminance = GetAccentAwareLuminance(
                pixels[pixelIndex + 2],
                pixels[pixelIndex + 1],
                pixels[pixelIndex],
                invertForDarkUi);

            if (invertForDarkUi)
            {
                luminance = 255 - luminance;
            }

            minLuminance = Math.Min(minLuminance, luminance);
            maxLuminance = Math.Max(maxLuminance, luminance);
        }

        var range = Math.Max(32, maxLuminance - minLuminance);
        for (var pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex += 4)
        {
            var luminance = GetAccentAwareLuminance(
                pixels[pixelIndex + 2],
                pixels[pixelIndex + 1],
                pixels[pixelIndex],
                invertForDarkUi);

            if (invertForDarkUi)
            {
                luminance = 255 - luminance;
            }

            var normalized = ((luminance - minLuminance) * 255) / range;
            var contrasted = StretchContrast(normalized);
            var grayscale = (byte)Math.Clamp(contrasted, 0, 255);

            pixels[pixelIndex] = grayscale;
            pixels[pixelIndex + 1] = grayscale;
            pixels[pixelIndex + 2] = grayscale;
            pixels[pixelIndex + 3] = 255;
        }

        return SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            source.PixelWidth,
            source.PixelHeight,
            BitmapAlphaMode.Premultiplied);
    }

    private static int GetAccentAwareLuminance(byte red, byte green, byte blue, bool preferForegroundBoost)
    {
        var baseLuminance = (int)Math.Round((red * 0.299d) + (green * 0.587d) + (blue * 0.114d));
        var maxChannel = Math.Max(red, Math.Max(green, blue));
        var minChannel = Math.Min(red, Math.Min(green, blue));
        var spread = maxChannel - minChannel;

        var boosted = baseLuminance;
        if (spread >= 20 && maxChannel >= 96)
        {
            var vividBoost = (maxChannel * (preferForegroundBoost ? 0.9d : 0.78d))
                + (spread * (preferForegroundBoost ? 0.38d : 0.24d));
            boosted = Math.Max(boosted, (int)Math.Round(vividBoost));
        }

        if (preferForegroundBoost && maxChannel >= 140)
        {
            boosted = Math.Max(boosted, (int)Math.Round((maxChannel * 0.94d) + (spread * 0.22d)));
        }

        return Math.Clamp(boosted, 0, 255);
    }

    private static int StretchContrast(int luminance)
    {
        if (luminance <= 16)
        {
            return 0;
        }

        if (luminance >= 239)
        {
            return 255;
        }

        return (int)Math.Round((luminance - 16) * (255d / 223d));
    }
}
