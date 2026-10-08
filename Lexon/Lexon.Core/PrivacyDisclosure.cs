namespace Lexon.Core;

/// <summary>
/// First-use and Settings revisit copy explaining what text can leave the PC.
/// Kept in Core so onboarding, coach, and Settings share one source.
/// </summary>
public static class PrivacyDisclosure
{
    public const string Title = "What Lexon can see — and what leaves your PC";

    public const string Body =
        "Lexon reads the focused typing field so it can offer suggestions. That includes the words around the caret in the active app.\n\n" +
        "Stays on this PC\n" +
        "• Local suggestions, spelling, and grammar\n" +
        "• Learned vocabulary and writing style\n\n" +
        "Can leave this PC (only when enabled)\n" +
        "• Cloud AI while typing — off by default. Words around the caret go to your chosen provider as you type.\n" +
        "• Rewrites — selection only, when you ask (Aa chip or Ctrl+Alt+R).\n" +
        "• Prefetch on selection — off by default. Before a selection is sent, Lexon names the provider and asks.\n\n" +
        "Stay fully local\n" +
        "• Turn on Local-only mode\n" +
        "• Block apps you do not want Lexon in\n" +
        "• Do not add an API key (nothing goes to OpenAI, Gemini, or DeepSeek)\n\n" +
        "Password fields and blocked apps are always skipped. Double-press Ctrl to disable Lexon until you turn it back on.";

    public const string ButtonLabel = "What Lexon can see…";
}
