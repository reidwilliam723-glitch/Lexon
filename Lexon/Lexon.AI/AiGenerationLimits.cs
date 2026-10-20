namespace Lexon.AI;

internal static class AiGenerationLimits
{
    public const int SuggestionMaxTokens = 48;
    public static readonly TimeSpan SuggestionTimeout = TimeSpan.FromMilliseconds(2500);
    public static readonly TimeSpan RewriteTimeout = TimeSpan.FromSeconds(40);

    public static int RewriteMaxTokens(string text)
    {
        var length = string.IsNullOrEmpty(text) ? 0 : text.Length;
        return Math.Clamp(length / 2 + 256, 512, 4096);
    }
}
