using System.Text;
using TextLayer.Domain.Models;

namespace TextLayer.Domain.Services;

public sealed class TextNormalizer
{
    private static readonly char[] LeadingNoSpaceCharacters = [',', '.', ';', ':', '!', '?', ')', ']', '}', '%', '+'];
    private static readonly char[] TrailingNoSpaceCharacters = ['(', '[', '{', '/', '\\', '$', '#', '+'];

    public string NormalizeDocument(RecognizedDocument document)
        => BuildReadableText(BuildExportLines(document));

    public string NormalizeSelection(IReadOnlyList<RecognizedWord> words)
        => BuildReadableText(BuildExportLines(words));

    private static IReadOnlyList<ExportLine> BuildExportLines(RecognizedDocument document)
    {
        if (document.Words.Count == 0)
        {
            return [];
        }

        var wordsById = document.Words.ToDictionary(word => word.WordId);
        var lines = new List<ExportLine>();

        foreach (var line in document.Lines.OrderBy(line => line.Index))
        {
            var lineWords = line.WordIds
                .Select(wordId => wordsById.GetValueOrDefault(wordId))
                .OfType<RecognizedWord>()
                .OrderBy(word => word.Index)
                .ToArray();

            if (lineWords.Length > 0)
            {
                lines.Add(BuildExportLine(lineWords));
            }
        }

        return lines.Count > 0
            ? lines
            : BuildExportLines(document.Words);
    }

    private static IReadOnlyList<ExportLine> BuildExportLines(IReadOnlyList<RecognizedWord> words)
    {
        if (words.Count == 0)
        {
            return [];
        }

        return words
            .OrderBy(word => word.Index)
            .GroupBy(word => word.LineIndex)
            .OrderBy(group => group.Key)
            .Select(group => BuildExportLine(group.OrderBy(word => word.Index).ToArray()))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .ToArray();
    }

    private static ExportLine BuildExportLine(IReadOnlyList<RecognizedWord> words)
    {
        var text = NormalizeWords(words);
        var left = words.Min(word => word.BoundingRect.Left);
        var top = words.Min(word => word.BoundingRect.Top);
        var right = words.Max(word => word.BoundingRect.Right);
        var bottom = words.Max(word => word.BoundingRect.Bottom);
        var averageHeight = words.Average(word => Math.Max(1d, word.BoundingRect.Height));

        return new ExportLine(
            Text: text,
            Left: left,
            Top: top,
            Right: right,
            Bottom: bottom,
            AverageWordHeight: averageHeight,
            WordCount: words.Count);
    }

    private static string BuildReadableText(IReadOnlyList<ExportLine> lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        ExportLine? previous = null;

        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line.Text)))
        {
            if (previous is null)
            {
                builder.Append(line.Text);
                previous = line;
                continue;
            }

            if (ShouldStartParagraph(previous.Value, line))
            {
                builder.AppendLine();
                builder.AppendLine();
            }
            else if (ShouldKeepLineBreak(previous.Value, line))
            {
                builder.AppendLine();
            }
            else if (ShouldInsertSpace(previous.Value.Text, line.Text))
            {
                builder.Append(' ');
            }

            builder.Append(line.Text);
            previous = line;
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeWords(IReadOnlyList<RecognizedWord> words)
    {
        if (words.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        string? previous = null;

        foreach (var word in words.OrderBy(word => word.Index))
        {
            if (builder.Length > 0 && ShouldInsertSpace(previous, word.NormalizedText))
            {
                builder.Append(' ');
            }

            builder.Append(word.NormalizedText);
            previous = word.NormalizedText;
        }

        return builder.ToString().Trim();
    }

    private static bool ShouldStartParagraph(ExportLine previous, ExportLine current)
    {
        var verticalGap = current.Top - previous.Bottom;
        var height = Math.Max(previous.AverageWordHeight, current.AverageWordHeight);

        return verticalGap > height * 1.45d
            || (verticalGap > height * 0.95d && EndsSentence(previous.Text) && !StartsWithLowercase(current.Text));
    }

    private static bool ShouldKeepLineBreak(ExportLine previous, ExportLine current)
    {
        if (StartsListItem(current.Text))
        {
            return true;
        }

        if (StartsListItem(previous.Text))
        {
            return true;
        }

        if (LooksLikeHeading(previous, current))
        {
            return true;
        }

        if (previous.Text.EndsWith(":", StringComparison.Ordinal))
        {
            return true;
        }

        var verticalGap = current.Top - previous.Bottom;
        var height = Math.Max(previous.AverageWordHeight, current.AverageWordHeight);
        if (verticalGap > height * 0.65d && !StartsWithLowercase(current.Text))
        {
            return true;
        }

        var indentDelta = Math.Abs(current.Left - previous.Left);
        return indentDelta > Math.Max(previous.AverageWordHeight * 1.8d, 18d)
            && EndsSentence(previous.Text);
    }

    private static bool LooksLikeHeading(ExportLine previous, ExportLine current)
        => previous.WordCount <= 8
           && !EndsSentence(previous.Text)
           && previous.AverageWordHeight >= current.AverageWordHeight * 1.12d;

    private static bool StartsListItem(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed[0] is '-' or '*' or '•' or '–')
        {
            return true;
        }

        var dotIndex = trimmed.IndexOf('.');
        return dotIndex is > 0 and <= 3
            && trimmed[..dotIndex].All(char.IsDigit)
            && dotIndex + 1 < trimmed.Length
            && char.IsWhiteSpace(trimmed[dotIndex + 1]);
    }

    private static bool EndsSentence(string text)
        => text.TrimEnd().EndsWith('.') || text.TrimEnd().EndsWith('!') || text.TrimEnd().EndsWith('?');

    private static bool StartsWithLowercase(string text)
    {
        foreach (var character in text)
        {
            if (char.IsLetter(character))
            {
                return char.IsLower(character);
            }
        }

        return false;
    }

    private static bool ShouldInsertSpace(string? previous, string current)
    {
        if (string.IsNullOrWhiteSpace(previous))
        {
            return false;
        }

        if (current.Length > 0 && LeadingNoSpaceCharacters.Contains(current[0]))
        {
            return false;
        }

        if (previous.Length > 0 && TrailingNoSpaceCharacters.Contains(previous[^1]))
        {
            return false;
        }

        if ((previous.EndsWith('-') && previous != "-") || current.StartsWith('-'))
        {
            return false;
        }

        return true;
    }

    private readonly record struct ExportLine(
        string Text,
        double Left,
        double Top,
        double Right,
        double Bottom,
        double AverageWordHeight,
        int WordCount);
}
