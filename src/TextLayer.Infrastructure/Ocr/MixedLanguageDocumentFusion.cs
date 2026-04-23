using TextLayer.Application.Models;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;

namespace TextLayer.Infrastructure.Ocr;

public sealed class MixedLanguageDocumentFusion
{
    private static readonly string[] ProtectedTokens =
    [
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
    ];

    public RecognizedDocument Fuse(
        IReadOnlyList<MixedLanguageRecognitionBranch> branches,
        string engineId,
        long recognitionDurationMs)
    {
        var nonEmptyBranches = branches
            .Where(branch => branch.Document.Words.Count > 0)
            .ToArray();

        if (nonEmptyBranches.Length == 0)
        {
            var template = branches.FirstOrDefault()?.Document;
            return template is null
                ? throw new InvalidOperationException("TextLayer expected at least one mixed OCR branch.")
                : template with
                {
                    FullText = string.Empty,
                    Lines = [],
                    Words = [],
                    OcrEngineId = engineId,
                    RecognitionDurationMs = recognitionDurationMs,
                    LanguageHint = "eng+rus fused",
                };
        }

        var wordCandidates = nonEmptyBranches
            .SelectMany((branch, branchIndex) => branch.Document.Words
                .Where(word => !string.IsNullOrWhiteSpace(word.NormalizedText))
                .Select(word => new FusionWordCandidate(word, branch.LanguageMode, branchIndex)))
            .OrderBy(candidate => candidate.Word.BoundingRect.Top)
            .ThenBy(candidate => candidate.Word.BoundingRect.Left)
            .ToArray();

        var groups = BuildOverlapGroups(wordCandidates);
        var selectedWords = groups
            .Select(ChooseBestWord)
            .Where(word => !string.IsNullOrWhiteSpace(word.NormalizedText))
            .ToArray();

        return RebuildDocument(
            nonEmptyBranches[0].Document,
            selectedWords,
            engineId,
            recognitionDurationMs);
    }

    private static List<List<FusionWordCandidate>> BuildOverlapGroups(IReadOnlyList<FusionWordCandidate> candidates)
    {
        var groups = new List<List<FusionWordCandidate>>();
        foreach (var candidate in candidates)
        {
            var targetGroup = groups
                .Where(group => group.Any(existing => RepresentsSameVisibleWord(existing.Word, candidate.Word)))
                .OrderByDescending(group => group.Max(existing => GetOverlapRatio(existing.Word.BoundingRect, candidate.Word.BoundingRect)))
                .FirstOrDefault();

            if (targetGroup is null)
            {
                groups.Add([candidate]);
                continue;
            }

            targetGroup.Add(candidate);
        }

        return groups;
    }

    private static RecognizedWord ChooseBestWord(IReadOnlyList<FusionWordCandidate> candidates)
    {
        var groupHasCyrillic = candidates.Any(candidate => AnalyzeToken(candidate.Word.NormalizedText).CyrillicCount > 0);
        var groupHasProtectedToken = candidates.Any(candidate => LooksLikeProtectedToken(candidate.Word.NormalizedText));

        return candidates
            .OrderByDescending(candidate => GetWordPreferenceScore(candidate, groupHasCyrillic, groupHasProtectedToken))
            .ThenBy(candidate => candidate.BranchIndex)
            .First()
            .Word;
    }

