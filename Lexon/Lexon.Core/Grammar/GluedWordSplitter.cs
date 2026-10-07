using System.Text.RegularExpressions;

namespace Lexon.Core.Grammar;

/// <summary>
/// Suggests a split when a token is two dictionary words glued together.
/// Never used for auto-correct — too many false positives.
/// </summary>
public static class GluedWordSplitter
{
    private static readonly Regex Token = new(@"\b[A-Za-z]{4,24}\b", RegexOptions.Compiled);

    private static readonly HashSet<string> ShortLeft = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "i", "an", "as", "at", "be", "by", "do", "go", "he", "if", "in", "is",
        "it", "me", "my", "no", "of", "on", "or", "so", "to", "up", "us", "we"
    };

    public static IReadOnlyList<GrammarMatch> Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var matches = new List<GrammarMatch>();
        foreach (Match match in Token.Matches(text))
        {
            var token = match.Value;
            if (HasInternalCapital(token) || GluedWordDictionary.Contains(token))
            {
                continue;
            }

            if (!TrySplit(token, out var left, out var right))
            {
                continue;
            }

            matches.Add(new GrammarMatch(
                match.Index,
                match.Length,
                token,
                left + " " + right,
                "Add a space between words",
                GrammarRuleCategory.Punctuation));
        }

        return matches;
    }

    public static bool TrySplit(string token, out string left, out string right)
    {
        left = string.Empty;
        right = string.Empty;
        if (string.IsNullOrEmpty(token) || token.Length < 4 || HasInternalCapital(token))
        {
            return false;
        }

        if (GluedWordDictionary.Contains(token))
        {
            return false;
        }

        var bestLeft = 0;
        for (var i = 1; i < token.Length; i++)
        {
            var candidateLeft = token[..i];
            var candidateRight = token[i..];
            if (!IsValidLeft(candidateLeft) || !IsValidRight(candidateRight))
            {
                continue;
            }

            if (i > bestLeft)
            {
                bestLeft = i;
                left = PreserveCase(token[..i], candidateLeft);
                right = PreserveCase(token[i..], candidateRight);
            }
        }

        return bestLeft > 0;
    }

    private static bool IsValidLeft(string word)
    {
        if (ShortLeft.Contains(word) && word.Length <= 2)
        {
            return true;
        }

        return word.Length >= 3 && GluedWordDictionary.Contains(word);
    }

    private static bool IsValidRight(string word)
        => word.Length >= 3 && GluedWordDictionary.Contains(word);

    private static bool HasInternalCapital(string token)
    {
        for (var i = 1; i < token.Length; i++)
        {
            if (char.IsUpper(token[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string PreserveCase(string original, string canonical)
    {
        if (original.Length == 0)
        {
            return canonical;
        }

        if (original.All(char.IsUpper))
        {
            return canonical.ToUpperInvariant();
        }

        if (char.IsUpper(original[0]))
        {
            return char.ToUpperInvariant(canonical[0]) + canonical[1..].ToLowerInvariant();
        }

        return canonical.ToLowerInvariant();
    }
}
