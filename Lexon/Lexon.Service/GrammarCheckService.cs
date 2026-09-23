using Lexon.AI.Interfaces;
using Lexon.Core.Grammar;
using Lexon.Core.Interfaces;
using Lexon.Core.Models;
using Lexon.Input;
using Lexon.Input.Interfaces;
using Lexon.Overlay;
using Lexon.Overlay.Interfaces;
using Lexon.Privacy;
using Lexon.Profiles;

namespace Lexon.Service;

public sealed class GrammarCheckService
{
    public const string MuteAppItem = "Mute grammar in this app";

    private IAIProvider? _aiProvider;
    private readonly IFocusTracker _focusTracker;
    private readonly IActionMenuOverlay _menu;
    private readonly SelectionRewriteService _rewrite;
    private readonly IEditConfirmation _confirmation;
    private readonly ITextInjector _injector;
    private readonly Lexon.Input.UndoManager _undo;
    private readonly Profile _profile;
    private readonly IPrivacyGuard _privacyGuard;
    private readonly GlanceOverlay? _glance;
    private readonly ISuggestionOverlay? _suggestions;
    private string _sourceText = string.Empty;
    private List<GrammarMatch> _matches = [];
    private long _pauseGeneration;
    private string _lastCheckedFingerprint = string.Empty;
    private bool _enabled = true;

    public const int PauseIdleMilliseconds = 1500;

    public GrammarCheckService(
        IAIProvider? aiProvider,
        IFocusTracker focusTracker,
        IActionMenuOverlay menu,
        SelectionRewriteService rewrite,
        IEditConfirmation confirmation,
        ITextInjector injector,
        Lexon.Input.UndoManager undo,
        Profile profile,
        IPrivacyGuard privacyGuard,
        GlanceOverlay? glance = null,
        ISuggestionOverlay? suggestions = null)
    {
        _aiProvider = aiProvider;
        _focusTracker = focusTracker;
        _menu = menu;
        _rewrite = rewrite;
        _confirmation = confirmation;
        _injector = injector;
        _undo = undo;
        _profile = profile;
        _privacyGuard = privacyGuard;
        _glance = glance;
        _suggestions = suggestions;
        _menu.ItemSelected += (_, args) =>
        {
            if (args.Text == MuteAppItem)
            {
                MuteCurrentApp();
                return;
            }

            if (args.Index <= 0 || args.Index > _matches.Count)
            {
                return;
            }

            FixMatch(_matches[args.Index - 1]);
        };
    }

    public event Action<IReadOnlyList<Suggestion>>? SuggestionsOffered;

