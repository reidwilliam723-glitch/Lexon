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

        // Voters decide the preferred spelling. Capitalized sentence/quote/bullet openers
        // ("Report shows...") are capitalized by position, so they never vote and are only
        // checked afterwards against non-case differences (hyphens, regional variants).
        var voters = new List<(int Start, string Value)>();
        var openers = new List<(int Start, string Value)>();
        foreach (Match match in Token.Matches(text))
        {
            if (match.Length < 3 || StopWords.Contains(match.Value))
            {
                continue;
            }

            // A known misspelling must never become the preferred spelling.
            if (CommonMisspellings.TryCorrect(match.Value, out _))
            {
                continue;
            }

            if (IsInitialCapOnly(match.Value) && IsSentenceOpener(text, match.Index))
            {
                openers.Add((match.Index, match.Value));
            }
            else
            {
                voters.Add((match.Index, match.Value));
            }
        }

        if (voters.Count + openers.Count < 2)
        {
            return [];
        }

        var groups = new Dictionary<string, List<(int Start, string Value)>>(StringComparer.Ordinal);
        foreach (var token in voters)
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
        foreach (var (key, group) in groups)
        {
            var forms = group
                .GroupBy(t => t.Value, StringComparer.Ordinal)
                .Select(g => (Form: g.Key, Count: g.Count(), First: g.Min(x => x.Start)))
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.First)
                .ToList();

            var preferred = forms[0].Form;

            if (forms.Count >= 2 && IsTermLike(forms))
            {
                foreach (var occurrence in group)
                {
                    if (!occurrence.Value.Equals(preferred, StringComparison.Ordinal))
                    {
                        results.Add(Flag(occurrence.Start, occurrence.Value, preferred));
                    }
                }
            }

            // Openers are flagged only when they differ by more than case (hyphen / regional variant).
            foreach (var opener in openers)
            {
                if (FamilyKey(Fold(opener.Value)) == key
                    && !opener.Value.Equals(preferred, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(Flag(opener.Start, opener.Value, CapitalizeFirst(preferred)));
                }
            }
        }

        return results
            .GroupBy(m => m.Start)
            .Select(g => g.First())
            .OrderBy(m => m.Start)
            .Take(12)
            .ToList();
    }

    private static GrammarMatch Flag(int start, string original, string preferred)
        => new(
            start,
            original.Length,
            original,
            preferred,
            $"Inconsistent spelling — also used as \"{preferred}\"",
            GrammarRuleCategory.Consistency);

    private static bool IsTermLike(List<(string Form, int Count, int First)> forms)
    {
        if (forms.Any(f => f.Form.Contains('-')))
        {
            return true;
        }

        var folded = Fold(forms[0].Form);
        if (SpellingFamilies.ContainsKey(folded))
        {
            return true;
        }

        // Case-only differences: internal capitals (LexFlow, iPhone) or ALL-CAPS vs mixed/lower.
        // A plain first-letter capital (Apple/apple, March/march) is not a spelling variant.
        if (forms.Any(f => HasInternalCapital(f.Form)))
        {
            return true;
        }

        var hasAllCaps = forms.Any(f => IsAllCaps(f.Form));
        var hasNonAllCaps = forms.Any(f => !IsAllCaps(f.Form));
        return hasAllCaps && hasNonAllCaps;
    }

    private static bool HasInternalCapital(string value)
    {
        var seenLetter = false;
        foreach (var ch in value)
        {
            if (!char.IsLetter(ch))
            {
                continue;
            }

            if (seenLetter && char.IsUpper(ch) && value.Any(char.IsLower))
            {
                return true;
            }

            seenLetter = true;
        }

        return false;
    }

    private static bool IsAllCaps(string value)
        => value.Any(char.IsLetter) && value.Where(char.IsLetter).All(char.IsUpper);

    private static bool IsSentenceOpener(string text, int index)
    {
        var i = index - 1;
        var sawNewline = false;
        var sawOpeningMark = false;
        while (i >= 0 && (char.IsWhiteSpace(text[i]) || IsOpeningMark(text[i])))
        {
            sawNewline |= text[i] == '\n' || text[i] == '\r';
            sawOpeningMark |= IsOpeningMark(text[i]);
            i--;
        }

        return i < 0 || sawNewline || sawOpeningMark || text[i] is '.' or '!' or '?';
    }

    // Opening quotes/brackets and list bullets: a capital right after these starts a phrase.
    private static bool IsOpeningMark(char c)
        => c is '"' or '\u201C' or '\u2018' or '\'' or '(' or '[' or '{' or '\u00AB' or '-' or '*' or '\u2022' or '\u2013' or '\u2014';

    private static bool IsInitialCapOnly(string value)
        => value.Length > 1 && char.IsUpper(value[0]) && value.Skip(1).All(c => !char.IsUpper(c));

    private static string CapitalizeFirst(string value)
        => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string Fold(string value)
        => value.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);

    private static string FamilyKey(string folded)
        => SpellingFamilies.TryGetValue(folded, out var family) ? family : folded;
}
