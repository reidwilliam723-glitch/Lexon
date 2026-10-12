namespace Lexon.Input;

/// <summary>
/// Computes how to apply an autocomplete suggestion over the already-typed prefix.
/// </summary>
public static class SuggestionInsertion
{
    /// <summary>
    /// Returns how many characters to delete before inserting <paramref name="suggestion"/>.
    /// When the suggestion continues the current word, only the missing suffix is inserted
    /// so "hel" + "hello" becomes "hello" rather than "helhello".
    /// </summary>
    /// <summary>
    /// The letters of the word before the caret, and the letters of that same
    /// word still after the caret. False when the caret is not inside a word.
    /// </summary>
    public static bool TryWordAtCaret(string? fullText, int caret, out string before, out string after)
    {
        before = string.Empty;
        after = string.Empty;
        if (string.IsNullOrEmpty(fullText))
        {
            return false;
        }

        if (caret < 0)
        {
            caret = 0;
        }
        else if (caret > fullText.Length)
        {
            caret = fullText.Length;
        }

        if (caret == 0 || IsWordSeparator(fullText[caret - 1]))
        {
            return false;
        }

        var start = caret;
        while (start > 0 && !IsWordSeparator(fullText[start - 1]))
        {
            start--;
        }

        var end = caret;
        while (end < fullText.Length && !IsWordSeparator(fullText[end]))
        {
            end++;
        }

        before = fullText[start..caret];
        after = fullText[caret..end];
        return before.Length > 0;
    }

    /// <summary>
    /// Prefix to suggest from. The word at the caret wins over the keystroke
    /// buffer, which only knows the last characters typed.
    /// </summary>
    public static string SuggestionPrefix(string? typedBuffer, string? fullText, int caret)
    {
        if (TryWordAtCaret(fullText, caret, out var before, out _))
        {
            return before;
        }

        return CurrentToken(typedBuffer);
    }

    /// <summary>
    /// Replacement for a word the caret sits inside. Letters after the caret
    /// are deleted as well, so the suggestion replaces the whole word.
    /// </summary>
    public static (int DeleteBackward, int DeleteForward, string InsertText) GetWordReplacement(
        string? beforeCaret,
        string? afterCaret,
        string? suggestion)
    {
        var (backward, insert) = GetReplacement(beforeCaret, suggestion);
        if (string.IsNullOrEmpty(suggestion))
        {
            return (0, 0, string.Empty);
        }

        return (backward, afterCaret?.Length ?? 0, insert);
    }