    private static double GetWordPreferenceScore(
        FusionWordCandidate candidate,
        bool groupHasCyrillic,
        bool groupHasProtectedToken)
    {
        var word = candidate.Word;
        var metrics = AnalyzeToken(word.NormalizedText);
        var symbolPenalty = word.NormalizedText.Count(static character =>
            !char.IsWhiteSpace(character)
            && !char.IsLetterOrDigit(character)
            && character is not '.' and not ',' and not ':' and not ';' and not '!' and not '?'
            && character is not '-' and not '_' and not '+' and not '/' and not '\\');

        var isProtectedToken = LooksLikeProtectedToken(word.NormalizedText);
        var isPseudoLatin = LooksLikePseudoTransliteratedCyrillic(word.NormalizedText, metrics);
        var isPseudoCyrillic = LooksLikePseudoCyrillicTechnicalToken(word.NormalizedText, metrics);

        var score = (metrics.LetterOrDigitCount * 5.2d)
            + (word.NormalizedText.Length * 1.2d)
            + ((word.Confidence ?? 70d) * 0.14d)
            - (symbolPenalty * 2.2d);

        if (isProtectedToken)
        {
            score += 95d;
        }

        if (metrics.CyrillicCount > 0)
        {
            score += candidate.LanguageMode == OcrLanguageMode.Russian ? 52d : 22d;
            score += metrics.CyrillicSpecificCount > 0 ? 18d : 8d;
        }

        if (metrics.LatinCount > 0)
        {
            score += candidate.LanguageMode == OcrLanguageMode.English ? 38d : 14d;
            score += metrics.LatinSpecificCount > 0 ? 18d : 0d;
        }

        if (candidate.LanguageMode == OcrLanguageMode.EnglishRussian)
        {
            score += metrics.HasLatin && metrics.HasCyrillic ? 18d : 5d;
        }

        if (metrics.HasLatin && metrics.HasCyrillic && !isProtectedToken)
        {
            score -= 28d;
        }

        if (isPseudoLatin)
        {
            score -= groupHasCyrillic ? 165d : 82d;
        }

        if (isPseudoCyrillic)
        {
            score -= groupHasProtectedToken ? 105d : 36d;
        }

        if (candidate.LanguageMode == OcrLanguageMode.English
            && metrics.LatinSpecificCount == 0
            && metrics.LatinCount >= 3
            && groupHasCyrillic
            && !isProtectedToken)
        {
            score -= 70d;
        }

        if (candidate.LanguageMode == OcrLanguageMode.English
            && groupHasCyrillic
            && !isProtectedToken
            && metrics.LatinSpecificCount < 4)
        {
            score -= 48d;
        }

        return score;
    }

    private static bool RepresentsSameVisibleWord(RecognizedWord left, RecognizedWord right)
    {
        var overlapRatio = GetOverlapRatio(left.BoundingRect, right.BoundingRect);
        if (overlapRatio >= 0.32d)
        {
            return true;
        }

        var sameText = LooksLikeSameToken(left.NormalizedText, right.NormalizedText);
        if (sameText && overlapRatio >= 0.14d)
        {
            return true;
        }

        var leftCenterX = left.BoundingRect.Left + (left.BoundingRect.Width / 2d);
        var rightCenterX = right.BoundingRect.Left + (right.BoundingRect.Width / 2d);
        var leftCenterY = left.BoundingRect.Top + (left.BoundingRect.Height / 2d);
        var rightCenterY = right.BoundingRect.Top + (right.BoundingRect.Height / 2d);

        return sameText
            && Math.Abs(leftCenterX - rightCenterX) <= Math.Clamp(Math.Max(left.BoundingRect.Width, right.BoundingRect.Width) * 0.6d, 4d, 28d)
            && Math.Abs(leftCenterY - rightCenterY) <= Math.Clamp(Math.Max(left.BoundingRect.Height, right.BoundingRect.Height) * 0.58d, 3d, 16d);
    }

    private static double GetOverlapRatio(RectD left, RectD right)
    {
        var horizontalOverlap = Math.Max(0d, Math.Min(left.Right, right.Right) - Math.Max(left.Left, right.Left));
        var verticalOverlap = Math.Max(0d, Math.Min(left.Bottom, right.Bottom) - Math.Max(left.Top, right.Top));
        var overlapArea = horizontalOverlap * verticalOverlap;
        var minArea = Math.Max(1d, Math.Min(left.Width * left.Height, right.Width * right.Height));
        return overlapArea / minArea;
    }

