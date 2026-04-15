using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using TextLayer.App.Models;
using TextLayer.Domain.Geometry;

namespace TextLayer.App.Services;

public sealed class ScreenCaptureService
{
    public async Task<ScreenCaptureSnapshot> CaptureRegionAsync(ScreenSelectionResult selection, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshotPath = await Task.Run(
            () => CaptureRegionToFile(selection.PixelBounds, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return new ScreenCaptureSnapshot(snapshotPath, selection);
    }

    private static string CaptureRegionToFile(PixelRect pixelBounds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (pixelBounds.IsEmpty)
        {
            throw new InvalidOperationException("TextLayer could not capture an empty screen region.");
        }

        var captureDirectory = Path.Combine(Path.GetTempPath(), "TextLayer", "Captures");
        Directory.CreateDirectory(captureDirectory);

        var filePath = Path.Combine(captureDirectory, $"capture-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.png");
        using var bitmap = new Bitmap(pixelBounds.Width, pixelBounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(pixelBounds.X, pixelBounds.Y, 0, 0, new Size(pixelBounds.Width, pixelBounds.Height), CopyPixelOperation.SourceCopy);
        }

        bitmap.Save(filePath, ImageFormat.Png);
        return filePath;
    }
}
