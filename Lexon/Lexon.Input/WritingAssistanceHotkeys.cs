using Lexon.AI.Interfaces;
using Lexon.Input.Interfaces;

namespace Lexon.Input;

/// <summary>
/// Manages writing assistance hotkeys for AI-powered text editing
/// </summary>
public class WritingAssistanceHotkeys
{
    private readonly IAIProvider _aiProvider;
    private readonly IFocusTracker _focusTracker;
    private readonly ITextInjector _textInjector;
    private readonly KeyboardShortcutManager _shortcutManager;
    private readonly UndoManager _undoManager;

    public WritingAssistanceHotkeys(
        IAIProvider aiProvider,
        IFocusTracker focusTracker,
        ITextInjector textInjector,
        KeyboardShortcutManager shortcutManager,
        UndoManager undoManager)
    {
        _aiProvider = aiProvider ?? throw new ArgumentNullException(nameof(aiProvider));
        _focusTracker = focusTracker ?? throw new ArgumentNullException(nameof(focusTracker));
        _textInjector = textInjector ?? throw new ArgumentNullException(nameof(textInjector));
        _shortcutManager = shortcutManager ?? throw new ArgumentNullException(nameof(shortcutManager));
        _undoManager = undoManager ?? throw new ArgumentNullException(nameof(undoManager));

        RegisterDefaultHotkeys();
    }

    private void RegisterDefaultHotkeys()
    {
        // Ctrl+Shift+R: Rewrite text
        _shortcutManager.RegisterShortcut("Rewrite", new KeyboardShortcut
        {
            Key = 0x52, // R
            Modifiers = KeyModifiers.Control | KeyModifiers.Shift
        }, async (sender, args) => await HandleRewriteAsync());

        // Ctrl+Shift+G: Improve grammar
        _shortcutManager.RegisterShortcut("ImproveGrammar", new KeyboardShortcut
        {
            Key = 0x47, // G
            Modifiers = KeyModifiers.Control | KeyModifiers.Shift
        }, async (sender, args) => await HandleImproveGrammarAsync());

        // Ctrl+Shift+F: Formal tone
        _shortcutManager.RegisterShortcut("FormalTone", new KeyboardShortcut
        {
            Key = 0x46, // F
            Modifiers = KeyModifiers.Control | KeyModifiers.Shift
        }, async (sender, args) => await HandleChangeToneAsync("formal"));

        // Ctrl+Shift+C: Casual tone
        _shortcutManager.RegisterShortcut("CasualTone", new KeyboardShortcut
        {
            Key = 0x43, // C
            Modifiers = KeyModifiers.Control | KeyModifiers.Shift
        }, async (sender, args) => await HandleChangeToneAsync("casual"));

        // Ctrl+Shift+P: Professional tone
        _shortcutManager.RegisterShortcut("ProfessionalTone", new KeyboardShortcut
        {
            Key = 0x50, // P
            Modifiers = KeyModifiers.Control | KeyModifiers.Shift
        }, async (sender, args) => await HandleChangeToneAsync("professional"));
    }

    public void RegisterCustomHotkey(string name, KeyboardShortcut shortcut, Func<Task> handler)
    {
        _shortcutManager.RegisterShortcut(name, shortcut, async (sender, args) => await handler());
    }

    private async Task HandleRewriteAsync()
    {
        var context = _focusTracker.GetCurrentContext();
        var textToRewrite = GetTextToProcess(context);

        if (string.IsNullOrEmpty(textToRewrite))
        {
            return;
        }

        var instruction = "Rewrite this text to be clearer and more concise";
        var rewritten = await _aiProvider.RewriteTextAsync(textToRewrite, instruction);

        if (!string.IsNullOrEmpty(rewritten) && rewritten != textToRewrite)
        {
            ReplaceText(textToRewrite, rewritten);
        }
    }

    private async Task HandleImproveGrammarAsync()
    {
        var context = _focusTracker.GetCurrentContext();
        var textToImprove = GetTextToProcess(context);

        if (string.IsNullOrEmpty(textToImprove))
        {
            return;
        }

        var improved = await _aiProvider.ImproveGrammarAsync(textToImprove);

        if (!string.IsNullOrEmpty(improved) && improved != textToImprove)
        {
            ReplaceText(textToImprove, improved);
        }
    }

    private async Task HandleChangeToneAsync(string tone)
    {
        var context = _focusTracker.GetCurrentContext();
        var textToChange = GetTextToProcess(context);

        if (string.IsNullOrEmpty(textToChange))
        {
            return;
        }

        var changed = await _aiProvider.ChangeToneAsync(textToChange, tone);

        if (!string.IsNullOrEmpty(changed) && changed != textToChange)
        {
            ReplaceText(textToChange, changed);
        }
    }

    private string GetTextToProcess(Lexon.Core.Models.TextContext context)
    {
        // Try to get selected text first (this would need to be implemented via UI Automation)
        // For now, use the current word or previous words as fallback
        if (!string.IsNullOrEmpty(context.CurrentWord))
        {
            return context.CurrentWord;
        }

        if (!string.IsNullOrEmpty(context.PreviousWords))
        {
            // Get the last word from previous words
            var words = context.PreviousWords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
            {
                return words[^1];
            }
        }

        return string.Empty;
    }

    private void ReplaceText(string oldText, string newText)
    {
        _textInjector.ReplaceText(oldText, newText);
        _undoManager.RecordOperation(oldText, newText);
    }

    public void UnregisterAllHotkeys()
    {
        _shortcutManager.UnregisterShortcut("Rewrite");
        _shortcutManager.UnregisterShortcut("ImproveGrammar");
        _shortcutManager.UnregisterShortcut("FormalTone");
        _shortcutManager.UnregisterShortcut("CasualTone");
        _shortcutManager.UnregisterShortcut("ProfessionalTone");
    }
}