    public static (int DeleteCount, string InsertText) GetReplacement(string? currentWord, string? suggestion)
    {
        suggestion ??= string.Empty;
        currentWord ??= string.Empty;

        if (suggestion.Length == 0)
        {
            return (0, string.Empty);
        }

        if (currentWord.Length == 0)
        {
            return (0, suggestion);
        }

        if (suggestion.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
        {
            // Insert only the missing tail. Deleting the prefix first is
            // unreliable in some apps (Backspace is dropped) and produces
            // "helhello" when the full word is then injected.
            return (0, suggestion[currentWord.Length..]);
        }

        // Misspelling / correction: keep the shared stem so we backspace
        // only the wrong tail ("helo" → delete "o", insert "lo").
        var common = CommonPrefixLength(currentWord, suggestion);
        return (currentWord.Length - common, suggestion[common..]);
    }

    /// <summary>
    /// Replaces a trailing phrase (grammar span) by backspacing only the
    /// differing tail, then inserting the new tail. Deleting the whole
    /// original ("he are") is unreliable — apps drop batched Backspace and
    /// the overlay can cover the hole so the replacement looks invisible.
    /// </summary>
    public static bool TryReplaceTrailingPhrase(
        string? original,
        string? replacement,
        string? haystack,
        out int deleteCount,
        out string insertText)
    {
        deleteCount = 0;
        insertText = string.Empty;
        original ??= string.Empty;
        replacement ??= string.Empty;
        haystack ??= string.Empty;
        if (original.Length == 0 || replacement.Length == 0)
        {
            return false;
        }

        var trailingWhitespace = haystack.Length - haystack.TrimEnd().Length;
        var core = haystack.TrimEnd();
        while (core.Length > 0 && IsWordSeparator(core[^1]) && !char.IsWhiteSpace(core[^1]))
        {
            core = core[..^1];
        }

        core = core.TrimEnd();
        if (!core.EndsWith(original, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var (stemDelete, stemInsert) = GetReplacement(original, replacement);
        deleteCount = stemDelete + trailingWhitespace;
        insertText = stemInsert;
        if (!insertText.EndsWith(' '))
        {
            insertText += " ";
        }

        return deleteCount > 0 || insertText.Length > 0;
    }

    /// <summary>
    /// The in-progress word to replace. Prefers a typed token that is a
    /// prefix of the suggestion; if the user misspelled, uses the current
    /// token anyway so accept can overwrite it.
    /// </summary>
    public static string ResolveInProgressWord(
        string? suggestion,
        string? typedBuffer,
        string? textBeforeCaret,
        params string?[] extraCandidates)
    {
        var matching = ResolvePrefix(
            suggestion,
            BuildCandidates(typedBuffer, textBeforeCaret, extraCandidates));
        if (matching.Length > 0)
        {
            return matching;
        }

        var typed = CurrentToken(typedBuffer);
        if (typed.Length > 0)
        {
            return typed;
        }

        typed = CurrentToken(textBeforeCaret);
        if (typed.Length > 0)
        {
            return typed;
        }

        foreach (var candidate in extraCandidates)
        {
            typed = CurrentToken(candidate);
            if (typed.Length > 0)
            {
                return typed;
            }

            if (!string.IsNullOrEmpty(candidate) && candidate.All(c => !IsWordSeparator(c)))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Picks the longest in-progress token that is a prefix of
    /// <paramref name="suggestion"/>. Also scans suffixes of longer snippets
    /// (buffer / text before the caret) so a stale CurrentWord of "" still
    /// finds "hel" at the end of "please hel".
    /// </summary>
    public static string ResolvePrefix(string? suggestion, params string?[] candidates)
    {
        suggestion ??= string.Empty;
        var best = string.Empty;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            Consider(CurrentToken(candidate), suggestion, ref best);
            Consider(GetLastWord(candidate), suggestion, ref best);
            ConsiderLongestSuffix(candidate, suggestion, ref best);
        }

        return best;
    }

    /// <summary>
    /// The word currently being typed: empty if <paramref name="text"/> is
    /// empty or ends with a separator.
    /// </summary>
    public static string CurrentToken(string? text)
    {
        if (string.IsNullOrEmpty(text) || IsWordSeparator(text[^1]))
        {
            return string.Empty;
        }

        var start = text.Length;
        while (start > 0 && !IsWordSeparator(text[start - 1]))
        {
            start--;
        }

        return text[start..];
    }

    /// <summary>
    /// The word immediately before a trailing separator, or empty when the
    /// caret is still inside a word.
    /// </summary>
    public static string LastCompletedWord(string? text)
    {
        if (string.IsNullOrEmpty(text) || !IsWordSeparator(text[^1]))
        {
            return string.Empty;
        }

        var end = text.Length;
        while (end > 0 && IsWordSeparator(text[end - 1]))
        {
            end--;
        }

        if (end == 0)
        {
            return string.Empty;
        }

        var start = end;
        while (start > 0 && !IsWordSeparator(text[start - 1]))
        {
            start--;
        }

        return text[start..end];
    }

    public static string TextBeforeCaret(string? fullText, int caretPosition)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            return string.Empty;
        }

        if (caretPosition < 0 || caretPosition > fullText.Length)
        {
            caretPosition = fullText.Length;
        }

        return fullText[..caretPosition];
    }

    public static string GetLastWord(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var end = text.Length;
        while (end > 0 && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        var start = end;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        return text[start..end];
    }

    private static void Consider(string word, string suggestion, ref string best)
    {
        if (word.Length > best.Length
            && suggestion.StartsWith(word, StringComparison.OrdinalIgnoreCase))
        {
            best = word;
        }
    }

    private static void ConsiderLongestSuffix(string text, string suggestion, ref string best)
    {
        var max = Math.Min(text.Length, suggestion.Length);
        for (var length = max; length > best.Length; length--)
        {
            var suffix = text[^length..];
            if (!suggestion.StartsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separated = length == text.Length || IsWordSeparator(text[^(length + 1)]);
            if (separated)
            {
                best = suffix;
                return;
            }
        }
    }

    public static bool IsWordSeparator(char character)
        => char.IsWhiteSpace(character)
            || character is '.' or ',' or '!' or '?' or ';' or ':' or ')' or ']';

    public static bool EndsWithWordSeparator(string? text)
        => !string.IsNullOrEmpty(text) && IsWordSeparator(text[^1]);

    private static int CommonPrefixLength(string currentWord, string suggestion)
    {
        var length = Math.Min(currentWord.Length, suggestion.Length);
        var i = 0;
        while (i < length
            && char.ToLowerInvariant(currentWord[i]) == char.ToLowerInvariant(suggestion[i]))
        {
            i++;
        }

        return i;
    }

    private static string?[] BuildCandidates(
        string? typedBuffer,
        string? textBeforeCaret,
        string?[] extraCandidates)
    {
        var result = new string?[2 + extraCandidates.Length];
        result[0] = typedBuffer;
        result[1] = textBeforeCaret;
        extraCandidates.CopyTo(result, 2);
        return result;
    }
}
