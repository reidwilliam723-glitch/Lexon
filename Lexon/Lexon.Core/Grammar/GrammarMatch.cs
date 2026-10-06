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
}