    private static bool LooksLikeSameToken(string left, string right)
    {
        var normalizedLeft = NormalizeForComparison(left);
        var normalizedRight = NormalizeForComparison(right);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return false;
        }

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var minLength = Math.Min(normalizedLeft.Length, normalizedRight.Length);
        var maxLength = Math.Max(normalizedLeft.Length, normalizedRight.Length);
        return minLength >= 4
            && maxLength - minLength <= 2
            && (normalizedLeft.Contains(normalizedRight, StringComparison.OrdinalIgnoreCase)
                || normalizedRight.Contains(normalizedLeft, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeForComparison(string text)
        => new(text
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static RecognizedDocument RebuildDocument(
        RecognizedDocument template,
        IReadOnlyList<RecognizedWord> sourceWords,
        string engineId,
        long recognitionDurationMs)
    {
        if (sourceWords.Count == 0)
        {
            return template with
            {
                FullText = string.Empty,
                Lines = [],
                Words = [],
                OcrEngineId = engineId,
                RecognitionDurationMs = recognitionDurationMs,
                LanguageHint = "eng+rus fused",
            };
        }

        var lineBuckets = new List<List<RecognizedWord>>();
        foreach (var word in sourceWords
                     .OrderBy(candidate => candidate.BoundingRect.Top)
                     .ThenBy(candidate => candidate.BoundingRect.Left))
        {
            var assignedLine = lineBuckets.FirstOrDefault(line => BelongsToLine(word, line));
            if (assignedLine is null)
            {
                assignedLine = [];
                lineBuckets.Add(assignedLine);
            }

            assignedLine.Add(word);
        }

        var rebuiltWords = new List<RecognizedWord>(sourceWords.Count);
        var rebuiltLines = new List<RecognizedLine>(lineBuckets.Count);

        foreach (var line in lineBuckets.OrderBy(candidate => candidate.Min(word => word.BoundingRect.Top)))
        {
            var orderedWords = line.OrderBy(word => word.BoundingRect.Left).ToArray();
            var lineIndex = rebuiltLines.Count;
            var remappedWords = orderedWords
                .Select((word, index) => word with
                {
                    Index = rebuiltWords.Count + index,
                    LineIndex = lineIndex,
                })
                .ToArray();

            rebuiltWords.AddRange(remappedWords);
            rebuiltLines.Add(new RecognizedLine(
                Guid.NewGuid(),
                lineIndex,
                string.Join(' ', remappedWords.Select(word => word.Text)),
                BuildLineRect(remappedWords),
                null,
                remappedWords.Select(word => word.WordId).ToArray()));
        }

        return template with
        {
            FullText = string.Join(Environment.NewLine, rebuiltLines.Select(line => line.Text)),
            Lines = rebuiltLines,
            Words = rebuiltWords,
            OcrEngineId = engineId,
            RecognitionDurationMs = recognitionDurationMs,
            LanguageHint = "eng+rus fused",
        };
    }

    private static bool BelongsToLine(RecognizedWord word, IReadOnlyList<RecognizedWord> lineWords)
    {
        if (lineWords.Count == 0)
        {
            return true;
        }

        var lineTop = lineWords.Min(candidate => candidate.BoundingRect.Top);
        var lineBottom = lineWords.Max(candidate => candidate.BoundingRect.Bottom);
        var lineCenterY = lineTop + ((lineBottom - lineTop) / 2d);
        var wordCenterY = word.BoundingRect.Top + (word.BoundingRect.Height / 2d);
        var minHeight = Math.Min(word.BoundingRect.Height, lineBottom - lineTop);
        var verticalOverlap = Math.Max(0d, Math.Min(word.BoundingRect.Bottom, lineBottom) - Math.Max(word.BoundingRect.Top, lineTop));

        return Math.Abs(wordCenterY - lineCenterY) <= Math.Clamp(Math.Max(word.BoundingRect.Height, lineBottom - lineTop) * 0.55d, 4d, 18d)
            || verticalOverlap >= minHeight * 0.34d;
    }

    private static RectD BuildLineRect(IReadOnlyList<RecognizedWord> lineWords)
    {
        var left = lineWords.Min(word => word.BoundingRect.Left);
        var top = lineWords.Min(word => word.BoundingRect.Top);
        var right = lineWords.Max(word => word.BoundingRect.Right);
        var bottom = lineWords.Max(word => word.BoundingRect.Bottom);
        return new RectD(left, top, right - left, bottom - top);
    }

    private static bool LooksLikeProtectedToken(string text)
    {
        if (text.Contains("://", StringComparison.Ordinal)
            || text.Contains('@')
            || text.Contains('\\')
            || text.Contains('/')
            || text.Contains("www.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ProtectedTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikePseudoTransliteratedCyrillic(string text, TokenMetrics metrics)
    {
        if (!metrics.HasLatin || metrics.HasCyrillic || LooksLikeProtectedToken(text))
        {
            return false;
        }

        if (metrics.LatinCount >= 3
            && metrics.AmbiguousLatinCount >= Math.Max(2, metrics.LatinCount - 1)
            && metrics.LatinSpecificCount <= 1)
        {
            return true;
        }

        if (metrics.DigitCount > 0
            && metrics.LatinCount >= 2
            && text.Any(static character => character is '3' or '4' or '6' or '0'))
        {
            return true;
        }

        var folded = NormalizeForComparison(text).ToLowerInvariant();
        return folded.Length >= 5
            && (folded.Contains("cuct", StringComparison.Ordinal)
                || folded.Contains("pabot", StringComparison.Ordinal)
                || folded.Contains("anyc", StringComparison.Ordinal)
                || folded.Contains("zapyc", StringComparison.Ordinal)
                || folded.Contains("ctbo", StringComparison.Ordinal));
    }

    private static bool LooksLikePseudoCyrillicTechnicalToken(string text, TokenMetrics metrics)
        => metrics.HasCyrillic
           && !metrics.HasLatin
           && metrics.CyrillicCount >= 3
           && metrics.AmbiguousCyrillicCount >= Math.Max(2, metrics.CyrillicCount - 1)
           && metrics.CyrillicSpecificCount <= 1
           && !LooksLikeProtectedToken(text);

    private static TokenMetrics AnalyzeToken(string text)
    {
        var latinCount = 0;
        var latinSpecificCount = 0;
        var ambiguousLatinCount = 0;
        var cyrillicCount = 0;
        var cyrillicSpecificCount = 0;
        var ambiguousCyrillicCount = 0;
        var digitCount = 0;

        foreach (var character in text)
        {
            if (char.IsDigit(character))
            {
                digitCount++;
                continue;
            }

            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                latinCount++;
                if (IsLatinLookalike(character))
                {
                    ambiguousLatinCount++;
                }
                else
                {
                    latinSpecificCount++;
                }

                continue;
            }

            if (character is >= '\u0400' and <= '\u04FF')
            {
                cyrillicCount++;
                if (ScriptAwareOcrPostProcessorLatinMaps.ContainsCyrillicLookalike(character))
                {
                    ambiguousCyrillicCount++;
                }
                else
                {
                    cyrillicSpecificCount++;
                }
            }
        }

        return new TokenMetrics(
            latinCount,
            latinSpecificCount,
            ambiguousLatinCount,
            cyrillicCount,
            cyrillicSpecificCount,
            ambiguousCyrillicCount,
            digitCount);
    }

    private static bool IsLatinLookalike(char character)
        => character is 'A' or 'a'
            or 'B' or 'b'
            or 'C' or 'c'
            or 'E' or 'e'
            or 'H' or 'h'
            or 'K' or 'k'
            or 'M' or 'm'
            or 'N' or 'n'
            or 'O' or 'o'
            or 'P' or 'p'
            or 'T' or 't'
            or 'V' or 'v'
            or 'X' or 'x'
            or 'Y' or 'y';

    private sealed record FusionWordCandidate(
        RecognizedWord Word,
        OcrLanguageMode LanguageMode,
        int BranchIndex);

    private readonly record struct TokenMetrics(
        int LatinCount,
        int LatinSpecificCount,
        int AmbiguousLatinCount,
        int CyrillicCount,
        int CyrillicSpecificCount,
        int AmbiguousCyrillicCount,
        int DigitCount)
    {
        public bool HasLatin => LatinCount > 0;

        public bool HasCyrillic => CyrillicCount > 0;

        public int LetterOrDigitCount => LatinCount + CyrillicCount + DigitCount;
    }
}

public sealed record MixedLanguageRecognitionBranch(
    RecognizedDocument Document,
    OcrLanguageMode LanguageMode);
