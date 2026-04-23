using System.Text;
using TextLayer.Application.Models;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;

namespace TextLayer.Infrastructure.Ocr;

public sealed class ScriptAwareOcrPostProcessor
{
    private static readonly IReadOnlyDictionary<char, char> LatinToCyrillicMap = new Dictionary<char, char>
    {
        ['A'] = '\u0410',
        ['a'] = '\u0430',
        ['B'] = '\u0412',
        ['b'] = '\u0432',
        ['C'] = '\u0421',
        ['c'] = '\u0441',
        ['E'] = '\u0415',
        ['e'] = '\u0435',
        ['H'] = '\u041D',
        ['h'] = '\u043D',
        ['K'] = '\u041A',
        ['k'] = '\u043A',
        ['M'] = '\u041C',
        ['m'] = '\u043C',
        ['O'] = '\u041E',
        ['o'] = '\u043E',
        ['P'] = '\u0420',
        ['p'] = '\u0440',
        ['T'] = '\u0422',
        ['t'] = '\u0442',
        ['V'] = '\u0412',
        ['v'] = '\u0432',
        ['X'] = '\u0425',
        ['x'] = '\u0445',
        ['Y'] = '\u0423',
        ['y'] = '\u0443',
    };

    private static readonly IReadOnlyDictionary<char, char> AggressivePseudoLatinToCyrillicMap = new Dictionary<char, char>
    {
        ['N'] = '\u041F',
        ['n'] = '\u043F',
    };

    private static readonly IReadOnlyDictionary<char, char> CyrillicToLatinMap = new Dictionary<char, char>
    {
        ['\u0410'] = 'A',
        ['\u0430'] = 'a',
        ['\u0401'] = 'E',
        ['\u0451'] = 'e',
        ['\u0412'] = 'B',
        ['\u0432'] = 'b',
        ['\u0415'] = 'E',
        ['\u0435'] = 'e',
        ['\u0406'] = 'I',
        ['\u0456'] = 'i',
        ['\u041A'] = 'K',
        ['\u043A'] = 'k',
        ['\u041C'] = 'M',
        ['\u043C'] = 'm',
        ['\u041D'] = 'H',
        ['\u043D'] = 'h',
        ['\u041E'] = 'O',
        ['\u043E'] = 'o',
        ['\u0420'] = 'P',
        ['\u0440'] = 'p',
        ['\u0405'] = 'S',
        ['\u0455'] = 's',
        ['\u0421'] = 'C',
        ['\u0441'] = 'c',
        ['\u0422'] = 'T',
        ['\u0442'] = 't',
        ['\u0425'] = 'X',
        ['\u0445'] = 'x',
        ['\u0423'] = 'Y',
        ['\u0443'] = 'y',
    };

    private static readonly IReadOnlyList<ProtectedToken> ProtectedTokens =
    [
        new("TextLayer", "textlayer", 2),
        new("TextLayer-Setup", "textlayersetup", 3),
        new("OCR", "ocr", 1),
        new("Windows", "windows", 2),
        new("Tesseract", "tesseract", 2),
        new("GitHub", "github", 1),
        new("Releases", "releases", 2),
        new("Download", "download", 2),
        new("Setup", "setup", 1),
        new("Ctrl+Shift+O", "ctrlshifto", 2),
        new("Esc", "esc", 1),
    ];

    private readonly RecognizedDocumentNoiseFilter noiseFilter = new();
    private readonly ScriptAwareOcrAnalyzer analyzer = new();

    public ScriptAwareOcrProcessingResult Process(
        RecognizedDocument document,
        OcrLanguageMode requestedLanguageMode,
        RecognizedDocumentNoiseFilter.NoiseFilterProfile filterProfile = RecognizedDocumentNoiseFilter.NoiseFilterProfile.Standard)
    {
        var initiallyCorrected = RebuildCorrectedDocument(document, requestedLanguageMode);
        var mergedDocument = MergeAdjacentWordFragments(initiallyCorrected.Document);
        var filteredDocument = noiseFilter.Filter(mergedDocument, filterProfile);
        var finalCorrection = RebuildCorrectedDocument(filteredDocument, requestedLanguageMode);

        var analysis = analyzer.Analyze(finalCorrection.Document, requestedLanguageMode) with
        {
            CorrectedWordCount = initiallyCorrected.CorrectedWordCount + finalCorrection.CorrectedWordCount,
        };

        return new ScriptAwareOcrProcessingResult(finalCorrection.Document, analysis);
    }

    private static CorrectedDocumentResult RebuildCorrectedDocument(RecognizedDocument document, OcrLanguageMode requestedLanguageMode)
    {
        if (document.Words.Count == 0)
        {
            return new CorrectedDocumentResult(document, 0);
        }

        var wordsById = document.Words.ToDictionary(word => word.WordId);
        var lineContexts = BuildLineContexts(document, wordsById, requestedLanguageMode);
        var documentMetrics = document.Words.Select(word => AnalyzeWord(word.NormalizedText)).ToArray();
        var documentContext = BuildDocumentContext(documentMetrics, requestedLanguageMode);

        var correctedWordCount = 0;
        var correctedWords = new List<RecognizedWord>(document.Words.Count);
        var correctedLines = new List<RecognizedLine>(document.Lines.Count);

        foreach (var line in document.Lines.OrderBy(line => line.Index))
        {
            var lineWords = line.WordIds
                .Select(wordId => wordsById.GetValueOrDefault(wordId))
                .OfType<RecognizedWord>()
                .OrderBy(word => word.Index)
                .ToArray();

            if (lineWords.Length == 0)
            {
                continue;
            }

            var lineContext = lineContexts.TryGetValue(line.Index, out var existingLineContext)
                ? existingLineContext
                : BuildLineContext(lineWords, requestedLanguageMode);
            var remappedWords = new List<RecognizedWord>(lineWords.Length);
            foreach (var word in lineWords)
            {
                var correctedWord = CorrectWord(word, lineContext, documentContext, requestedLanguageMode);
                if (!string.Equals(correctedWord.Text, word.Text, StringComparison.Ordinal))
                {
                    correctedWordCount++;
                }

                remappedWords.Add(correctedWord with
                {
                    Index = correctedWords.Count + remappedWords.Count,
                    LineIndex = correctedLines.Count,
                });
            }

            correctedWords.AddRange(remappedWords);
            correctedLines.Add(new RecognizedLine(
                Guid.NewGuid(),
                correctedLines.Count,
                string.Join(' ', remappedWords.Select(word => word.Text)),
                BuildLineRect(remappedWords),
                null,
                remappedWords.Select(word => word.WordId).ToArray()));
        }

        return new CorrectedDocumentResult(
            document with
            {
                Lines = correctedLines,
                Words = correctedWords,
                FullText = correctedLines.Count == 0
                    ? string.Empty
                    : string.Join(Environment.NewLine, correctedLines.Select(line => line.Text)),
            },
            correctedWordCount);
    }

