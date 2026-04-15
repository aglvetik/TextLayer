using System.Drawing;
using TextLayer.Application.Models;
using TextLayer.Infrastructure.Ocr;

namespace TextLayer.Tests.Infrastructure;

public sealed class TesseractOcrEngineTests
{
    [Fact]
    public async Task AccurateMode_CanRunRepeatedly_WithoutLeavingPageLocked()
    {
        var sourcePath = CreateTempImage();
        try
        {
            var engine = new TesseractOcrEngine(new TesseractDataPathResolver(GetAppProjectBaseDirectory()));
            var request = new OcrRequestOptions(OcrMode.Accurate, OcrLanguageMode.Russian);

            var first = await engine.RecognizeAsync(sourcePath, request, CancellationToken.None);
            var second = await engine.RecognizeAsync(sourcePath, request, CancellationToken.None);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(OcrEngineSelector.AccurateEngineId, first.OcrEngineId);
            Assert.Equal(OcrEngineSelector.AccurateEngineId, second.OcrEngineId);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    private static string CreateTempImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        using var bitmap = new Bitmap(720, 220);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(35, 39, 47));
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using var brush = new SolidBrush(Color.FromArgb(242, 244, 248));
        using var accentBrush = new SolidBrush(Color.FromArgb(255, 126, 182));
        using var font = new Font("Segoe UI", 24, FontStyle.Regular, GraphicsUnit.Pixel);
        using var boldFont = new Font("Segoe UI", 26, FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.DrawString("Привет и мир", boldFont, brush, new PointF(28, 34));
        graphics.DrawString("русский текст и English", font, accentBrush, new PointF(28, 92));
        graphics.DrawString("и к в", font, brush, new PointF(28, 148));
        bitmap.Save(path);
        return path;
    }

    private static string GetAppProjectBaseDirectory()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "TextLayer.sln")))
            {
                return Path.Combine(directory, "src", "TextLayer.App");
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate the TextLayer repository root for the OCR integration test.");
    }
}
