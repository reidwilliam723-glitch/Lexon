using Lexon.Core.Models;

namespace Lexon.Core.Grammar;

public static class GrammarSuggestionMapper
{
    public const string Source = "Grammar";
    public const string OriginalKey = "ReplaceOriginal";
    public const string ReplacementKey = "Replacement";

    public static bool IsGrammarFix(Suggestion? suggestion)
        => suggestion != null
           && string.Equals(suggestion.Source, Source, StringComparison.Ordinal);

    public static void SplitFrom(IEnumerable<Suggestion> suggestions, out List<Suggestion> grammar, out List<Suggestion> completions)
    {
        grammar = [];
        completions = [];
        foreach (var suggestion in suggestions)
        {
            if (IsGrammarFix(suggestion))
            {
                grammar.Add(suggestion);
            }
            else
            {
                completions.Add(suggestion);
            }
        }
    }

    public static Suggestion ToSuggestion(GrammarMatch match)
    {
        return new Suggestion
        {
            Text = match.DisplayText,
            Source = Source,
            Score = 0.995,
            Category = match.Category.ToString(),
            Metadata =
            {
                [OriginalKey] = match.Original,
                [ReplacementKey] = match.Replacement
            }
        };
    }

    public static bool TryGetSpanReplacement(Suggestion? suggestion, out string original, out string replacement)
    {
        original = string.Empty;
        replacement = string.Empty;
        if (suggestion == null || !string.Equals(suggestion.Source, Source, StringComparison.Ordinal))
        {
            return false;
        }

        if (!suggestion.Metadata.TryGetValue(OriginalKey, out var originalObj)
            || originalObj is not string originalText
            || string.IsNullOrEmpty(originalText))
        {
            return false;
        }

        original = originalText;
        if (suggestion.Metadata.TryGetValue(ReplacementKey, out var replacementObj)
            && replacementObj is string replacementText
            && replacementText.Length > 0)
        {
            replacement = replacementText;
        }
        else
        {
            replacement = suggestion.Text;
        }

        return replacement.Length > 0;
    }

    public static IReadOnlyList<GrammarMatch> TrailingMatches(
        string text,
        string sensitivity = "Medium",
        bool includeConsistency = false,
        Func<string, bool>? skipSpellingWord = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var trimmed = text.TrimEnd();
        IEnumerable<GrammarMatch> matches = RuleBasedGrammarChecker.Find(trimmed, sensitivity, skipSpellingWord);
        if (includeConsistency)
        {
            matches = matches.Concat(ConsistencyChecker.Find(trimmed));
        }

        return matches
            .Where(match => IsTrailing(trimmed, match))
            .OrderByDescending(match => match.Start)
            .ToList();
    }

    public static IReadOnlyList<Suggestion> Suggest(
        TextContext context,
        string sensitivity = "Medium",
        bool includeConsistency = false,
        Func<string, bool>? skipSpellingWord = null)
    {
        var window = WindowFrom(context);
        if (window.Length < 3)
        {
            return [];
        }

        return TrailingMatches(window, sensitivity, includeConsistency, skipSpellingWord)
            .Take(3)
            .Select(ToSuggestion)
            .ToList();
    }

    public static string WindowFrom(TextContext context)
    {
        var full = context.FullText ?? string.Empty;
        if (full.Length > 0)
        {
            var caret = context.CursorPosition;
            if (caret < 0 || caret > full.Length)
            {
                caret = full.Length;
            }

            var before = full[..caret];
            const int maxChars = 400;
            if (before.Length > maxChars)
            {
                before = before[^maxChars..];
            }

            return before.TrimEnd();
        }

        var current = context.CurrentWord ?? string.Empty;
        var previous = context.PreviousWords?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(previous))
        {
            return current;
        }

        if (string.IsNullOrEmpty(current))
        {
            return previous;
        }

        return previous + " " + current;
    }

    private static bool IsTrailing(string text, GrammarMatch match)
    {
        if (match.Start < 0 || match.Start + match.Length > text.Length)
        {
            return false;
        }

        var after = text[(match.Start + match.Length)..];
        return after.Length == 0 || after.All(ch => char.IsWhiteSpace(ch) || char.IsPunctuation(ch));
    }
}
