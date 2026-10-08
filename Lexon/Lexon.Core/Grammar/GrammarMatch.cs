namespace Lexon.Core.Grammar;

public enum GrammarRuleCategory
{
    Typo,
    Agreement,
    ConfusedWord,
    Punctuation,
    Consistency
}

public sealed class GrammarMatch
{
    public GrammarMatch(
        int start,
        int length,
        string original,
        string replacement,
        string message,
        GrammarRuleCategory category)
    {
        Start = start;
        Length = length;
        Original = original;
        Replacement = replacement;
        Message = message;
        Category = category;
    }

    public int Start { get; }
    public int Length { get; }
    public string Original { get; }
    public string Replacement { get; }
    public string Message { get; }
    public GrammarRuleCategory Category { get; }
    public string DisplayText => $"{Original} → {Replacement}";

    public bool Overlaps(GrammarMatch other)
    {
        var end = Start + Length;
        var otherEnd = other.Start + other.Length;
        return Start < otherEnd && other.Start < end;
    }

    /// <summary>
    /// Keeps every rule-based match and drops consistency matches that cover the same span.
    /// </summary>
    public static List<GrammarMatch> PreferRuleBased(IReadOnlyList<GrammarMatch> ruleBased, IEnumerable<GrammarMatch> consistency)
    {
        var merged = new List<GrammarMatch>(ruleBased);
        foreach (var match in consistency)
        {
            if (ruleBased.Any(rule => rule.Overlaps(match)))
            {
                continue;
            }

            merged.Add(match);
        }

        return merged;
    }
}
