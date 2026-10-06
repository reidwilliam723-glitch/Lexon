using System.Text.RegularExpressions;

namespace Lexon.Core.Grammar;

/// <summary>
/// Flags when the same name or term appears with two different spellings
/// (casing, hyphens, or known regional variants) in one document.
/// </summary>
public static class ConsistencyChecker
{
    private static readonly Regex Token = new(
        @"\b[\p{L}][\p{L}\p{N}']*(?:-[\p{L}][\p{L}\p{N}']*)*\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "if", "then", "else", "when", "while",
        "to", "of", "in", "on", "at", "for", "from", "by", "with", "as", "is", "are",
        "was", "were", "be", "been", "being", "it", "its", "this", "that", "these",
        "those", "i", "you", "he", "she", "we", "they", "me", "him", "her", "us",
        "them", "my", "your", "his", "our", "their", "not", "no", "yes", "do", "does",
        "did", "have", "has", "had", "will", "would", "can", "could", "should", "may",
        "might", "must", "shall", "so", "than", "too", "very", "just", "also", "only"
    };

    /// <summary>
    /// Maps regional / alternate spellings onto one family key.
    /// </summary>
    private static readonly Dictionary<string, string> SpellingFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["colour"] = "color",
        ["color"] = "color",
        ["favour"] = "favor",
        ["favor"] = "favor",
        ["organise"] = "organize",
        ["organize"] = "organize",
        ["behaviour"] = "behavior",
        ["behavior"] = "behavior",
        ["centre"] = "center",
        ["center"] = "center",
        ["defence"] = "defense",
        ["defense"] = "defense",
        ["licence"] = "license",
        ["license"] = "license",
        ["grey"] = "gray",
        ["gray"] = "gray",
        ["cancelled"] = "canceled",
        ["canceled"] = "canceled",
        ["modelling"] = "modeling",
        ["modeling"] = "modeling"
    };

    public static IReadOnlyList<GrammarMatch> Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var tokens = new List<(int Start, string Value)>();
        foreach (Match match in Token.Matches(text))
        {
            if (match.Length < 3 || StopWords.Contains(match.Value))
            {
                continue;
            }

            tokens.Add((match.Index, match.Value));
        }

        if (tokens.Count < 2)
        {
            return [];
        }

        var groups = new Dictionary<string, List<(int Start, string Value)>>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            var key = FamilyKey(Fold(token.Value));
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(token);
        }

        var results = new List<GrammarMatch>();
        foreach (var group in groups.Values)
        {
            var forms = group
                .GroupBy(t => t.Value, StringComparer.Ordinal)
                .Select(g => (Form: g.Key, Count: g.Count(), First: g.Min(x => x.Start)))
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.First)
                .ToList();

            if (forms.Count < 2)
            {
                continue;
            }

            // Ignore pure ALL-CAPS acronyms vs lowercase common words unless both recur.
            if (!IsTermLike(forms))
            {
                continue;
            }

            var preferred = forms[0].Form;
            foreach (var occurrence in group)
            {
                if (occurrence.Value.Equals(preferred, StringComparison.Ordinal))
                {
                    continue;
                }

                results.Add(new GrammarMatch(
                    occurrence.Start,
                    occurrence.Value.Length,
                    occurrence.Value,
                    preferred,
                    $"Inconsistent spelling — also used as \"{preferred}\"",
                    GrammarRuleCategory.Consistency));
            }
        }

        return results
            .OrderBy(m => m.Start)
            .Take(12)
            .ToList();
    }

    private static bool IsTermLike(List<(string Form, int Count, int First)> forms)
    {
        // Prefer flagging names/products (mixed case or hyphen) or known variants.
        if (forms.Any(f => f.Form.Contains('-') || f.Form.Any(char.IsUpper) && f.Form.Any(char.IsLower)))
        {
            return true;
        }

        var folded = Fold(forms[0].Form);
        if (SpellingFamilies.ContainsKey(folded))
        {
            return true;
        }

        // Same letters, different casing only — require at least one form twice or length >= 5.
        return forms.Any(f => f.Count >= 2) || folded.Length >= 5;
    }

    private static string Fold(string value)
        => value.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);

    private static string FamilyKey(string folded)
        => SpellingFamilies.TryGetValue(folded, out var family) ? family : folded;
}
