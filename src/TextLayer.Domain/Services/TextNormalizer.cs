using System.Text;
using TextLayer.Domain.Models;

namespace TextLayer.Domain.Services;

public sealed class TextNormalizer
{
    private static readonly char[] LeadingNoSpaceCharacters = [',', '.', ';', ':', '!', '?', ')', ']', '}', '%'];
    private static readonly char[] TrailingNoSpaceCharacters = ['(', '[', '{', '/', '$', '#'];

    public string NormalizeDocument(RecognizedDocument document)
        => NormalizeWords(document.Words);

    public string NormalizeSelection(IReadOnlyList<RecognizedWord> words)
        => NormalizeWords(words);

    private string NormalizeWords(IReadOnlyList<RecognizedWord> words)
    {
        if (words.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var currentLineIndex = words[0].LineIndex;
        string? previous = null;

        foreach (var word in words.OrderBy(word => word.Index))
        {
            if (builder.Length > 0)
            {
                if (word.LineIndex != currentLineIndex)
                {
                    builder.AppendLine();
                    previous = null;
                    currentLineIndex = word.LineIndex;
                }
                else if (ShouldInsertSpace(previous, word.NormalizedText))
                {
                    builder.Append(' ');
                }
            }

            builder.Append(word.NormalizedText);
            previous = word.NormalizedText;
        }

        return builder.ToString().Trim();
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

        if (previous.EndsWith('-') || current.StartsWith('-'))
        {
            return false;
        }

        return true;
    }
}
