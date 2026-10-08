using Lexon.AI.Interfaces;
using Lexon.Core;
using Lexon.Core.Interfaces;
using Lexon.Core.Learning;
using Lexon.Core.Models;
using Lexon.Input;
using Lexon.Input.Interfaces;
using Lexon.Overlay;
using Lexon.Overlay.Interfaces;
using Lexon.Privacy;
using Lexon.Profiles;

namespace Lexon.Service;

public sealed class SelectionRewriteService
{
    public const string CorrectToneItem = "Correct tone for this app…";
    public const string PlainLanguageOption = "Plain language";
    public const string FormalOption = "More formal";
    public const string ConciseOption = "More concise";

    /// <summary>
    /// Focused writing modes shown in the rewrite menu. Each mode streams a
    /// preview before apply (see <see cref="EditConfirmation.BeginLiveRewrite"/>).
    /// </summary>
    public static readonly string[] Options =
    [
        PlainLanguageOption,
        FormalOption,
        ConciseOption,
        "Expand",
        "Fix grammar",
        "Free instruction…"
    ];

    /// <summary>
    /// Modes offered as a Settings default (excludes free-form instruction).
    /// </summary>
    public static readonly string[] DefaultModeChoices =
    [
        PlainLanguageOption,
        FormalOption,
        ConciseOption,
        "Expand",
        "Fix grammar"
    ];

    private volatile IAIProvider? _aiProvider;
    private readonly IFocusTracker _focusTracker;
    private readonly ITextInjector _textInjector;
    private readonly UndoManager _undoManager;
    private readonly IEditConfirmation _confirmation;
    private readonly IActionMenuOverlay _menu;
    private readonly PersonalizationManager _personalization;
    private readonly Profile _profile;
    private readonly IPrivacyGuard _privacyGuard;
    private readonly CloudAiActivityLog? _aiLog;
    private readonly AiAccessPolicy? _accessPolicy;
    private readonly SelectionChipOverlay? _chip;
    private readonly GlanceOverlay? _glance;
    private string _pendingSelection = string.Empty;
    private string _cachedSelection = string.Empty;
    private string _pendingExtras = string.Empty;
    private TextContext? _pendingContext;
    private AppWritingCategory _pendingCategory;
    private string _pendingStyle = "default";
    private bool _pickingTone;
    private readonly object _prefetchLock = new();
    private Dictionary<string, Task<string>> _prefetch = [];
    private CancellationTokenSource? _prefetchCts;
    private string _prefetchSelection = string.Empty;
    private string _prefetchOption = string.Empty;
    private CancellationTokenSource? _rewriteCts;
    private bool _enabled = true;

    public SelectionRewriteService(
        IAIProvider? aiProvider,
        IFocusTracker focusTracker,
        ITextInjector textInjector,
        UndoManager undoManager,
        IEditConfirmation confirmation,
        IActionMenuOverlay menu,
        PersonalizationManager personalization,
        Profile profile,
        IPrivacyGuard privacyGuard,
        CloudAiActivityLog? aiLog = null,
        SelectionChipOverlay? chip = null,
        GlanceOverlay? glance = null,
        AiAccessPolicy? accessPolicy = null)
    {
        _aiProvider = aiProvider;
        _focusTracker = focusTracker;
        _textInjector = textInjector;
        _undoManager = undoManager;
        _confirmation = confirmation;
        _menu = menu;
        _personalization = personalization;
        _profile = profile;
        _privacyGuard = privacyGuard;
        _aiLog = aiLog;
        _accessPolicy = accessPolicy;
        if (_accessPolicy != null)
        {
            _accessPolicy.Changed += OnAccessPolicyChanged;
        }
        _chip = chip;
        _glance = glance;
        _menu.ItemSelected += (_, args) => _ = OnOptionSelected(args.Text);
        _menu.Cancelled += (_, _) => CancelPrefetch();
        if (_chip != null)
        {
            _chip.Clicked += (_, _) => ShowRewriteMenu();
        }
    }

    private void OnAccessPolicyChanged()
    {
        if (_accessPolicy != null && !_accessPolicy.AllowsPrefetch)
        {
            CancelPrefetch();
        }
    }

