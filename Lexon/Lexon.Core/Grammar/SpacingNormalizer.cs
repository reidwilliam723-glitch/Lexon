using Lexon.Core.Models;

namespace Lexon.Core.Grammar;

/// <summary>
/// Safe spacing edits for prose. Never auto-applies in code apps, URLs,
/// emails, paths, versions, or around numeric punctuation.
/// </summary>
public static class SpacingNormalizer
{
    public static bool IsCodeApp(string? processName, IReadOnlyDictionary<string, AppWritingCategory>? overrides = null)
        => AppCategoryMapper.Resolve(processName, overrides) == AppWritingCategory.Code;

    /// <summary>
    /// Finds the rightmost safe spacing edit in <paramref name="text"/>.
    /// </summary>
    public static bool TryGetTrailingEdit(string? text, out int start, out int length, out string replacement)
    {
        start = 0;
        length = 0;
        replacement = string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (TryCollapseExtraSpaces(text, out start, out length, out replacement))
        {
            return true;
        }

        if (TryStripSpaceBeforePunct(text, out start, out length, out replacement))
        {
            return true;
        }

        return TryInsertSpaceAfterPunct(text, out start, out length, out replacement);
    }

    public static (int DeleteCount, string InsertText) GetEditToEnd(
        string text,
        int start,
        int length,
        string replacement)
    {
        var tail = start + length < text.Length ? text[(start + length)..] : string.Empty;
        return (text.Length - start, replacement + tail);
    }

    /// <summary>
    /// True when inserting a space after the punctuation at
    /// <paramref name="punctIndex"/> would break a number, URL, path, or repeat.
    /// </summary>
    public static bool ShouldSkipSpaceAfter(string text, int punctIndex)
    {
        if (punctIndex < 0 || punctIndex >= text.Length)
        {
            return true;
        }

        var punct = text[punctIndex];
        if (punct is not (',' or ';' or ':' or '.' or '!' or '?'))
        {
            return true;
        }

        if (punctIndex + 1 >= text.Length)
        {
            return true;
        }

        var next = text[punctIndex + 1];
        if (char.IsWhiteSpace(next))
        {
            return true;
        }

        if (IsRepeatedPunct(text, punctIndex) || IsEllipsis(text, punctIndex) || IsUrlScheme(text, punctIndex))
        {
            return true;
        }

        if (LooksLikeUrlEmailOrPath(text, punctIndex))
        {
            return true;
        }

        if (punct is '.' or ':' && NeighborsAreDigits(text, punctIndex))
        {
            return true;
        }

        if (punct == ',' && NeighborsAreDigits(text, punctIndex))
        {
            return true;
        }

        if (punct == '.' && !char.IsUpper(next) && !char.IsWhiteSpace(next))
        {
            // file.txt, example.com, v1.2 — only treat ".Next" (capital) as a sentence.
            return !char.IsLetter(next) || !char.IsUpper(next);
        }

        if (punct is ',' or ';' or '!' or '?' or ':')
        {
            return !char.IsLetter(next);
        }

        return false;
    }

    private static bool TryInsertSpaceAfterPunct(string text, out int start, out int length, out string replacement)
    {
        start = 0;
        length = 0;
        replacement = string.Empty;
        var from = Math.Max(0, text.Length - 80);
        for (var i = text.Length - 2; i >= from; i--)
        {
            var ch = text[i];
            if (ch is not (',' or ';' or ':' or '.' or '!' or '?'))
            {
                continue;
            }

            if (ShouldSkipSpaceAfter(text, i))
            {
                continue;
            }

            start = i;
            length = 2;
            replacement = ch + " " + text[i + 1];
            return true;
        }

        return false;
    }

