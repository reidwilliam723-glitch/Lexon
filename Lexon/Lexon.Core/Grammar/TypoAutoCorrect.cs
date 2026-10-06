namespace Lexon.Core.Grammar;

/// <summary>
/// Decides whether a just-completed word should be auto-corrected.
/// Only <see cref="CommonMisspellings"/> qualify — never grammar rules.
/// </summary>
public static class TypoAutoCorrect
{
    public static bool TryGetCorrection(
        string? completedWord,
        bool enabled,
        IEnumerable<string>? learnedWords,
        out string correction,
        bool allowCodeSwitching = true)
    {
        correction = string.Empty;
        if (!enabled || string.IsNullOrWhiteSpace(completedWord))
        {
            return false;
        }

        if (allowCodeSwitching && ScriptLanguageGuard.ShouldSkipSpelling(completedWord))
        {
            return false;
        }

        if (!CommonMisspellings.TryCorrect(completedWord, out var raw)
            || raw.Equals(completedWord, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (learnedWords != null
            && learnedWords.Any(word => word.Equals(completedWord, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        correction = PreserveShape(completedWord, raw);
        return true;
    }

    public static (int DeleteCount, string InsertText) GetEdit(string completedWord, string correction, char separator)
        => (completedWord.Length + 1, correction + separator);

    public static string PreserveShape(string typed, string correction)
    {
        if (typed.Length > 0 && char.IsUpper(typed[0]))
        {
            if (typed.All(char.IsUpper) && !correction.Contains(' '))
            {
                return correction.ToUpperInvariant();
            }

            return char.ToUpperInvariant(correction[0]) + correction[1..];
        }

        return correction;
    }
}