    public bool IsMenuVisible => _menu.IsVisible;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
        {
            CancelPending();
            HideAffordance();
            _menu.Hide();
        }
    }

    public void HideAffordance() => _chip?.Hide();

    public void ConsiderSelectionAffordance()
    {
        if (!_enabled)
        {
            _chip?.Hide();
            return;
        }
        if (_menu.IsVisible || _confirmation.IsPreviewVisible)
        {
            _chip?.Hide();
            return;
        }

        var context = Enrich(_focusTracker.GetCurrentContext());
        if (_privacyGuard.ShouldBlockAssistance(context))
        {
            _chip?.Hide();
            CancelPrefetch();
            return;
        }

        _focusTracker.TryGetSelectedText(out var selected);
        if (!TryResolveSelection(selected, cached: null, preferCache: false, out var resolved))
        {
            _chip?.Hide();
            _cachedSelection = string.Empty;
            return;
        }

        _cachedSelection = resolved;

        var caret = _focusTracker.GetCaretScreenPosition();
        _chip?.ShowNear(caret.X, caret.Y);
        if (_accessPolicy == null || _accessPolicy.AllowsPrefetch)
        {
            PrefetchLastUsed(resolved);
        }
    }

    public bool TryHandleKey(KeyboardEventArgs e)
    {
        if (!_enabled)
        {
            return false;
        }
        if (_confirmation.TryHandleKey(e.VirtualKey, e.IsShiftPressed, e.IsControlPressed, e.IsAltPressed))
        {
            e.Handled = true;
            return true;
        }

        if (_menu.IsVisible && !e.IsControlPressed && !e.IsAltPressed && !e.IsShiftPressed)
        {
            if (e.VirtualKey == 40)
            {
                e.Handled = true;
                _menu.SelectNext();
                return true;
            }

            if (e.VirtualKey == 38)
            {
                e.Handled = true;
                _menu.SelectPrevious();
                return true;
            }

            if (e.VirtualKey == 13)
            {
                e.Handled = true;
                _menu.ConfirmSelection();
                return true;
            }

            if (e.VirtualKey == 27)
            {
                e.Handled = true;
                _menu.Cancel();
                return true;
            }
        }

        if (e.VirtualKey == 0x52
            && e.IsControlPressed
            && e.IsAltPressed
            && !e.IsShiftPressed
            && _profile.GetSetting("EnableRewriteHotkey", true))
        {
            e.Handled = true;
            ShowRewriteMenu();
            return true;
        }

#if DEBUG
        if (e.VirtualKey == 0x50 && e.IsControlPressed && e.IsAltPressed && !e.IsShiftPressed)
        {
            e.Handled = true;
            var caret = _focusTracker.GetCaretScreenPosition();
            _confirmation.RequestEdit(
                "The quick brown fox jumps over the lazy dog.",
                "The quick brown fox leaps over the sleepy dog.\n\nSecond paragraph for a longer preview.",
                caret.X,
                caret.Y,
                () => { },
                trustKey: "preview-harness");
            return true;
        }
#endif

        if (e.IsShiftPressed && e.VirtualKey is 37 or 38 or 39 or 40 or 35 or 36)
        {
            ConsiderSelectionAffordance();
        }
        else if (!e.IsControlPressed && !e.IsAltPressed && e.VirtualKey is >= 48 and <= 90 or 8 or 32 or >= 186)
        {
            HideAffordance();
        }

        return false;
    }

    public void ShowRewriteMenu(int? x = null, int? y = null) => ShowRewriteMenu(x, y, preferCache: false);

    public void ShowRewriteMenuAt(int x, int y) => ShowRewriteMenu(x, y, preferCache: true);

    public static bool TryResolveSelection(string? live, string? cached, bool preferCache, out string selected)
    {
        selected = string.Empty;
        if (preferCache && !string.IsNullOrWhiteSpace(cached))
        {
            selected = cached.Trim();
            return true;
        }

        if (!string.IsNullOrWhiteSpace(live))
        {
            selected = live.Trim();
            return true;
        }

        if (!string.IsNullOrWhiteSpace(cached))
        {
            selected = cached.Trim();
            return true;
        }

        return false;
    }

    private void ShowRewriteMenu(int? x, int? y, bool preferCache)
    {
        if (!_enabled)
        {
            return;
        }
        var context = Enrich(_focusTracker.GetCurrentContext());
        if (_privacyGuard.ShouldBlockAssistance(context))
        {
            CancelPrefetch();
            _chip?.Hide();
            NotifyBlocked(PrivacyGuard.RewriteBlockedMessage);
            return;
        }

        string? live = null;
        if (!preferCache || string.IsNullOrWhiteSpace(_cachedSelection))
        {
            if (_focusTracker.TryGetSelectedText(out var read) && !string.IsNullOrWhiteSpace(read))
            {
                live = read;
            }
        }

        if (!TryResolveSelection(live, _cachedSelection, preferCache, out var selected))
        {
            return;
        }

        _pickingTone = false;
        _pendingSelection = selected;
        _cachedSelection = selected;
        _chip?.Hide();
        var caret = _focusTracker.GetCaretScreenPosition();
        _pendingContext = context;
        _pendingExtras = AiPromptContext.BuildExtras(context);
        var items = OrderedOptions();
        items.Add(CorrectToneItem);
        _menu.ShowMenu(items, x ?? caret.X, y ?? caret.Y, $"Detected tone: {context.AppCategory}");
        if (_aiProvider != null)
        {
            _ = _aiProvider.WarmupAsync();
        }

        StartPrefetch(selected, items, context);
    }

    private List<string> OrderedOptions()
        => OrderOptions(
            _profile.GetSetting("LastRewriteOption", string.Empty),
            _profile.GetSetting("DefaultWritingMode", PlainLanguageOption));

    internal static List<string> OrderOptions(string? lastUsed, string? defaultMode)
    {
        var preferred = !string.IsNullOrWhiteSpace(lastUsed)
            ? lastUsed
            : NormalizeDefaultMode(defaultMode);
        var items = Options.ToList();
        if (!string.IsNullOrEmpty(preferred) && items.Remove(preferred))
        {
            items.Insert(0, preferred);
        }

        return items;
    }

    public static string InstructionFor(string option)
        => option switch
        {
            PlainLanguageOption =>
                "Rewrite in plain language for a general audience. Use short sentences and everyday words. Keep the meaning.",
            FormalOption => "Make this more formal",
            ConciseOption => "Make this more concise without losing meaning",
            "Expand" => "Expand this with a bit more detail, keeping the same meaning",
            "Fix grammar" => "Fix grammar and clarity only",
            _ => "Rewrite this to be clearer"
        };

    /// <summary>
    /// Resolves a stored default mode to a known menu option.
    /// </summary>
    public static string NormalizeDefaultMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return PlainLanguageOption;
        }

        foreach (var choice in DefaultModeChoices)
        {
            if (choice.Equals(mode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return choice;
            }
        }

        return PlainLanguageOption;
    }

    private static string ComposeInstruction(string instruction, TextContext context)
    {
        var extras = AiPromptContext.BuildExtras(context);
        return string.IsNullOrEmpty(extras) ? instruction : extras + "\n" + instruction;
    }

    private void PrefetchLastUsed(string selected)
    {
        if (_accessPolicy != null && !_accessPolicy.AllowsPrefetch)
        {
            return;
        }

        var provider = _aiProvider;
        if (provider == null || _menu.IsVisible || _confirmation.IsPreviewVisible)
        {
            return;
        }

        var context = Enrich(_focusTracker.GetCurrentContext());
        if (_privacyGuard.ShouldBlockAssistance(context))
        {
            return;
        }

        StartPrefetch(selected, OrderedOptions(), context);
    }

    private void StartPrefetch(string selected, IEnumerable<string> items, TextContext context)
    {
        var provider = _aiProvider;
        if (provider == null || string.IsNullOrEmpty(selected) || _privacyGuard.ShouldBlockAssistance(context))
        {
            return;
        }

        if (_accessPolicy != null && !_accessPolicy.AllowsPrefetch)
        {
            return;
        }

        string? first = null;
        foreach (var option in items)
        {
            if (option == CorrectToneItem || option == "Free instruction…" || !Options.Contains(option))
            {
                continue;
            }

            first = option;
            break;
        }

        if (first == null)
        {
            return;
        }

        lock (_prefetchLock)
        {
            if (_prefetchSelection == selected
                && _prefetchOption == first
                && _prefetch.TryGetValue(first, out var running)
                && !running.IsCanceled
                && !running.IsFaulted)
            {
                return;
            }
        }

        CancelPrefetch();
        var cts = new CancellationTokenSource();
        _prefetchCts = cts;
        _prefetchSelection = selected;
        _prefetchOption = first;
        var instruction = ComposeInstruction(InstructionFor(first), context);
        NoteCloudSend(context, "prefetch");
        lock (_prefetchLock)
        {
            _prefetch = new Dictionary<string, Task<string>>(StringComparer.Ordinal)
            {
                [first] = provider.RewriteTextAsync(selected, instruction, cts.Token)
            };
        }
    }

    private Task<string> AwaitPrefetchOrStart(string option, string selected, string fullInstruction)
    {
        Task<string>? existing = null;
        lock (_prefetchLock)
        {
            _prefetch.TryGetValue(option, out existing);
        }

        if (existing != null)
        {
            return AwaitRewrite(existing, selected, fullInstruction);
        }

        CancelPrefetch();
        var provider = _aiProvider;
        if (provider == null)
        {
            return Task.FromResult(selected);
        }

        return provider.RewriteTextAsync(selected, fullInstruction);
    }

    private async Task<string> AwaitRewrite(Task<string> existing, string selected, string fullInstruction)
    {
        try
        {
            return await existing;
        }
        catch (OperationCanceledException)
        {
            var provider = _aiProvider;
            if (provider == null)
            {
                return selected;
            }

            return await provider.RewriteTextAsync(selected, fullInstruction);
        }
        catch
        {
            var provider = _aiProvider;
            if (provider == null)
            {
                return selected;
            }

            return await provider.RewriteTextAsync(selected, fullInstruction);
        }
    }

    private void CancelPrefetch()
    {
        try
        {
            _prefetchCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _prefetchCts?.Dispose();
        _prefetchCts = null;
        lock (_prefetchLock)
        {
            _prefetch = [];
            _prefetchSelection = string.Empty;
            _prefetchOption = string.Empty;
        }
    }

    private async Task OnOptionSelected(string option)
    {
        if (_pickingTone)
        {
            ApplyToneOverride(option);
            return;
        }

        if (option == CorrectToneItem)
        {
            _pickingTone = true;
            var toneCaret = _focusTracker.GetCaretScreenPosition();
            _menu.ShowMenu(
                ["Use Casual for this app", "Use Formal for this app", "Use Code for this app", "Use Neutral for this app"],
                toneCaret.X,
                toneCaret.Y,
                "Set tone for this app");
            return;
        }

        var selected = _pendingSelection;
        var provider = _aiProvider;
        if (string.IsNullOrEmpty(selected) || provider == null)
        {
            return;
        }

        if (_accessPolicy != null && !_accessPolicy.AllowsRewrite)
        {
            NotifyBlocked("AI rewrites are turned off in Settings.");
            return;
        }

        var context = _pendingContext ?? Enrich(_focusTracker.GetCurrentContext());
        if (_privacyGuard.ShouldBlockAssistance(context))
        {
            NotifyBlocked(PrivacyGuard.RewriteBlockedMessage);
            return;
        }

        if (Options.Contains(option))
        {
            _profile.SetSetting("LastRewriteOption", option);
            _ = _profile.SaveAsync();
        }

        var instruction = InstructionFor(option);
        var fullInstruction = string.IsNullOrEmpty(_pendingExtras)
            ? instruction
            : _pendingExtras + "\n" + instruction;

        try
        {
            _rewriteCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _rewriteCts?.Dispose();
        _rewriteCts = new CancellationTokenSource();
        var ct = _rewriteCts.Token;

        _pendingCategory = context.AppCategory;
        _pendingStyle = string.IsNullOrEmpty(context.WritingStyleHint) ? "default" : "styled";
        var caret = _focusTracker.GetCaretScreenPosition();
        var trustKey = $"{context.ApplicationName}|{option}";
        NoteCloudSend(context, "rewrite");

        _confirmation.BeginLiveRewrite(selected, caret.X, caret.Y, after =>
        {
            var next = after.Trim();
            if (string.IsNullOrWhiteSpace(next) || next == selected)
            {
                return;
            }

            _textInjector.ReplaceSelection(next);
            _undoManager.RecordOperation(selected, next);
            _personalization.RecordCharactersInserted(next.Length);
            _personalization.RecordRewriteOutcome(_pendingCategory, _pendingStyle, true);
        }, () =>
        {
            try
            {
                _rewriteCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _personalization.RecordRewriteOutcome(_pendingCategory, _pendingStyle, false);
        }, trustKey);

        var progress = new LiveRewriteProgress(partial => _confirmation.UpdateLiveRewrite(partial, false));
        try
        {
            Task<string>? existing = null;
            lock (_prefetchLock)
            {
                _prefetch.TryGetValue(option, out existing);
            }

            string rewritten;
            if (existing != null)
            {
                rewritten = await existing;
                if (!string.IsNullOrEmpty(rewritten))
                {
                    progress.Report(rewritten);
                }
            }
            else
            {
                rewritten = await provider.RewriteTextAsync(selected, fullInstruction, ct, progress);
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            var next = (rewritten ?? string.Empty).Trim();
            _confirmation.UpdateLiveRewrite(string.IsNullOrWhiteSpace(next) ? selected : next, true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed class LiveRewriteProgress : IProgress<string>
    {
        private readonly Action<string> _emit;

        public LiveRewriteProgress(Action<string> emit) => _emit = emit;

        public void Report(string value) => _emit(value);
    }

    private void ApplyToneOverride(string option)
    {
        _pickingTone = false;
        var category = option switch
        {
            "Use Casual for this app" => AppWritingCategory.Casual,
            "Use Formal for this app" => AppWritingCategory.Formal,
            "Use Code for this app" => AppWritingCategory.Code,
            _ => AppWritingCategory.Neutral
        };

        var app = AppCategoryMapper.Normalize(_focusTracker.GetCurrentContext().ApplicationName);
        if (string.IsNullOrEmpty(app))
        {
            return;
        }

        var rows = _profile.GetSetting<List<string>>("AppCategoryOverrides", []) ?? [];
        rows.RemoveAll(row => AppCategoryMapper.TryParseRow(row, out var existing, out _) && existing == app);
        rows.Add(AppCategoryMapper.FormatRow(app, category));
        _profile.SetSetting("AppCategoryOverrides", rows);
        _ = _profile.SaveAsync();
    }

    public void CancelPending()
    {
        CancelPrefetch();
        try
        {
            _rewriteCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void SetProvider(IAIProvider? provider)
    {
        var previous = _aiProvider;
        if (!ReferenceEquals(previous, provider))
        {
            CancelPending();
        }

        _aiProvider = provider;
    }

    internal Task<string> AwaitRewriteForTests(Task<string> existing, string selected)
        => AwaitRewrite(existing, selected, "test");

    public TextContext Enrich(TextContext context)
    {
        var overrides = AppCategoryMapper.ParseOverrides(_profile.GetSetting<List<string>>("AppCategoryOverrides", []));
        context.AppCategory = AppCategoryMapper.Resolve(context.ApplicationName, overrides);
        if (_personalization.ShouldAvoidStyle(context.AppCategory))
        {
            context.AppCategory = AppWritingCategory.Neutral;
            context.AppToneHint = string.Empty;
        }
        else
        {
            context.AppToneHint = AppCategoryMapper.ToneHint(context.AppCategory);
        }

        context.WritingStyleHint = _personalization.GetStyleHint(context.AppCategory);
        return context;
    }

    private void NotifyBlocked(string message)
    {
        var caret = _focusTracker.GetCaretScreenPosition();
        if (_glance != null)
        {
            _glance.ShowGlance(message, caret.X, caret.Y);
            return;
        }

        _menu.ShowMenu([message], caret.X, caret.Y);
    }

    private void NoteCloudSend(TextContext context, string action)
    {
        var provider = _aiProvider;
        if (!CloudAiNames.RequiresTypingConsent(provider?.Name, provider?.NetworkEndpoint))
        {
            return;
        }

        _aiLog?.Record(provider?.Name, context.ApplicationName, action);
    }
}
