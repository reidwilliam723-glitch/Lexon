using System.Text;

namespace Lexon.Core.Grammar;

/// <summary>
/// Skips English spelling checks for foreign or mixed-script tokens so
/// code-switching mid-sentence is not treated as a typo.
/// </summary>
public static class ScriptLanguageGuard
{
    private const double NonLatinLetterThreshold = 0.40;
    private const int SurroundingScanChars = 600;

    /// <summary>
    /// Returns true when <paramref name="word"/> has non-ASCII or non-Latin
    /// letters, or when <paramref name="surroundingText"/> is predominantly
    /// non-Latin (more than 40% of its letters).
    /// </summary>
    public static bool ShouldSkipSpelling(string word, string? surroundingText = null)
    {
        if (!string.IsNullOrEmpty(word) && WordLooksForeign(word))
        {
            return true;
        }

        return SurroundingIsPredominantlyNonLatin(Tail(surroundingText));
    }

    private static bool WordLooksForeign(string word)
    {
        foreach (var rune in word.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune))
            {
                continue;
            }

            if (rune.Value > 0x7F || !IsLatinLetter(rune))
            {
                return true;
            }
        }

        return false;
    }

    private static string? Tail(string? surroundingText)
    {
        if (string.IsNullOrEmpty(surroundingText) || surroundingText.Length <= SurroundingScanChars)
        {
            return surroundingText;
        }

        return surroundingText[^SurroundingScanChars..];
    }

    private static bool SurroundingIsPredominantlyNonLatin(string? surroundingText)
    {
        if (string.IsNullOrEmpty(surroundingText))
        {
            return false;
        }

        var letters = 0;
        var nonLatin = 0;
        foreach (var rune in surroundingText.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune))
            {
                continue;
            }

            letters++;
            if (!IsLatinLetter(rune))
            {
                nonLatin++;
            }
        }

        return letters > 0 && (double)nonLatin / letters > NonLatinLetterThreshold;
    }

    /// <summary>
    /// Latin script including Basic Latin and common Latin Extended blocks.
    /// </summary>
    private static bool IsLatinLetter(Rune rune)
    {
        var code = rune.Value;
        return code is (>= 0x0041 and <= 0x005A)
            or (>= 0x0061 and <= 0x007A)
            or (>= 0x00C0 and <= 0x00D6)
            or (>= 0x00D8 and <= 0x00F6)
            or (>= 0x00F8 and <= 0x024F)
            or (>= 0x1E00 and <= 0x1EFF)
            or (>= 0x2C60 and <= 0x2C7F)
            or (>= 0xA720 and <= 0xA7FF)
            or (>= 0xAB30 and <= 0xAB6F)
            or (>= 0xFF21 and <= 0xFF3A)
            or (>= 0xFF41 and <= 0xFF5A);
    }
}
