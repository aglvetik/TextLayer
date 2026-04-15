using TextLayer.Application.Models;

namespace TextLayer.Infrastructure.Ocr;

public sealed class TesseractDataPathResolver(string? baseDirectory = null, Func<string?>? prefixProvider = null)
{
    private readonly string baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
        ? AppContext.BaseDirectory
        : baseDirectory;

    private readonly Func<string?> prefixProvider = prefixProvider
        ?? (() => Environment.GetEnvironmentVariable("TESSDATA_PREFIX"));

    public string Resolve(OcrLanguageMode languageMode)
    {
        foreach (var candidate in GetCandidateDirectories())
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            if (HasRequiredLanguageData(candidate, languageMode))
            {
                return candidate;
            }
        }

        throw new TesseractConfigurationException(
            $"TextLayer could not find the required Tesseract language data. Place the traineddata files in '{Path.Combine(baseDirectory, "tessdata")}'.");
    }

    private IEnumerable<string> GetCandidateDirectories()
    {
        yield return Path.Combine(baseDirectory, "tessdata");
        yield return Path.Combine(baseDirectory, "Assets", "tessdata");

        var prefix = prefixProvider();
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            yield return prefix;
            yield return Path.Combine(prefix, "tessdata");
        }
    }

    private static bool HasRequiredLanguageData(string directoryPath, OcrLanguageMode languageMode)
    {
        foreach (var language in GetLanguageCodes(languageMode))
        {
            var dataFilePath = Path.Combine(directoryPath, $"{language}.traineddata");
            if (!File.Exists(dataFilePath))
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<string> GetLanguageCodes(OcrLanguageMode languageMode)
        => languageMode switch
        {
            OcrLanguageMode.English => ["eng"],
            OcrLanguageMode.Russian => ["rus"],
            _ => ["eng", "rus"],
        };
}