    private static Dictionary<int, ScriptContext> BuildLineContexts(
        RecognizedDocument document,
        IReadOnlyDictionary<Guid, RecognizedWord> wordsById,
        OcrLanguageMode requestedLanguageMode)
    {
        var contexts = new Dictionary<int, ScriptContext>();
        foreach (var line in document.Lines.OrderBy(line => line.Index))
        {
            var lineWords = line.WordIds
                .Select(wordId => wordsById.GetValueOrDefault(wordId))
                .OfType<RecognizedWord>()
                .ToArray();
            contexts[line.Index] = BuildLineContext(lineWords, requestedLanguageMode);
        }

        return contexts;
    }

    private static ScriptContext BuildLineContext(IReadOnlyList<RecognizedWord> lineWords, OcrLanguageMode requestedLanguageMode)
    {
        var metrics = lineWords.Select(word => AnalyzeWord(word.NormalizedText)).ToArray();
        return BuildDocumentContext(metrics, requestedLanguageMode);
    }

    private static ScriptContext BuildDocumentContext(IEnumerable<WordScriptMetrics> metrics, OcrLanguageMode requestedLanguageMode)
    {
        var latinWeight = 0d;
        var cyrillicWeight = 0d;

        foreach (var metric in metrics)
        {
            latinWeight += (metric.LatinSpecificCount * 2.4d)
                + (metric.LatinLetterCount > 0 && metric.CyrillicLetterCount == 0 ? 1.1d : 0d);
            cyrillicWeight += (metric.CyrillicSpecificCount * 2.4d)
                + (metric.CyrillicLetterCount > 0 && metric.LatinLetterCount == 0 ? 1.1d : 0d);

            if (metric.IsSuspiciousPseudoLatin)
            {
                cyrillicWeight += 0.9d;
            }

            if (metric.IsSuspiciousPseudoCyrillic)
            {
                latinWeight += 0.9d;
            }
        }

        switch (requestedLanguageMode)
        {
            case OcrLanguageMode.English:
                latinWeight += 4d;
                break;
            case OcrLanguageMode.Russian:
                cyrillicWeight += 4d;
                break;
            case OcrLanguageMode.EnglishRussian:
                latinWeight += 1.5d;
                cyrillicWeight += 1.5d;
                break;
        }

        return new ScriptContext(latinWeight, cyrillicWeight);
    }

    private static RecognizedWord CorrectWord(
        RecognizedWord word,
        ScriptContext lineContext,
        ScriptContext documentContext,
        OcrLanguageMode requestedLanguageMode)
    {
        if (string.IsNullOrWhiteSpace(word.NormalizedText))
        {
            return word;
        }

        if (TryCorrectProtectedToken(word, out var protectedWord))
        {
            return protectedWord;
        }

        if (LooksLikeProtectedToken(word.NormalizedText))
        {
            return word;
        }

        var metrics = AnalyzeWord(word.NormalizedText);
        if (!metrics.HasLetters)
        {
            return word;
        }

        var targetScript = ResolveTargetScript(metrics, lineContext, documentContext, requestedLanguageMode);
        var correctedText = targetScript switch
        {
            ScriptKind.Cyrillic => ConvertToCyrillic(word.Text, metrics, lineContext, documentContext, requestedLanguageMode),
            ScriptKind.Latin => ConvertToLatin(word.Text, metrics, lineContext, documentContext, requestedLanguageMode),
            _ => CleanupMixedScriptWord(word.Text, metrics, lineContext, documentContext, requestedLanguageMode),
        };
        correctedText = CleanupMixedScriptWord(correctedText, AnalyzeWord(NormalizeWord(correctedText)), lineContext, documentContext, requestedLanguageMode);

        var normalized = NormalizeWord(correctedText);
        if (string.IsNullOrWhiteSpace(normalized)
            || string.Equals(normalized, word.NormalizedText, StringComparison.Ordinal))
        {
            return word;
        }

        var correctedWord = word with
        {
            Text = correctedText,
            NormalizedText = normalized,
        };

        return TryCorrectProtectedToken(correctedWord, out protectedWord)
            ? protectedWord
            : correctedWord;
    }

    private static bool TryCorrectProtectedToken(RecognizedWord word, out RecognizedWord correctedWord)
    {
        correctedWord = word;
        var rawText = word.Text;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return false;
        }

        var (prefix, core, suffix) = SplitTokenAffixes(rawText);
        if (string.IsNullOrWhiteSpace(core))
        {
            return false;
        }

        var skeleton = BuildLatinProtectedTokenSkeleton(core);
        if (TryRepairInstallerToken(core, skeleton, out var repairedInstaller))
        {
            correctedWord = word with
            {
                Text = prefix + repairedInstaller + suffix,
                NormalizedText = NormalizeWord(prefix + repairedInstaller + suffix),
            };
            return !string.Equals(correctedWord.NormalizedText, word.NormalizedText, StringComparison.Ordinal);
        }

        if (TryRepairExecutableToken(core, skeleton, out var repairedExecutable))
        {
            correctedWord = word with
            {
                Text = prefix + repairedExecutable + suffix,
                NormalizedText = NormalizeWord(prefix + repairedExecutable + suffix),
            };
            return !string.Equals(correctedWord.NormalizedText, word.NormalizedText, StringComparison.Ordinal);
        }

        if (TryRepairTechnicalLatinToken(core, out var repairedTechnicalToken))
        {
            correctedWord = word with
            {
                Text = prefix + repairedTechnicalToken + suffix,
                NormalizedText = NormalizeWord(prefix + repairedTechnicalToken + suffix),
            };
            return !string.Equals(correctedWord.NormalizedText, word.NormalizedText, StringComparison.Ordinal);
        }

        var folded = FoldProtectedToken(skeleton);
        if (folded.Length == 0)
        {
            return false;
        }