    private static bool TryStripSpaceBeforePunct(string text, out int start, out int length, out string replacement)
    {
        start = 0;
        length = 0;
        replacement = string.Empty;
        if (text.Length < 2)
        {
            return false;
        }

        var punct = text[^1];
        if (punct is not (',' or ';' or ':' or '.' or '!' or '?'))
        {
            return false;
        }

        var i = text.Length - 2;
        if (!char.IsWhiteSpace(text[i]))
        {
            return false;
        }

        while (i > 0 && char.IsWhiteSpace(text[i - 1]))
        {
            i--;
        }

        if (i == 0 && char.IsWhiteSpace(text[0]))
        {
            return false;
        }

        if (punct == ':' && i + 1 < text.Length - 1 && text[i + 1] == '/')
        {
            return false;
        }

        start = i;
        length = text.Length - 1 - i + 1;
        replacement = punct.ToString();
        return true;
    }

    private static bool TryCollapseExtraSpaces(string text, out int start, out int length, out string replacement)
    {
        start = 0;
        length = 0;
        replacement = string.Empty;
        if (text.Length < 3 || !char.IsWhiteSpace(text[^1]))
        {
            return false;
        }

        var end = text.Length;
        var i = end;
        while (i > 0 && text[i - 1] is ' ' or '\t')
        {
            i--;
        }

        var count = end - i;
        if (count < 3)
        {
            return false;
        }

        if (i == 0 || text[i - 1] is '\n' or '\r')
        {
            return false;
        }

        var keepTwo = i > 0 && text[i - 1] is '.' or '!' or '?';
        start = i;
        length = count;
        replacement = keepTwo ? "  " : " ";
        return true;
    }

    private static bool NeighborsAreDigits(string text, int punctIndex)
    {
        var before = punctIndex > 0 && char.IsDigit(text[punctIndex - 1]);
        var after = punctIndex + 1 < text.Length && char.IsDigit(text[punctIndex + 1]);
        return before && after;
    }

    private static bool IsRepeatedPunct(string text, int punctIndex)
    {
        var punct = text[punctIndex];
        if (punct is not ('!' or '?'))
        {
            return false;
        }

        return (punctIndex > 0 && text[punctIndex - 1] == punct)
            || (punctIndex + 1 < text.Length && text[punctIndex + 1] == punct);
    }

    private static bool IsEllipsis(string text, int punctIndex)
    {
        if (text[punctIndex] != '.')
        {
            return false;
        }

        return (punctIndex > 0 && text[punctIndex - 1] == '.')
            || (punctIndex + 1 < text.Length && text[punctIndex + 1] == '.');
    }

    private static bool IsUrlScheme(string text, int punctIndex)
        => text[punctIndex] == ':'
           && punctIndex + 2 < text.Length
           && text[punctIndex + 1] == '/'
           && text[punctIndex + 2] == '/';

    private static bool LooksLikeUrlEmailOrPath(string text, int punctIndex)
    {
        var from = Math.Max(0, punctIndex - 24);
        var to = Math.Min(text.Length, punctIndex + 16);
        var window = text[from..to];
        if (window.Contains("://", StringComparison.Ordinal)
            || window.Contains("www.", StringComparison.OrdinalIgnoreCase)
            || window.Contains('@')
            || window.Contains('\\')
            || window.Contains('/'))
        {
            return true;
        }

        return HasVersionPattern(text, punctIndex);
    }

    private static bool HasVersionPattern(string text, int punctIndex)
    {
        if (text[punctIndex] != '.')
        {
            return false;
        }

        var left = punctIndex - 1;
        while (left >= 0 && (char.IsDigit(text[left]) || text[left] is 'v' or 'V'))
        {
            left--;
        }

        var right = punctIndex + 1;
        while (right < text.Length && (char.IsDigit(text[right]) || text[right] == '.'))
        {
            right++;
        }

        var slice = text[(left + 1)..Math.Min(right, text.Length)];
        var dots = 0;
        foreach (var ch in slice)
        {
            if (ch == '.')
            {
                dots++;
            }
        }

        return dots >= 2 && slice.Any(char.IsDigit);
    }
}
