namespace Lexon.Core;

public enum AiSendScope
{
    Typing,
    Rewrite,
    Prefetch
}

/// <summary>
/// Just-in-time check before text is sent to a cloud AI provider.
/// </summary>
public static class AiSendCheck
{
    public const string ConfirmKey = "ConfirmAiSends";
    public const string AllowedScopesKey = "AiSendAllowedScopes";
    public const string SendOnce = "Send this time";
    public const string DontSend = "Don't send";

    public static string AlwaysAllow(AiSendScope scope) => scope switch
    {
        AiSendScope.Prefetch => "Always allow selection prefetch",
        AiSendScope.Rewrite => "Always allow rewrite sends",
        _ => "Always allow while typing"
    };

    public static string Message(string? providerName, AiSendScope scope)
    {
        var provider = string.IsNullOrWhiteSpace(providerName) ? "Your AI provider" : providerName.Trim();
        return scope switch
        {
            AiSendScope.Prefetch => $"{provider} will receive the selected text before you choose a rewrite.",
            AiSendScope.Rewrite => $"{provider} will receive the selected text for this rewrite.",
            _ => $"{provider} will receive the words around the caret."
        };
    }

    public static bool NeedsPrompt(bool confirmSends, IEnumerable<string>? allowedScopes, AiSendScope scope, bool leavesThisPc)
    {
        if (!leavesThisPc || !confirmSends)
        {
            return false;
        }

        if (allowedScopes == null)
        {
            return true;
        }

        var token = scope.ToString();
        foreach (var allowed in allowedScopes)
        {
            if (string.Equals(allowed, token, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