        foreach (var token in ProtectedTokens)
        {
            if (IsProtectedTokenMatch(folded, token))
            {
                correctedWord = word with
                {
                    Text = prefix + token.Canonical + suffix,
                    NormalizedText = NormalizeWord(prefix + token.Canonical + suffix),
                };
                return !string.Equals(correctedWord.NormalizedText, word.NormalizedText, StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static (string Prefix, string Core, string Suffix) SplitTokenAffixes(string text)
    {
        var start = 0;
        var end = text.Length - 1;

        while (start <= end && IsWrapperPrefix(text[start]))
        {
            start++;
        }

        while (end >= start && IsWrapperSuffix(text[end]))
        {
            end--;
        }

        return (
            text[..start],
            text[start..(end + 1)],
            text[(end + 1)..]);
    }

    private static bool IsWrapperPrefix(char character)
        => character is '"' or '\'' or '(' or '[' or '{' or '<';

    private static bool IsWrapperSuffix(char character)
        => character is '"' or '\'' or ')' or ']' or '}' or '>' or ',' or ';' or ':' or '!' or '?';

    private static bool TryRepairExecutableToken(string core, string skeleton, out string repaired)
    {
        repaired = string.Empty;
        if (!skeleton.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        var parts = skeleton.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var extension = parts[^1];
        if (EditDistance(FoldProtectedToken(extension), "exe") > 1)
        {
            return false;
        }

        var name = string.Join('.', parts[..^1]);
        if (string.IsNullOrWhiteSpace(name))
        {
            repaired = ".exe";
            return true;
        }

        var foldedName = FoldProtectedToken(name);
        if ((foldedName.Contains("setup", StringComparison.Ordinal) || foldedName.Contains("textlayer", StringComparison.Ordinal))
            && EditDistance(foldedName.Replace("setup", string.Empty, StringComparison.Ordinal), "textlayer") <= 4)
        {
            var installerVersion = ExtractInstallerVersion(core, skeleton);
            repaired = string.IsNullOrWhiteSpace(installerVersion)
                ? "TextLayer-Setup.exe"
                : $"TextLayer-Setup-{installerVersion}.exe";
            return !string.Equals(repaired, core, StringComparison.Ordinal);
        }

        var repairedName = ProtectedTokens
            .Where(token => token.Folded.Length > 3)
            .OrderBy(token => EditDistance(foldedName, token.Folded))
            .FirstOrDefault(token => EditDistance(foldedName, token.Folded) <= token.MaxEditDistance);

        repaired = repairedName is null
            ? $"{name}.exe"
            : $"{repairedName.Canonical}.exe";
        return !string.Equals(repaired, core, StringComparison.Ordinal);
    }

    private static bool TryRepairInstallerToken(string core, string skeleton, out string repaired)
    {
        repaired = string.Empty;
        var folded = FoldProtectedToken(skeleton);
        var extensionSeparator = skeleton.LastIndexOf('.');
        if (extensionSeparator < 0)
        {
            return false;
        }

        var extension = skeleton[(extensionSeparator + 1)..];
        if (EditDistance(FoldProtectedToken(extension), "exe") > 1)
        {
            return false;
        }

        var name = skeleton[..extensionSeparator];
        var nameSegments = name.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
        var hasSetup = nameSegments.Any(segment => EditDistance(FoldProtectedToken(segment), "setup") <= 1)
            || folded.Contains("setup", StringComparison.Ordinal);
        var hasTextLayer = folded.Contains("text", StringComparison.Ordinal)
            || folded.Contains("layer", StringComparison.Ordinal)
            || EditDistance(FoldProtectedToken(name.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty)), "textlayer") <= 3
            || nameSegments.Any(segment => EditDistance(FoldProtectedToken(segment), "textlayer") <= 3)
            || folded.Contains("textlayer", StringComparison.Ordinal);
        if (!hasSetup || !hasTextLayer)
        {
            return false;
        }

        var version = ExtractInstallerVersion(core, skeleton);
        if (string.IsNullOrWhiteSpace(version))
        {
            version = ExtractInstallerVersion(skeleton, skeleton);
        }

        repaired = string.IsNullOrWhiteSpace(version)
            ? "TextLayer-Setup.exe"
            : $"TextLayer-Setup-{version}.exe";
        return !string.Equals(repaired, core, StringComparison.Ordinal);
    }

    private static string ExtractInstallerVersion(string source, string skeleton)
    {
        var setupIndex = skeleton.IndexOf("Setup", StringComparison.OrdinalIgnoreCase);
        if (setupIndex < 0)
        {
            return NormalizeExtractedVersion(ExtractVersionToken(source));
        }

        var startIndex = Math.Min(source.Length, setupIndex + "Setup".Length);
        return NormalizeExtractedVersion(ExtractVersionToken(source[startIndex..]));
    }

    private static string ExtractVersionToken(string text)
    {
        var bestStart = -1;
        var bestLength = 0;
        var currentStart = -1;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            var isVersionCharacter = char.IsDigit(character) || character == '.';
            if (isVersionCharacter)
            {
                currentStart = currentStart >= 0 ? currentStart : index;
                continue;
            }

            if (currentStart >= 0)
            {
                var length = index - currentStart;
                if (length > bestLength && text[currentStart..index].Any(char.IsDigit))
                {
                    bestStart = currentStart;
                    bestLength = length;
                }

                currentStart = -1;
            }
        }

        if (currentStart >= 0)
        {
            var length = text.Length - currentStart;
            if (length > bestLength && text[currentStart..].Any(char.IsDigit))
            {
                bestStart = currentStart;
                bestLength = length;
            }
        }

        return bestStart >= 0
            ? text.Substring(bestStart, bestLength).Trim('.')
            : string.Empty;
    }

    private static string NormalizeExtractedVersion(string version)
        => new string(version
            .Select(character => character switch
            {
                'l' or 'I' => '1',
                'o' or 'O' => '0',
                _ => character,
            })
            .Where(character => char.IsDigit(character) || character == '.')
            .ToArray())
            .Trim('.');

    private static bool IsProtectedTokenMatch(string folded, ProtectedToken token)
    {
        if (string.Equals(folded, token.Folded, StringComparison.Ordinal))
        {
            return true;
        }

        if (token.Folded == "ctrlshifto"
            && folded.Contains("ctrl", StringComparison.Ordinal)
            && folded.Contains("shift", StringComparison.Ordinal)
            && (folded.EndsWith('o') || folded.EndsWith('0')))
        {
            return true;
        }

        if (token.Folded.Length <= 3 && folded.Length < token.Folded.Length)
        {
            return false;
        }

        return EditDistance(folded, token.Folded) <= token.MaxEditDistance;
    }

    private static bool TryRepairTechnicalLatinToken(string core, out string repaired)
    {
        repaired = string.Empty;
        if (!LooksTechnicalTokenLike(core))
        {
            return false;
        }

        var builder = new StringBuilder(core.Length);
        for (var index = 0; index < core.Length; index++)
        {
            var character = core[index];
            if (CyrillicToLatinMap.TryGetValue(character, out var mapped))
            {
                builder.Append(mapped);
                continue;
            }

            builder.Append(character switch
            {
                '\u0401' => 'E',
                '\u0451' => 'e',
                '\u0417' => IsLikelyDigitContext(core, index) ? '3' : 'Z',
                '\u0437' => IsLikelyDigitContext(core, index) ? '3' : 'z',
                _ => NormalizeTechnicalDigitLookalike(character, core, index),
            });
        }

        repaired = builder.ToString();
        return !string.Equals(repaired, core, StringComparison.Ordinal);
    }

    private static bool LooksTechnicalTokenLike(string text)
    {
        var folded = FoldProtectedToken(BuildLatinProtectedTokenSkeleton(text));
        return text.Contains("://", StringComparison.Ordinal)
               || text.Contains("www.", StringComparison.OrdinalIgnoreCase)
               || text.Contains(".exe", StringComparison.OrdinalIgnoreCase)
               || text.Contains('\\')
               || text.Contains('/')
               || text.Contains('@')
               || text.Contains('+')
               || folded.Contains("textlayer", StringComparison.Ordinal)
               || folded.Contains("github", StringComparison.Ordinal)
               || folded.Contains("tesseract", StringComparison.Ordinal)
               || folded.Contains("windows", StringComparison.Ordinal)
               || folded.Contains("ctrl", StringComparison.Ordinal)
               || folded.Contains("shift", StringComparison.Ordinal)
               || folded.Contains("esc", StringComparison.Ordinal);
    }

    private static char NormalizeTechnicalDigitLookalike(char character, string text, int index)
        => character switch
        {
            '0' when ShouldTreatZeroAsLetter(text, index) => IsUppercaseContext(text, index) ? 'O' : 'o',
            '1' when IsSurroundedByLetters(text, index) => char.IsUpper(text[Math.Max(0, index - 1)]) ? 'I' : 'l',
            '5' when IsSurroundedByLetters(text, index) => char.IsUpper(text[Math.Max(0, index - 1)]) ? 'S' : 's',
            _ => character,
        };

    private static bool IsLikelyDigitContext(string text, int index)
        => (index > 0 && char.IsDigit(text[index - 1]))
           || (index + 1 < text.Length && char.IsDigit(text[index + 1]));

    private static bool IsSurroundedByLetters(string text, int index)
        => index > 0
           && index + 1 < text.Length
           && char.IsLetter(text[index - 1])
           && char.IsLetter(text[index + 1]);

    private static string BuildLatinProtectedTokenSkeleton(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (CyrillicToLatinMap.TryGetValue(character, out var mapped))
            {
                builder.Append(mapped);
                continue;
            }

            builder.Append(character switch
            {
                '\u0413' => 'G',
                '\u0433' => 'g',
                '\u0418' => 'N',
                '\u0438' => 'n',
                '\u041B' => 'L',
                '\u043B' => 'l',
                '\u0417' => '3',
                '\u0437' => '3',
                '\u0401' => 'E',
                '\u0451' => 'e',
                '\u042F' => 'R',
                '\u044F' => 'r',
                '\u042C' => 'b',
                '\u044C' => 'b',
                '\u0406' => 'I',
                '\u0456' => 'i',
                '\u0405' => 'S',
                '\u0455' => 's',
                '1' => 'l',
                '5' => 's',
                '7' => 't',
                _ => character,
            });
        }

        return builder.ToString();
    }

    private static string FoldProtectedToken(string text)
        => new(text
            .Where(char.IsLetterOrDigit)
            .Select(character => character switch
            {
                '0' => 'o',
                '1' => 'l',
                '5' => 's',
                '7' => 't',
                _ => char.ToLowerInvariant(character),
            })
            .ToArray());

    private static int EditDistance(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var index = 0; index <= right.Length; index++)
        {
            previous[index] = index;
        }

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static ScriptKind ResolveTargetScript(
        WordScriptMetrics metrics,
        ScriptContext lineContext,
        ScriptContext documentContext,
        OcrLanguageMode requestedLanguageMode)
    {
        if (metrics.MixedScript)
        {
            if (metrics.CyrillicLetterCount > metrics.LatinLetterCount
                && metrics.AmbiguousLatinCount > 0
                && ShouldFavorCyrillic(lineContext, documentContext, requestedLanguageMode))
            {
                return ScriptKind.Cyrillic;
            }

            if (metrics.LatinLetterCount > metrics.CyrillicLetterCount
                && metrics.AmbiguousCyrillicCount > 0
                && ShouldFavorLatin(lineContext, documentContext, requestedLanguageMode))
            {
                return ScriptKind.Latin;
            }

            if (requestedLanguageMode == OcrLanguageMode.Russian
                && metrics.CyrillicLetterCount >= metrics.LatinLetterCount
                && metrics.AmbiguousLatinCount > 0
                && ShouldFavorCyrillic(lineContext, documentContext, requestedLanguageMode))
            {
                return ScriptKind.Cyrillic;
            }

            if (requestedLanguageMode == OcrLanguageMode.English
                && metrics.LatinLetterCount >= metrics.CyrillicLetterCount
                && metrics.AmbiguousCyrillicCount > 0
                && ShouldFavorLatin(lineContext, documentContext, requestedLanguageMode))
            {
                return ScriptKind.Latin;
            }

            if (metrics.CyrillicLetterCount >= metrics.LatinLetterCount + 2
                && ShouldFavorCyrillic(lineContext, documentContext, requestedLanguageMode))
            {
                return ScriptKind.Cyrillic;
            }

            if (metrics.LatinLetterCount >= metrics.CyrillicLetterCount + 2
                && ShouldFavorLatin(lineContext, documentContext, requestedLanguageMode))
            {
                return ScriptKind.Latin;
            }

            if (metrics.CyrillicSpecificCount >= metrics.LatinSpecificCount + 1
                && metrics.AmbiguousLatinCount > 0
                && ShouldFavorCyrillic(lineContext, documentContext, requestedLanguageMode))
            {
                return ScriptKind.Cyrillic;
            }

            if (metrics.LatinSpecificCount >= metrics.CyrillicSpecificCount + 1
                && metrics.AmbiguousCyrillicCount > 0
                && ShouldFavorLatin(lineContext, documentContext, requestedLanguageMode))
            {
                return ScriptKind.Latin;
            }

            if (metrics.CyrillicSpecificCount > 0 && metrics.LatinSpecificCount == 0)
            {
                return ScriptKind.Cyrillic;
            }

            if (metrics.LatinSpecificCount > 0 && metrics.CyrillicSpecificCount == 0)
            {
                return ScriptKind.Latin;
            }
        }

        if (metrics.AllLatin && metrics.IsSuspiciousPseudoLatin && ShouldFavorCyrillic(lineContext, documentContext, requestedLanguageMode))
        {
            return ScriptKind.Cyrillic;
        }

        if (metrics.AllCyrillic
            && metrics.IsSuspiciousPseudoCyrillic
            && (requestedLanguageMode == OcrLanguageMode.English || metrics.CyrillicSpecificCount == 0)
            && ShouldFavorLatin(lineContext, documentContext, requestedLanguageMode))
        {
            return ScriptKind.Latin;
        }

        return ScriptKind.Unknown;
    }

    private static bool ShouldFavorCyrillic(ScriptContext lineContext, ScriptContext documentContext, OcrLanguageMode requestedLanguageMode)
    {
        if (requestedLanguageMode == OcrLanguageMode.Russian)
        {
            return lineContext.CyrillicWeight + documentContext.CyrillicWeight
                >= lineContext.LatinWeight + documentContext.LatinWeight - 1.5d;
        }

        if (lineContext.CyrillicWeight >= lineContext.LatinWeight + 1.8d)
        {
            return true;
        }

        if (documentContext.CyrillicWeight >= documentContext.LatinWeight + 3.4d)
        {
            return true;
        }

        return requestedLanguageMode == OcrLanguageMode.EnglishRussian
            && documentContext.CyrillicWeight >= documentContext.LatinWeight + 1.6d;
    }

    private static bool ShouldFavorLatin(ScriptContext lineContext, ScriptContext documentContext, OcrLanguageMode requestedLanguageMode)
    {
        if (requestedLanguageMode == OcrLanguageMode.English)
        {
            return lineContext.LatinWeight + documentContext.LatinWeight
                >= lineContext.CyrillicWeight + documentContext.CyrillicWeight - 1.5d;
        }

        if (lineContext.LatinWeight >= lineContext.CyrillicWeight + 1.8d)
        {
            return true;
        }

        if (documentContext.LatinWeight >= documentContext.CyrillicWeight + 3.4d)
        {
            return true;
        }

        return requestedLanguageMode == OcrLanguageMode.EnglishRussian
            && documentContext.LatinWeight >= documentContext.CyrillicWeight + 1.6d;
    }

    private static string ConvertToCyrillic(
        string sourceText,
        WordScriptMetrics metrics,
        ScriptContext lineContext,
        ScriptContext documentContext,
        OcrLanguageMode requestedLanguageMode)
    {
        var builder = new StringBuilder(sourceText.Length);
        var aggressive = metrics.IsSuspiciousPseudoLatin
            && ShouldFavorCyrillic(lineContext, documentContext, requestedLanguageMode);

        for (var index = 0; index < sourceText.Length; index++)
        {
            var character = sourceText[index];
            var characterKind = ClassifyCharacter(character);
            if (characterKind == CharacterScriptKind.LatinAmbiguous
                && LatinToCyrillicMap.TryGetValue(character, out var mapped))
            {
                builder.Append(mapped);
                continue;
            }

            if (aggressive
                && characterKind == CharacterScriptKind.LatinAmbiguous
                && AggressivePseudoLatinToCyrillicMap.TryGetValue(character, out mapped))
            {
                builder.Append(mapped);
                continue;
            }

            if (aggressive && character == '0' && ShouldTreatZeroAsLetter(sourceText, index))
            {
                builder.Append(IsUppercaseContext(sourceText, index) ? '\u041E' : '\u043E');
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string ConvertToLatin(
        string sourceText,
        WordScriptMetrics metrics,
        ScriptContext lineContext,
        ScriptContext documentContext,
        OcrLanguageMode requestedLanguageMode)
    {
        var builder = new StringBuilder(sourceText.Length);
        var aggressive = metrics.IsSuspiciousPseudoCyrillic
            && ShouldFavorLatin(lineContext, documentContext, requestedLanguageMode);

        for (var index = 0; index < sourceText.Length; index++)
        {
            var character = sourceText[index];
            var characterKind = ClassifyCharacter(character);
            if (characterKind == CharacterScriptKind.CyrillicAmbiguous
                && CyrillicToLatinMap.TryGetValue(character, out var mapped))
            {
                builder.Append(mapped);
                continue;
            }

            if (aggressive && character == '\u041E' && ShouldTreatCyrillicOAsLatin(sourceText))
            {
                builder.Append('O');
                continue;
            }

            if (aggressive && character == '\u043E' && ShouldTreatCyrillicOAsLatin(sourceText))
            {
                builder.Append('o');
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string CleanupMixedScriptWord(
        string sourceText,
        WordScriptMetrics metrics,
        ScriptContext lineContext,
        ScriptContext documentContext,
        OcrLanguageMode requestedLanguageMode)
    {
        if (!metrics.MixedScript || LooksLikeProtectedToken(sourceText))
        {
            return sourceText;
        }

        if (requestedLanguageMode == OcrLanguageMode.Russian
            && metrics.CyrillicLetterCount >= metrics.LatinLetterCount
            && metrics.AmbiguousLatinCount > 0
            && ShouldFavorCyrillic(lineContext, documentContext, requestedLanguageMode))
        {
            return ConvertToCyrillic(sourceText, metrics, lineContext, documentContext, requestedLanguageMode);
        }

        if (requestedLanguageMode == OcrLanguageMode.English
            && metrics.LatinLetterCount >= metrics.CyrillicLetterCount
            && metrics.AmbiguousCyrillicCount > 0
            && ShouldFavorLatin(lineContext, documentContext, requestedLanguageMode))
        {
            return ConvertToLatin(sourceText, metrics, lineContext, documentContext, requestedLanguageMode);
        }

        if (metrics.CyrillicSpecificCount > metrics.LatinSpecificCount
            && metrics.AmbiguousLatinCount > 0
            && ShouldFavorCyrillic(lineContext, documentContext, requestedLanguageMode))
        {
            return ConvertToCyrillic(sourceText, metrics, lineContext, documentContext, requestedLanguageMode);
        }

        if (metrics.LatinSpecificCount > metrics.CyrillicSpecificCount
            && metrics.AmbiguousCyrillicCount > 0
            && ShouldFavorLatin(lineContext, documentContext, requestedLanguageMode))
        {
            return ConvertToLatin(sourceText, metrics, lineContext, documentContext, requestedLanguageMode);
        }

        return sourceText;
    }

    private static bool LooksLikeProtectedToken(string text)
        => text.Contains("://", StringComparison.Ordinal)
           || text.Contains('@')
           || text.Contains('\\')
           || text.Contains('/')
           || text.Contains("www.", StringComparison.OrdinalIgnoreCase)
           || text.Contains(".exe", StringComparison.OrdinalIgnoreCase)
           || text.Contains("TextLayer", StringComparison.OrdinalIgnoreCase)
           || text.Contains("OCR", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Windows", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Tesseract", StringComparison.OrdinalIgnoreCase)
           || text.Contains("GitHub", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Releases", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Setup", StringComparison.OrdinalIgnoreCase)
           || (text.Length <= 4 && text.All(character => char.IsUpper(character) || char.IsDigit(character)));

    private static bool ShouldTreatZeroAsLetter(string text, int index)
    {
        var previousIsLetter = index > 0 && char.IsLetter(text[index - 1]);
        var nextIsLetter = index + 1 < text.Length && char.IsLetter(text[index + 1]);
        return previousIsLetter || nextIsLetter;
    }

    private static bool ShouldTreatCyrillicOAsLatin(string text)
        => text.Any(static character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    private static bool IsUppercaseContext(string text, int index)
    {
        if (index > 0 && char.IsUpper(text[index - 1]))
        {
            return true;
        }

        return index + 1 < text.Length && char.IsUpper(text[index + 1]);
    }

    private static WordScriptMetrics AnalyzeWord(string text)
    {
        var latinLetters = 0;
        var latinSpecificLetters = 0;
        var ambiguousLatinLetters = 0;
        var cyrillicLetters = 0;
        var cyrillicSpecificLetters = 0;
        var ambiguousCyrillicLetters = 0;
        var digits = 0;

        foreach (var character in text)
        {
            switch (ClassifyCharacter(character))
            {
                case CharacterScriptKind.LatinAmbiguous:
                    latinLetters++;
                    ambiguousLatinLetters++;
                    break;
                case CharacterScriptKind.LatinSpecific:
                    latinLetters++;
                    latinSpecificLetters++;
                    break;
                case CharacterScriptKind.CyrillicAmbiguous:
                    cyrillicLetters++;
                    ambiguousCyrillicLetters++;
                    break;
                case CharacterScriptKind.CyrillicSpecific:
                    cyrillicLetters++;
                    cyrillicSpecificLetters++;
                    break;
                case CharacterScriptKind.Digit:
                    digits++;
                    break;
            }
        }

        var letterCount = latinLetters + cyrillicLetters;
        var allLatin = latinLetters > 0 && cyrillicLetters == 0;
        var allCyrillic = cyrillicLetters > 0 && latinLetters == 0;
        var ambiguousOnlyLatin = allLatin && latinSpecificLetters == 0 && ambiguousLatinLetters > 0;
        var ambiguousOnlyCyrillic = allCyrillic && cyrillicSpecificLetters == 0 && ambiguousCyrillicLetters > 0;
        var suspiciousPseudoLatin = allLatin
            && ((letterCount >= 3
                    && ambiguousLatinLetters >= Math.Max(2, letterCount - 1)
                    && latinSpecificLetters <= 1)
                || LooksLikePseudoTransliteratedCyrillic(text, latinLetters, latinSpecificLetters, ambiguousLatinLetters, digits));
        var suspiciousPseudoCyrillic = allCyrillic
            && letterCount >= 3
            && ambiguousCyrillicLetters >= Math.Max(2, letterCount - 1)
            && cyrillicSpecificLetters <= 1;

        return new WordScriptMetrics(
            LatinLetterCount: latinLetters,
            LatinSpecificCount: latinSpecificLetters,
            AmbiguousLatinCount: ambiguousLatinLetters,
            CyrillicLetterCount: cyrillicLetters,
            CyrillicSpecificCount: cyrillicSpecificLetters,
            AmbiguousCyrillicCount: ambiguousCyrillicLetters,
            DigitCount: digits,
            IsAmbiguousOnlyLatin: ambiguousOnlyLatin,
            IsAmbiguousOnlyCyrillic: ambiguousOnlyCyrillic,
            IsSuspiciousPseudoLatin: suspiciousPseudoLatin,
            IsSuspiciousPseudoCyrillic: suspiciousPseudoCyrillic);
    }

    private static CharacterScriptKind ClassifyCharacter(char character)
    {
        if (char.IsDigit(character))
        {
            return CharacterScriptKind.Digit;
        }

        if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
        {
            return LatinToCyrillicMap.ContainsKey(character) || AggressivePseudoLatinToCyrillicMap.ContainsKey(character)
                ? CharacterScriptKind.LatinAmbiguous
                : CharacterScriptKind.LatinSpecific;
        }

        if (character is >= '\u0400' and <= '\u04FF')
        {
            return CyrillicToLatinMap.ContainsKey(character)
                ? CharacterScriptKind.CyrillicAmbiguous
                : CharacterScriptKind.CyrillicSpecific;
        }

        return char.IsPunctuation(character) || char.IsWhiteSpace(character)
            ? CharacterScriptKind.Punctuation
            : CharacterScriptKind.Other;
    }

    private static bool LooksLikePseudoTransliteratedCyrillic(
        string text,
        int latinLetters,
        int latinSpecificLetters,
        int ambiguousLatinLetters,
        int digits)
    {
        if (latinLetters < 2 || LooksLikeProtectedToken(text))
        {
            return false;
        }

        if (digits > 0
            && text.Any(static character => character is '3' or '4' or '6' or '0')
            && ambiguousLatinLetters >= Math.Max(1, latinLetters - 3))
        {
            return true;
        }

        if (latinLetters >= 5
            && ambiguousLatinLetters >= Math.Max(3, latinLetters - 2)
            && latinSpecificLetters <= 2)
        {
            return true;
        }

        var folded = new string(text
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return folded.Contains("cuct", StringComparison.Ordinal)
            || folded.Contains("cuctem", StringComparison.Ordinal)
            || folded.Contains("pabot", StringComparison.Ordinal)
            || folded.Contains("anyc", StringComparison.Ordinal)
            || folded.Contains("zapyc", StringComparison.Ordinal)
            || folded.Contains("ctbo", StringComparison.Ordinal)
            || folded.StartsWith("kak", StringComparison.Ordinal)
            || folded.Contains("3to", StringComparison.Ordinal);
    }

    private static string NormalizeWord(string? rawWord)
        => string.IsNullOrWhiteSpace(rawWord)
            ? string.Empty
            : string.Join(' ', rawWord.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static RecognizedDocument MergeAdjacentWordFragments(RecognizedDocument document)
    {
        if (document.Words.Count <= 1)
        {
            return document;
        }

        var wordsById = document.Words.ToDictionary(word => word.WordId);
        var mergedWords = new List<RecognizedWord>(document.Words.Count);
        var mergedLines = new List<RecognizedLine>(document.Lines.Count);

        foreach (var line in document.Lines.OrderBy(line => line.Index))
        {
            var lineWords = line.WordIds
                .Select(wordId => wordsById.GetValueOrDefault(wordId))
                .OfType<RecognizedWord>()
                .OrderBy(word => word.Index)
                .ToArray();

            if (lineWords.Length == 0)
            {
                continue;
            }

            var lineMergedWords = new List<RecognizedWord>();
            var current = lineWords[0];
            for (var index = 1; index < lineWords.Length; index++)
            {
                var next = lineWords[index];
                if (ShouldMergeWordFragments(current, next))
                {
                    current = MergeWords(current, next);
                    continue;
                }

                lineMergedWords.Add(current);
                current = next;
            }

            lineMergedWords.Add(current);

            var remappedWords = lineMergedWords
                .Select((word, index) => word with
                {
                    Index = mergedWords.Count + index,
                    LineIndex = mergedLines.Count,
                })
                .ToArray();

            mergedWords.AddRange(remappedWords);
            mergedLines.Add(new RecognizedLine(
                Guid.NewGuid(),
                mergedLines.Count,
                string.Join(' ', remappedWords.Select(word => word.Text)),
                BuildLineRect(remappedWords),
                null,
                remappedWords.Select(word => word.WordId).ToArray()));
        }

        return document with
        {
            Lines = mergedLines,
            Words = mergedWords,
            FullText = mergedLines.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, mergedLines.Select(line => line.Text)),
        };
    }

    private static bool ShouldMergeWordFragments(RecognizedWord left, RecognizedWord right)
    {
        if (!IsMergeableToken(left.NormalizedText) || !IsMergeableToken(right.NormalizedText))
        {
            return false;
        }

        var gap = right.BoundingRect.Left - left.BoundingRect.Right;
        var height = Math.Max(left.BoundingRect.Height, right.BoundingRect.Height);
        var maxGap = Math.Clamp(height * 0.16d, 0.9d, 3.2d);
        if (gap > maxGap)
        {
            return false;
        }

        var verticalOverlap = Math.Min(left.BoundingRect.Bottom, right.BoundingRect.Bottom)
            - Math.Max(left.BoundingRect.Top, right.BoundingRect.Top);
        if (verticalOverlap < Math.Min(left.BoundingRect.Height, right.BoundingRect.Height) * 0.55d)
        {
            return false;
        }

        var leftMetrics = AnalyzeWord(left.NormalizedText);
        var rightMetrics = AnalyzeWord(right.NormalizedText);
        var hasStandaloneShortWord =
            IsStandaloneShortWord(left.NormalizedText, leftMetrics)
            || IsStandaloneShortWord(right.NormalizedText, rightMetrics);
        if (hasStandaloneShortWord && gap > Math.Clamp(height * 0.06d, 0.45d, 1.15d))
        {
            return false;
        }

        var strongMergeHint =
            left.NormalizedText.Length <= 3
            || right.NormalizedText.Length <= 3
            || leftMetrics.MixedScript
            || rightMetrics.MixedScript
            || leftMetrics.IsSuspiciousPseudoLatin
            || leftMetrics.IsSuspiciousPseudoCyrillic
            || rightMetrics.IsSuspiciousPseudoLatin
            || rightMetrics.IsSuspiciousPseudoCyrillic;
        var tightlyConnectedShortFragments =
            gap <= Math.Clamp(height * 0.42d, 2d, 7d)
            && verticalOverlap >= Math.Min(left.BoundingRect.Height, right.BoundingRect.Height) * 0.68d
            && (left.NormalizedText.Length <= 4 || right.NormalizedText.Length <= 4);

        return AreCompatibleScripts(leftMetrics, rightMetrics)
            && (gap <= 1.2d || strongMergeHint || tightlyConnectedShortFragments);
    }

    private static bool AreCompatibleScripts(WordScriptMetrics leftMetrics, WordScriptMetrics rightMetrics)
    {
        var leftScript = GetTokenScript(leftMetrics);
        var rightScript = GetTokenScript(rightMetrics);
        return leftScript == ScriptKind.Unknown
            || rightScript == ScriptKind.Unknown
            || leftScript == rightScript;
    }

    private static ScriptKind GetTokenScript(WordScriptMetrics metrics)
    {
        if (metrics.CyrillicLetterCount > metrics.LatinLetterCount)
        {
            return ScriptKind.Cyrillic;
        }

        if (metrics.LatinLetterCount > metrics.CyrillicLetterCount)
        {
            return ScriptKind.Latin;
        }

        return ScriptKind.Unknown;
    }

    private static bool IsMergeableToken(string text)
        => !string.IsNullOrWhiteSpace(text)
           && !LooksLikeProtectedToken(text)
           && text.Any(static character => char.IsLetterOrDigit(character));

    private static bool IsStandaloneShortWord(string text, WordScriptMetrics metrics)
        => text.Length <= 2
           && !metrics.MixedScript
           && !metrics.IsSuspiciousPseudoLatin
           && !metrics.IsSuspiciousPseudoCyrillic
           && text.All(static character => char.IsLetterOrDigit(character));

    private static RecognizedWord MergeWords(RecognizedWord left, RecognizedWord right)
    {
        var mergedText = left.Text + right.Text;
        return left with
        {
            Text = mergedText,
            NormalizedText = NormalizeWord(mergedText),
            BoundingRect = Union(left.BoundingRect, right.BoundingRect),
            Confidence = AverageConfidence(left.Confidence, right.Confidence),
        };
    }

    private static RectD Union(RectD left, RectD right)
    {
        var minLeft = Math.Min(left.Left, right.Left);
        var minTop = Math.Min(left.Top, right.Top);
        var maxRight = Math.Max(left.Right, right.Right);
        var maxBottom = Math.Max(left.Bottom, right.Bottom);
        return new RectD(minLeft, minTop, maxRight - minLeft, maxBottom - minTop);
    }

    private static double? AverageConfidence(double? first, double? second)
        => first.HasValue && second.HasValue
            ? (first.Value + second.Value) / 2d
            : first ?? second;

    private static RectD BuildLineRect(IReadOnlyList<RecognizedWord> lineWords)
    {
        var left = lineWords.Min(word => word.BoundingRect.Left);
        var top = lineWords.Min(word => word.BoundingRect.Top);
        var right = lineWords.Max(word => word.BoundingRect.Right);
        var bottom = lineWords.Max(word => word.BoundingRect.Bottom);
        return new RectD(left, top, right - left, bottom - top);
    }

    private readonly record struct ScriptContext(double LatinWeight, double CyrillicWeight);

    private readonly record struct CorrectedDocumentResult(RecognizedDocument Document, int CorrectedWordCount);

    private sealed record ProtectedToken(string Canonical, string Folded, int MaxEditDistance);

    private readonly record struct WordScriptMetrics(
        int LatinLetterCount,
        int LatinSpecificCount,
        int AmbiguousLatinCount,
        int CyrillicLetterCount,
        int CyrillicSpecificCount,
        int AmbiguousCyrillicCount,
        int DigitCount,
        bool IsAmbiguousOnlyLatin,
        bool IsAmbiguousOnlyCyrillic,
        bool IsSuspiciousPseudoLatin,
        bool IsSuspiciousPseudoCyrillic)
    {
        public bool HasLetters => LatinLetterCount + CyrillicLetterCount > 0;

        public bool AllLatin => LatinLetterCount > 0 && CyrillicLetterCount == 0;

        public bool AllCyrillic => CyrillicLetterCount > 0 && LatinLetterCount == 0;

        public bool MixedScript => LatinLetterCount > 0 && CyrillicLetterCount > 0;
    }

    private enum CharacterScriptKind
    {
        Other = 0,
        LatinSpecific = 1,
        LatinAmbiguous = 2,
        CyrillicSpecific = 3,
        CyrillicAmbiguous = 4,
        Digit = 5,
        Punctuation = 6,
    }

    private enum ScriptKind
    {
        Unknown = 0,
        Latin = 1,
        Cyrillic = 2,
    }
}

public sealed record ScriptAwareOcrProcessingResult(
    RecognizedDocument Document,
    ScriptAwareOcrAnalysis Analysis);

public sealed record ScriptAwareOcrAnalysis(
    ScriptDominance DominantScript,
    int LatinCharacterCount,
    int CyrillicCharacterCount,
    int MixedScriptWordCount,
    int SuspiciousPseudoLatinWordCount,
    int SuspiciousPseudoCyrillicWordCount,
    int AmbiguousWordCount,
    int CorrectedWordCount,
    bool LikelyMixedText)
{
    public bool HasLikelyTransliterationGarbage
        => SuspiciousPseudoLatinWordCount >= 2
           || SuspiciousPseudoCyrillicWordCount >= 2
           || MixedScriptWordCount >= 2;
}

public enum ScriptDominance
{
    Unknown = 0,
    Latin = 1,
    Cyrillic = 2,
    Mixed = 3,
}

public sealed class ScriptAwareOcrAnalyzer
{
    public ScriptAwareOcrAnalysis Analyze(RecognizedDocument document, OcrLanguageMode requestedLanguageMode)
    {
        var latinCharacters = 0;
        var cyrillicCharacters = 0;
        var latinWords = 0;
        var cyrillicWords = 0;
        var mixedScriptWords = 0;
        var suspiciousPseudoLatinWords = 0;
        var suspiciousPseudoCyrillicWords = 0;
        var ambiguousWords = 0;

        foreach (var word in document.Words)
        {
            var hasLatin = false;
            var hasCyrillic = false;
            var latinCount = 0;
            var cyrillicCount = 0;
            var ambiguousCount = 0;
            var specificLatinCount = 0;
            var specificCyrillicCount = 0;

            foreach (var character in word.NormalizedText)
            {
                if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                {
                    hasLatin = true;
                    latinCount++;
                    latinCharacters++;
                    if (ScriptAwareOcrPostProcessorLatinMaps.ContainsLatinLookalike(character))
                    {
                        ambiguousCount++;
                    }
                    else
                    {
                        specificLatinCount++;
                    }

                    continue;
                }

                if (character is >= '\u0400' and <= '\u04FF')
                {
                    hasCyrillic = true;
                    cyrillicCount++;
                    cyrillicCharacters++;
                    if (ScriptAwareOcrPostProcessorLatinMaps.ContainsCyrillicLookalike(character))
                    {
                        ambiguousCount++;
                    }
                    else
                    {
                        specificCyrillicCount++;
                    }
                }
            }

            if (hasLatin && hasCyrillic)
            {
                mixedScriptWords++;
            }
            else if (hasLatin)
            {
                latinWords++;
            }
            else if (hasCyrillic)
            {
                cyrillicWords++;
            }

            if ((hasLatin || hasCyrillic) && ambiguousCount >= Math.Max(2, latinCount + cyrillicCount - 1))
            {
                ambiguousWords++;
            }

            if (hasLatin
                && !hasCyrillic
                && IsSuspiciousPseudoLatinWord(word.NormalizedText, latinCount, specificLatinCount, ambiguousCount))
            {
                suspiciousPseudoLatinWords++;
            }

            if (hasCyrillic
                && !hasLatin
                && cyrillicCount >= 3
                && ambiguousCount >= Math.Max(2, cyrillicCount - 1)
                && specificCyrillicCount <= 1)
            {
                suspiciousPseudoCyrillicWords++;
            }
        }

        var dominantScript = DetermineDominantScript(latinCharacters, cyrillicCharacters);
        var likelyMixedText = dominantScript == ScriptDominance.Mixed
            || (latinCharacters >= 6 && cyrillicCharacters >= 6)
            || (latinWords >= 2 && cyrillicWords >= 2)
            || (mixedScriptWords >= 1 && latinCharacters >= 3 && cyrillicCharacters >= 3);

        return new ScriptAwareOcrAnalysis(
            dominantScript,
            latinCharacters,
            cyrillicCharacters,
            mixedScriptWords,
            suspiciousPseudoLatinWords,
            suspiciousPseudoCyrillicWords,
            ambiguousWords,
            0,
            likelyMixedText);
    }

    private static bool IsSuspiciousPseudoLatinWord(
        string text,
        int latinCount,
        int specificLatinCount,
        int ambiguousCount)
    {
        if (LooksLikeProtectedToken(text) || latinCount < 2)
        {
            return false;
        }

        if (latinCount >= 3
            && ambiguousCount >= Math.Max(2, latinCount - 1)
            && specificLatinCount <= 1)
        {
            return true;
        }

        if (text.Any(static character => character is '3' or '4' or '6' or '0')
            && latinCount >= 2
            && ambiguousCount >= Math.Max(1, latinCount - 3))
        {
            return true;
        }

        var folded = new string(text
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return folded.Length >= 5
            && (folded.Contains("cuct", StringComparison.Ordinal)
                || folded.Contains("pabot", StringComparison.Ordinal)
                || folded.Contains("anyc", StringComparison.Ordinal)
                || folded.Contains("zapyc", StringComparison.Ordinal)
                || folded.Contains("ctbo", StringComparison.Ordinal));
    }

    private static bool LooksLikeProtectedToken(string text)
        => text.Contains("://", StringComparison.Ordinal)
           || text.Contains('@')
           || text.Contains('\\')
           || text.Contains('/')
           || text.Contains("www.", StringComparison.OrdinalIgnoreCase)
           || text.Contains("TextLayer", StringComparison.OrdinalIgnoreCase)
           || text.Contains("OCR", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Windows", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Tesseract", StringComparison.OrdinalIgnoreCase)
           || text.Contains("GitHub", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Releases", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Download", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Setup", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)
           || text.Contains(".exe", StringComparison.OrdinalIgnoreCase)
           || string.Equals(text, "Esc", StringComparison.OrdinalIgnoreCase);

    private static ScriptDominance DetermineDominantScript(int latinCharacters, int cyrillicCharacters)
    {
        if (latinCharacters == 0 && cyrillicCharacters == 0)
        {
            return ScriptDominance.Unknown;
        }

        if (latinCharacters >= 8 && cyrillicCharacters >= 8)
        {
            return ScriptDominance.Mixed;
        }

        if (cyrillicCharacters >= latinCharacters * 1.6d)
        {
            return ScriptDominance.Cyrillic;
        }

        if (latinCharacters >= cyrillicCharacters * 1.6d)
        {
            return ScriptDominance.Latin;
        }

        return ScriptDominance.Mixed;
    }
}

internal static class ScriptAwareOcrPostProcessorLatinMaps
{
    private static readonly HashSet<char> LatinLookalikes =
    [
        'A', 'a', 'B', 'b', 'C', 'c', 'E', 'e', 'H', 'h', 'K', 'k', 'M', 'm', 'O', 'o', 'P', 'p', 'T', 't', 'V', 'v', 'X', 'x', 'Y', 'y', 'N', 'n'
    ];

    private static readonly HashSet<char> CyrillicLookalikes =
    [
        '\u0410', '\u0430', '\u0412', '\u0432', '\u0415', '\u0435', '\u041A', '\u043A', '\u041C', '\u043C', '\u041D', '\u043D', '\u041E', '\u043E', '\u0420', '\u0440', '\u0421', '\u0441', '\u0422', '\u0442', '\u0425', '\u0445', '\u0423', '\u0443'
    ];

    public static bool ContainsLatinLookalike(char character) => LatinLookalikes.Contains(character);

    public static bool ContainsCyrillicLookalike(char character) => CyrillicLookalikes.Contains(character);
}
