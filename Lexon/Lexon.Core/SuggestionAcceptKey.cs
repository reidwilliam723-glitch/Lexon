namespace Lexon.Core;

/// <summary>
/// Which key accepts the highlighted suggestion. Space is never one of these.
/// </summary>
public static class SuggestionAcceptKey
{
    public const string Tab = "Tab";
    public const string Enter = "Enter";
    public const string Right = "Right";
    public const string Numbers = "Numbers";

    public static string Normalize(string? stored)
    {
        if (string.Equals(stored, Enter, StringComparison.OrdinalIgnoreCase))
        {
            return Enter;
        }

        if (string.Equals(stored, Right, StringComparison.OrdinalIgnoreCase))
        {
            return Right;
        }

        if (string.Equals(stored, Numbers, StringComparison.OrdinalIgnoreCase))
        {
            return Numbers;
        }

        return Tab;
    }

    public static bool Matches(string? stored, int virtualKey, bool shift, bool control, bool alt)
    {
        if (shift || control || alt)
        {
            return false;
        }

        return Normalize(stored) switch
        {
            Enter => virtualKey == 13,
            Right => virtualKey == 39,
            Numbers => false,
            _ => virtualKey == 9
        };
    }
}