    public void SetProvider(IAIProvider? provider) => _aiProvider = provider;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
        {
            Interlocked.Increment(ref _pauseGeneration);
            _menu.Hide();
            _suggestions?.Hide();
        }
    }

    public bool IsAutomaticEnabled
        => _enabled && _profile.GetSetting("GrammarChecking", true);

    public void NoteActivity()
    {
        Interlocked.Increment(ref _pauseGeneration);
    }

    public void DismissAssistance()
    {
        NoteActivity();
        _glance?.Hide();
        _suggestions?.Hide();
        _menu.Hide();
    }

    public void SchedulePauseCheck()
    {
        if (!IsAutomaticEnabled)
        {
            return;
        }

        var generation = Interlocked.Increment(ref _pauseGeneration);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(PauseIdleMilliseconds);
                if (generation != Interlocked.Read(ref _pauseGeneration))
                {
                    return;
                }

                await CheckAsync(silentIfNone: true, pauseGeneration: generation);
            }
            catch
            {
                // Pause checks must never surface on the typing path.
            }
        });
    }

    public static bool ShouldRunPauseCheck(
        string currentFingerprint,
        string lastFingerprint,
        bool grammarEnabled,
        bool muted,
        bool menuVisible)
    {
        return grammarEnabled
            && !muted
            && !menuVisible
            && !string.IsNullOrWhiteSpace(currentFingerprint)
            && !string.Equals(currentFingerprint, lastFingerprint, StringComparison.Ordinal);
    }

    public bool TryHandleKey(KeyboardEventArgs e)
    {
        if (_menu.IsVisible && !e.IsControlPressed && !e.IsAltPressed && !e.IsShiftPressed)
        {
            if (e.VirtualKey is 38 or 40 or 13 or 27)
            {
                e.Handled = true;
                if (e.VirtualKey == 40) _menu.SelectNext();
                else if (e.VirtualKey == 38) _menu.SelectPrevious();
                else if (e.VirtualKey == 13) _menu.ConfirmSelection();
                else _menu.Cancel();
                return true;
            }
        }

        if (e.VirtualKey == 0x47
            && e.IsControlPressed
            && e.IsAltPressed
            && !e.IsShiftPressed
            && _profile.GetSetting("EnableGrammarHotkey", true))
        {
            e.Handled = true;
            _ = CheckAsync(silentIfNone: false, pauseGeneration: null);
            return true;
        }

        return false;
    }

    public bool IsMutedFor(TextContext context)
    {
        var app = AppCategoryMapper.Normalize(context.ApplicationName);
        var muted = _profile.GetSetting<List<string>>("GrammarMutedApps", []) ?? [];
        if (muted.Any(m => AppCategoryMapper.Normalize(m) == app))
        {
            return true;
        }

        if (_profile.GetSetting("MuteGrammarForCasualApps", false)
            && context.AppCategory == AppWritingCategory.Casual)
        {
            return true;
        }

        return false;
    }

    private Task CheckAsync(bool silentIfNone, long? pauseGeneration)
    {
        if (!_enabled)
        {
            return Task.CompletedTask;
        }

        if (silentIfNone && !_profile.GetSetting("GrammarChecking", true))
        {
            return Task.CompletedTask;
        }

        if (pauseGeneration is long generation
            && generation != Interlocked.Read(ref _pauseGeneration))
        {
            return Task.CompletedTask;
        }

        var context = _rewrite.Enrich(_focusTracker.GetCurrentContext());
        if (_privacyGuard.ShouldBlockAssistance(context))
        {
            if (!silentIfNone)
            {
                var caretBlocked = _focusTracker.GetCaretScreenPosition();
                if (_glance != null)
                {
                    _glance.ShowGlance(PrivacyGuard.GrammarBlockedMessage, caretBlocked.X, caretBlocked.Y);
                }
                else
                {
                    _menu.ShowMenu([PrivacyGuard.GrammarBlockedMessage], caretBlocked.X, caretBlocked.Y);
                }
            }

            return Task.CompletedTask;
        }

        if (IsMutedFor(context))
        {
            return Task.CompletedTask;
        }

        _focusTracker.TryGetSelectedText(out var selected);
        var liveText = context.FullText;
        if (WebEditorSupport.ShouldIgnoreLiveText(liveText, context.WindowTitle)
            || WebEditorSupport.IsWebDocumentEditor(context.ApplicationName, context.WindowTitle, null))
        {
            liveText = _focusTracker.GetTypedBufferText();
        }

        var text = ResolveCheckText(
            selected,
            liveText,
            context.PreviousWords,
            _focusTracker.GetTypedBufferText(),
            useSelection: !silentIfNone);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        var fingerprint = text.Trim();
        if (silentIfNone
            && !ShouldRunPauseCheck(
                fingerprint,
                _lastCheckedFingerprint,
                _profile.GetSetting("GrammarChecking", true),
                false,
                _menu.IsVisible))
        {
            return Task.CompletedTask;
        }

        _lastCheckedFingerprint = fingerprint;
        _sourceText = text;
        var sensitivity = _profile.GetSetting("GrammarSensitivity", "Medium");
        var found = RuleBasedGrammarChecker.Find(text, sensitivity);
        var limit = sensitivity switch
        {
            "Low" => 4,
            "High" => 16,
            _ => 8
        };
        _matches = found.Take(limit).ToList();

        var caret = _focusTracker.GetCaretScreenPosition();
        if (_matches.Count == 0)
        {
            if (!silentIfNone)
            {
                _menu.ShowMenu(["No issues found"], caret.X, caret.Y);
            }

            return Task.CompletedTask;
        }

        if (silentIfNone)
        {
            var trailing = GrammarSuggestionMapper.TrailingMatches(fingerprint, sensitivity);
            if (trailing.Count == 0 && _matches.Count > 0)
            {
                trailing = _matches
                    .OrderByDescending(m => m.Start)
                    .Take(1)
                    .ToList();
            }

            if (trailing.Count == 0)
            {
                return Task.CompletedTask;
            }

            var suggestions = trailing.Take(3).Select(GrammarSuggestionMapper.ToSuggestion).ToList();
            var caretX = caret.X;
            var caretY = caret.Y;
            if (caretX == 100 && caretY == 100)
            {
                // Dummy caret — keep the popup on-screen rather than top-left.
                caretX = 200;
                caretY = 200;
            }

            if (pauseGeneration is long still
                && still != Interlocked.Read(ref _pauseGeneration))
            {
                return Task.CompletedTask;
            }

            SuggestionsOffered?.Invoke(suggestions);
            return Task.CompletedTask;
        }

        var items = new List<string> { $"{_matches.Count} issue{(_matches.Count == 1 ? "" : "s")} found" };
        items.AddRange(_matches.Select(m => m.DisplayText));
        items.Add(MuteAppItem);
        _menu.ShowMenu(items, caret.X, caret.Y);
        return Task.CompletedTask;
    }

    private void MuteCurrentApp()
    {
        var app = AppCategoryMapper.Normalize(_focusTracker.GetCurrentContext().ApplicationName);
        if (string.IsNullOrEmpty(app))
        {
            return;
        }

        var muted = _profile.GetSetting<List<string>>("GrammarMutedApps", []) ?? [];
        if (!muted.Any(m => AppCategoryMapper.Normalize(m) == app))
        {
            muted.Add(app);
            _profile.SetSetting("GrammarMutedApps", muted);
            _ = _profile.SaveAsync();
        }
    }

    private void FixMatch(GrammarMatch match)
    {
        if (string.IsNullOrEmpty(_sourceText))
        {
            return;
        }

        var next = RuleBasedGrammarChecker.Apply(_sourceText, match);
        if (string.IsNullOrWhiteSpace(next) || next == _sourceText)
        {
            return;
        }

        var caret = _focusTracker.GetCaretScreenPosition();
        var original = _sourceText;
        _sourceText = next;
        _confirmation.RequestEdit(original, next, caret.X, caret.Y, () =>
        {
            _injector.ReplaceText(original, next);
            _undo.RecordOperation(original, next);
        }, trustKey: "grammar-fix");
    }

    /// <summary>
    /// Pause checks use nearby typed text, not a selection. The hotkey still
    /// prefers selected text when the user has highlighted something.
    /// </summary>
    public static string ResolveCheckText(
        string? selected,
        string? fullText,
        string? previousWords,
        string? typedBuffer,
        bool useSelection)
    {
        if (useSelection && !string.IsNullOrWhiteSpace(selected))
        {
            return selected.Trim();
        }

        var text = FirstNonEmpty(fullText, previousWords, typedBuffer);
        return ClipToRecent(text);
    }

    public static string ClipToRecent(string text, int maxChars = 480)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text.Trim();
        if (text.Length <= maxChars)
        {
            return text;
        }

        var slice = text[^maxChars..];
        var breakAt = slice.IndexOfAny(['.', '!', '?', '\n']);
        if (breakAt >= 0 && breakAt < slice.Length - 24)
        {
            slice = slice[(breakAt + 1)..];
        }

        return slice.Trim();
    }

    private static string FirstNonEmpty(params string?[] parts)
    {
        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                return part;
            }
        }

        return string.Empty;
    }
}
