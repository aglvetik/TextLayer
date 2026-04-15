using TextLayer.Application.Models;
using TextLayer.Infrastructure.Ocr;

namespace TextLayer.Tests.Infrastructure;

public sealed class TesseractDataPathResolverTests
{
    [Fact]
    public void Resolve_ThrowsFriendlyError_WhenLanguageDataIsMissing()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "TextLayerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDirectory);

        try
        {
            var resolver = new TesseractDataPathResolver(baseDirectory, () => null);

            var exception = Assert.Throws<TesseractConfigurationException>(
                () => resolver.Resolve(OcrLanguageMode.EnglishRussian));

            Assert.Contains("tessdata", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(baseDirectory))
            {
                Directory.Delete(baseDirectory, recursive: true);
            }
        }
    }
}
