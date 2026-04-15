using System.Globalization;
using System.Text;
using TextLayer.Domain.Geometry;
using TextLayer.Domain.Models;

namespace TextLayer.Infrastructure.Ocr;

public sealed class RecognizedDocumentNoiseFilter
{
    public RecognizedDocument Filter(
        RecognizedDocument document,
        NoiseFilterProfile profile = NoiseFilterProfile.Standard)
    {
        if (document.Words.Count == 0)
        {
            return document;
        }

        var wordsById = document.Words.ToDictionary(word => word.WordId);
        var filteredLines = new List<RecognizedLine>();
        var filteredWords = new List<RecognizedWord>();

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

            var keptWords = lineWords
                .Where(word => !ShouldRejectWord(word, lineWords, document, profile))
                .ToArray();

            if (keptWords.Length == 0)
            {
                keptWords = lineWords.Where(LooksTextual).ToArray();
                if (keptWords.Length == 0)
                {
                    continue;
                }
            }

            var lineIndex = filteredLines.Count;
            var remappedWords = keptWords
                .Select((word, index) => word with
                {
                    Index = filteredWords.Count + index,
                    LineIndex = lineIndex,
                })
                .ToArray();

            filteredWords.AddRange(remappedWords);
            filteredLines.Add(new RecognizedLine(
                Guid.NewGuid(),
                lineIndex,
                string.Join(' ', remappedWords.Select(word => word.Text)),
                BuildLineRect(remappedWords),
                null,
                remappedWords.Select(word => word.WordId).ToArray()));
        }

        if (filteredWords.Count == document.Words.Count && filteredLines.Count == document.Lines.Count)
        {
            return document;
        }

        return document with
        {
            Lines = filteredLines,
            Words = filteredWords,
            FullText = filteredLines.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, filteredLines.Select(line => line.Text)),
        };
    }

    private static bool ShouldRejectWord(
        RecognizedWord word,
        IReadOnlyList<RecognizedWord> lineWords,
        RecognizedDocument document,
        NoiseFilterProfile profile)
    {
        var metrics = AnalyzeToken(word.NormalizedText);
        if (metrics.RuneCount == 0)
        {
            return true;
        }

        if (LooksTextual(word))
        {
            return false;
        }

        var rect = word.BoundingRect;
        var maxSquareSize = Math.Clamp(Math.Min(document.ImagePixelWidth, document.ImagePixelHeight) * 0.045d, 16d, 42d);
        var aspectRatio = rect.Width / Math.Max(rect.Height, 1d);
        var looksSquare = aspectRatio is >= 0.55d and <= 1.8d;
        var looksLikeSmallIcon = looksSquare && rect.Width <= maxSquareSize && rect.Height <= maxSquareSize;
        var symbolOnly = metrics.LetterOrDigitCount == 0 && metrics.SymbolOrEmojiCount > 0;
        var repeatedNonTextGlyph = metrics.DistinctRuneCount == 1 && metrics.LetterOrDigitCount == 0;
        var isolatedLine = lineWords.Count == 1;
        var connectedToTextNeighbor = lineWords.Any(candidate =>
            candidate.WordId != word.WordId
            && LooksTextual(candidate)
            && IsConnectedTextFragment(word, candidate));

        if (connectedToTextNeighbor)
        {
            return false;
        }

        if (profile == NoiseFilterProfile.MaximumCoverage)
        {
            if (metrics.HasEmojiOrPictograph)
            {
                return looksLikeSmallIcon || isolatedLine || metrics.RuneCount <= 4;
            }

            return symbolOnly && looksLikeSmallIcon && isolatedLine;
        }

        if (metrics.HasEmojiOrPictograph)
        {
            return true;
        }

        if (symbolOnly)
        {
            return looksLikeSmallIcon || isolatedLine || metrics.RuneCount <= 4 || repeatedNonTextGlyph;
        }

        return repeatedNonTextGlyph && looksLikeSmallIcon;
    }

    private static bool IsConnectedTextFragment(RecognizedWord word, RecognizedWord neighbor)
    {
        var horizontalGap = Math.Max(0d, Math.Max(word.BoundingRect.Left, neighbor.BoundingRect.Left) - Math.Min(word.BoundingRect.Right, neighbor.BoundingRect.Right));
        var minHeight = Math.Min(word.BoundingRect.Height, neighbor.BoundingRect.Height);
        var verticalOverlap = Math.Min(word.BoundingRect.Bottom, neighbor.BoundingRect.Bottom)
            - Math.Max(word.BoundingRect.Top, neighbor.BoundingRect.Top);

        return horizontalGap <= Math.Clamp(minHeight * 0.42d, 2d, 7d)
            && verticalOverlap >= minHeight * 0.45d;
    }

    private static bool LooksTextual(RecognizedWord word)
    {
        var metrics = AnalyzeToken(word.NormalizedText);
        return metrics.LetterOrDigitCount >= 1
            || IsMeaningfulMixedToken(word.NormalizedText);
    }

    private static bool IsMeaningfulMixedToken(string text)
        => text.Contains('#', StringComparison.Ordinal)
           || text.Contains('@', StringComparison.Ordinal)
           || text.Contains('.', StringComparison.Ordinal)
           || text.Contains('-', StringComparison.Ordinal)
           || text.Contains('_', StringComparison.Ordinal)
           || text.Contains('+', StringComparison.Ordinal);

    private static TokenMetrics AnalyzeToken(string text)
    {
        var runeCount = 0;
        var letterCount = 0;
        var digitCount = 0;
        var symbolOrEmojiCount = 0;
        var distinctRunes = new HashSet<int>();

        foreach (var rune in text.EnumerateRunes())
        {
            runeCount++;
            distinctRunes.Add(rune.Value);

            if (Rune.IsLetter(rune))
            {
                letterCount++;
                continue;
            }

            if (Rune.IsDigit(rune))
            {
                digitCount++;
                continue;
            }

            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.OtherSymbol
                or UnicodeCategory.MathSymbol
                or UnicodeCategory.CurrencySymbol
                or UnicodeCategory.ModifierSymbol
                or UnicodeCategory.Surrogate)
            {
                symbolOrEmojiCount++;
            }

            if (rune.Value is >= 0x1F000 and <= 0x1FAFF
                or >= 0x2600 and <= 0x27BF
                or >= 0xFE00 and <= 0xFE0F)
            {
                symbolOrEmojiCount++;
            }
        }

        return new TokenMetrics(
            RuneCount: runeCount,
            LetterCount: letterCount,
            DigitCount: digitCount,
            SymbolOrEmojiCount: symbolOrEmojiCount,
            DistinctRuneCount: distinctRunes.Count);
    }

    private static RectD BuildLineRect(IReadOnlyList<RecognizedWord> lineWords)
    {
        var left = lineWords.Min(word => word.BoundingRect.Left);
        var top = lineWords.Min(word => word.BoundingRect.Top);
        var right = lineWords.Max(word => word.BoundingRect.Right);
        var bottom = lineWords.Max(word => word.BoundingRect.Bottom);
        return new RectD(left, top, right - left, bottom - top);
    }

    private readonly record struct TokenMetrics(
        int RuneCount,
        int LetterCount,
        int DigitCount,
        int SymbolOrEmojiCount,
        int DistinctRuneCount)
    {
        public int LetterOrDigitCount => LetterCount + DigitCount;

        public bool HasEmojiOrPictograph => SymbolOrEmojiCount > 0;
    }

    public enum NoiseFilterProfile
    {
        Standard = 0,
        MaximumCoverage = 1,
    }
}
