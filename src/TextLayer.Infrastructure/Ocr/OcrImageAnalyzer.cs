using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TextLayer.Infrastructure.Ocr;

public sealed class OcrImageAnalyzer
{
    public async Task<OcrImageAnalysis> AnalyzeAsync(string sourcePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException("The selected image could not be found.");
        }

        var file = await StorageFile.GetFileFromPathAsync(sourcePath).AsTask(cancellationToken).ConfigureAwait(false);
        using IRandomAccessStream stream = await file.OpenReadAsync().AsTask(cancellationToken).ConfigureAwait(false);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
        var orientedWidth = (int)decoder.OrientedPixelWidth;
        var orientedHeight = (int)decoder.OrientedPixelHeight;
        var analysisScale = GetAnalysisScale(orientedWidth, orientedHeight);
        var scaledWidth = Math.Max(1, (int)Math.Round(orientedWidth * analysisScale));
        var scaledHeight = Math.Max(1, (int)Math.Round(orientedHeight * analysisScale));

        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform
                {
                    ScaledWidth = (uint)scaledWidth,
                    ScaledHeight = (uint)scaledHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant,
                },
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);

        var pixels = new byte[softwareBitmap.PixelWidth * softwareBitmap.PixelHeight * 4];
        softwareBitmap.CopyToBuffer(pixels.AsBuffer());

        var minLuminance = 255;
        var maxLuminance = 0;
        double luminanceSum = 0d;
        double edgeMagnitudeSum = 0d;
        var sampleCount = 0;

        for (var y = 0; y < softwareBitmap.PixelHeight; y++)
        {
            for (var x = 0; x < softwareBitmap.PixelWidth; x++)
            {
                var index = ((y * softwareBitmap.PixelWidth) + x) * 4;
                var luminance = GetLuminance(pixels[index + 2], pixels[index + 1], pixels[index]);
                minLuminance = Math.Min(minLuminance, luminance);
                maxLuminance = Math.Max(maxLuminance, luminance);
                luminanceSum += luminance;
                sampleCount++;

                if (x > 0)
                {
                    var leftIndex = index - 4;
                    var left = GetLuminance(pixels[leftIndex + 2], pixels[leftIndex + 1], pixels[leftIndex]);
                    edgeMagnitudeSum += Math.Abs(luminance - left);
                }

                if (y > 0)
                {
                    var topIndex = index - (softwareBitmap.PixelWidth * 4);
                    var top = GetLuminance(pixels[topIndex + 2], pixels[topIndex + 1], pixels[topIndex]);
                    edgeMagnitudeSum += Math.Abs(luminance - top);
                }
            }
        }

        var averageLuminance = sampleCount == 0 ? 255d : luminanceSum / sampleCount;
        var contrastRange = maxLuminance - minLuminance;
        var normalizedEdgeDensity = sampleCount == 0
            ? 0d
            : edgeMagnitudeSum / (sampleCount * 255d);
        var isDarkBackground = averageLuminance < 118d;
        var isLowContrast = contrastRange < 120 || (contrastRange < 150 && averageLuminance < 150d);
        var likelySmallText = Math.Max(orientedWidth, orientedHeight) >= 1400 && normalizedEdgeDensity > 0.09d;
        var likelyChatScreenshot = isDarkBackground
            && orientedWidth >= 900
            && orientedHeight >= 500
            && normalizedEdgeDensity > 0.07d;

        return new OcrImageAnalysis(
            PixelWidth: orientedWidth,
            PixelHeight: orientedHeight,
            AverageLuminance: averageLuminance,
            ContrastRange: contrastRange,
            EdgeDensity: normalizedEdgeDensity,
            IsDarkBackground: isDarkBackground,
            IsLowContrast: isLowContrast,
            LikelySmallText: likelySmallText,
            LikelyChatScreenshot: likelyChatScreenshot);
    }

    private static double GetAnalysisScale(int width, int height)
    {
        const double maxAnalysisDimension = 512d;
        if (width <= maxAnalysisDimension && height <= maxAnalysisDimension)
        {
            return 1d;
        }

        return Math.Min(maxAnalysisDimension / width, maxAnalysisDimension / height);
    }

    private static int GetLuminance(byte red, byte green, byte blue)
        => (int)Math.Round((red * 0.299d) + (green * 0.587d) + (blue * 0.114d));
}
