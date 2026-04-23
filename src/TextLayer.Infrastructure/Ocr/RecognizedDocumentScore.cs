using TextLayer.Application.Models;
using TextLayer.Domain.Models;

namespace TextLayer.Infrastructure.Ocr;

public sealed record RecognizedDocumentScore(
    double Value,
    OcrLanguageMode SuggestedLanguageMode,
    bool IsStrong,
    int LatinCharacterCount,
    int CyrillicCharacterCount,
    int SuspiciousPseudoLatinWordCount,
    int SuspiciousPseudoCyrillicWordCount,
    int MixedScriptWordCount,
    ScriptDominance DominantScript,
    int CorrectedWordCount);

public sealed class RecognizedDocumentScoreCalculator
{
    private readonly ScriptAwareOcrAnalyzer analyzer = new();

    public RecognizedDocumentScore Score(RecognizedDocument document, OcrLanguageMode languageMode)
    {
        if (document.Words.Count == 0 || string.IsNullOrWhiteSpace(document.FullText))
        {
            return new RecognizedDocumentScore(0d, OcrLanguageMode.EnglishRussian, false, 0, 0, 0, 0, 0, ScriptDominance.Unknown, 0);
        }

        var text = document.FullText;
        var nonWhitespaceCount = text.Count(static character => !char.IsWhiteSpace(character));
        var alphaNumericCount = text.Count(static character => char.IsLetterOrDigit(character));
        var latinCount = text.Count(static character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
        var cyrillicCount = text.Count(static character => character is >= '\u0400' and <= '\u04FF');
        var analysis = analyzer.Analyze(document, languageMode);
        var suspiciousSymbolCount = text.Count(static character =>
            !char.IsWhiteSpace(character)
            && !char.IsLetterOrDigit(character)
            && character is not '.' and not ',' and not ':' and not ';' and not '!' and not '?'
            && character is not '(' and not ')' and not '[' and not ']' and not '{' and not '}'
            && character is not '-' and not '_' and not '/' and not '\\' and not '"' and not '\'' and not '+'
            && character is not '#' and not '@' and not '%' and not '&' and not '*' and not '=');

        var alphaRatio = nonWhitespaceCount == 0 ? 0d : alphaNumericCount / (double)nonWhitespaceCount;
        var protectedTokenHits = CountProtectedTokenHits(text);
        var duplicateTokenPenalty = GetDuplicateTokenPenalty(document);
        var mixedCorruptionPenalty = CountMixedCorruptedWords(document);
        var cleanCyrillicWordBonus = CountCleanCyrillicWords(document);
        var averageConfidence = document.Words
            .Where(word => word.Confidence.HasValue)
            .Select(word => word.Confidence!.Value)
            .DefaultIfEmpty(72d)
            .Average();
        var singleLetterNoise = document.Words.Count(word =>
            word.NormalizedText.Length == 1
            && !string.Equals(word.NormalizedText, "I", StringComparison.Ordinal)
            && !string.Equals(word.NormalizedText, "a", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(word.NormalizedText, "\u044F", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(word.NormalizedText, "\u0432", StringComparison.OrdinalIgnoreCase));
        var pseudoLatinPenalty = languageMode switch
        {
            OcrLanguageMode.Russian => 18d,
            OcrLanguageMode.EnglishRussian => 16d,
            _ => 5d,
        };
        var pseudoCyrillicPenalty = languageMode switch
        {
            OcrLanguageMode.English => 10.5d,
            OcrLanguageMode.EnglishRussian => 5.8d,
            _ => 4.2d,
        };

        var baseScore = (document.Words.Count * 5.5d)
            + (document.Lines.Count * 2d)
            + (nonWhitespaceCount * 0.32d)
            + (averageConfidence * 0.55d)
            + (alphaRatio * 28d)
            - (suspiciousSymbolCount * 1.6d)
            - (singleLetterNoise * 1.3d)
            - (analysis.MixedScriptWordCount * 4.5d)
            - (analysis.SuspiciousPseudoLatinWordCount * pseudoLatinPenalty)
            - (analysis.SuspiciousPseudoCyrillicWordCount * pseudoCyrillicPenalty)
            - (analysis.AmbiguousWordCount * 1.1d)
            - (mixedCorruptionPenalty * 8.5d)
            - duplicateTokenPenalty
            + (analysis.CorrectedWordCount * 3.6d)
            + (protectedTokenHits * 5.8d)
            + (cleanCyrillicWordBonus * (languageMode is OcrLanguageMode.Russian or OcrLanguageMode.EnglishRussian ? 1.35d : 0.3d));

        var languageFitScore = languageMode switch
        {
            OcrLanguageMode.English => (latinCount * 0.42d) - (cyrillicCount * 0.16d),
            OcrLanguageMode.Russian => (cyrillicCount * 0.48d) - (latinCount * 0.18d),
            OcrLanguageMode.EnglishRussian => analysis.LikelyMixedText
                ? (Math.Min(latinCount, cyrillicCount) * 0.46d) + (Math.Max(latinCount, cyrillicCount) * 0.14d)
                : (Math.Max(latinCount, cyrillicCount) * 0.18d),
            _ => 0d,
        };

        var dominantScriptBonus = languageMode switch
        {
            OcrLanguageMode.English when analysis.DominantScript == ScriptDominance.Latin => 16d,
            OcrLanguageMode.Russian when analysis.DominantScript == ScriptDominance.Cyrillic => 18d,
            OcrLanguageMode.EnglishRussian when analysis.LikelyMixedText => 24d,
            OcrLanguageMode.EnglishRussian when analysis.DominantScript == ScriptDominance.Cyrillic && analysis.SuspiciousPseudoLatinWordCount == 0 => 8d,
            OcrLanguageMode.EnglishRussian when analysis.DominantScript == ScriptDominance.Latin && analysis.SuspiciousPseudoCyrillicWordCount == 0 => 8d,
            _ => 0d,
        };

        var suggestedLanguage = (languageMode == OcrLanguageMode.EnglishRussian && latinCount > 0 && cyrillicCount > 0)
            ? OcrLanguageMode.EnglishRussian
            : analysis.LikelyMixedText
                ? OcrLanguageMode.EnglishRussian
                : analysis.DominantScript switch
                {
                    ScriptDominance.Cyrillic => OcrLanguageMode.Russian,
                    ScriptDominance.Latin => OcrLanguageMode.English,
                    _ => GetSuggestedLanguage(latinCount, cyrillicCount),
                };

        var score = Math.Max(0d, baseScore + languageFitScore + dominantScriptBonus);
        var isStrong = score >= 95d
            || (document.Words.Count >= 10
                && alphaRatio >= 0.58d
                && suspiciousSymbolCount <= Math.Max(6, nonWhitespaceCount / 8)
                && analysis.SuspiciousPseudoLatinWordCount <= (languageMode == OcrLanguageMode.EnglishRussian ? 0 : 1)
                && analysis.SuspiciousPseudoCyrillicWordCount <= 1
                && analysis.MixedScriptWordCount <= Math.Max(1, document.Words.Count / 12));

        return new RecognizedDocumentScore(
            score,
            suggestedLanguage,
            isStrong,
            latinCount,
            cyrillicCount,
            analysis.SuspiciousPseudoLatinWordCount,
            analysis.SuspiciousPseudoCyrillicWordCount,
            analysis.MixedScriptWordCount,
            analysis.DominantScript,
            analysis.CorrectedWordCount);
    }

    private static OcrLanguageMode GetSuggestedLanguage(int latinCount, int cyrillicCount)
    {
        if (latinCount >= 4 && cyrillicCount >= 4)
        {
            return OcrLanguageMode.EnglishRussian;
        }

        if (latinCount >= 2 && cyrillicCount >= 2)
        {
            return OcrLanguageMode.EnglishRussian;
        }

        if (latinCount >= 12 && latinCount >= cyrillicCount * 2)
        {
            return OcrLanguageMode.English;
        }

        if (cyrillicCount >= 12 && cyrillicCount >= latinCount * 2)
        {
            return OcrLanguageMode.Russian;
        }

        return OcrLanguageMode.EnglishRussian;
    }

    private static int CountProtectedTokenHits(string text)
    {
        var tokens = new[]
        {
            "TextLayer",
            "OCR",
            "Windows",
            "Tesseract",
            "GitHub",
            "Releases",
            "Download",
            "Setup",
            "Ctrl+Shift+O",
            "Esc",
            ".exe",
        };

        return tokens.Count(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static double GetDuplicateTokenPenalty(RecognizedDocument document)
    {
        var repeatedCount = document.Words
            .Select(word => NormalizeTokenForDuplication(word.NormalizedText))
            .Where(token => token.Length >= 3)
            .GroupBy(token => token, StringComparer.OrdinalIgnoreCase)
            .Sum(group => Math.Max(0, group.Count() - 2));

        return repeatedCount * 2.4d;
    }

    private static string NormalizeTokenForDuplication(string text)
        => new(text.Where(char.IsLetterOrDigit).ToArray());

    private static int CountMixedCorruptedWords(RecognizedDocument document)
        => document.Words.Count(word => IsMixedCorruptedToken(word.NormalizedText));

    private static int CountCleanCyrillicWords(RecognizedDocument document)
        => document.Words.Count(word => IsCleanCyrillicToken(word.NormalizedText));

    private static bool IsMixedCorruptedToken(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || LooksTechnicalToken(text))
        {
            return false;
        }

        var hasLatin = false;
        var hasCyrillic = false;
        foreach (var character in text)
        {
            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                hasLatin = true;
            }
            else if (character is >= '\u0400' and <= '\u04FF')
            {
                hasCyrillic = true;
            }
        }

        return hasLatin && hasCyrillic;
    }

    private static bool IsCleanCyrillicToken(string text)
    {
        var hasCyrillic = false;
        foreach (var character in text)
        {
            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                return false;
            }

            if (character is >= '\u0400' and <= '\u04FF')
            {
                hasCyrillic = true;
            }
        }

        return hasCyrillic;
    }

    private static bool LooksTechnicalToken(string text)
        => text.Contains("://", StringComparison.Ordinal)
           || text.Contains("www.", StringComparison.OrdinalIgnoreCase)
           || text.Contains(".exe", StringComparison.OrdinalIgnoreCase)
           || text.Contains("TextLayer", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Windows", StringComparison.OrdinalIgnoreCase)
           || text.Contains("GitHub", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Tesseract", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Ctrl", StringComparison.OrdinalIgnoreCase);
}
