using Lexon.Core.Grammar;
using Lexon.Core.Interfaces;
using Lexon.Core.Learning;
using Lexon.Core.Models;
using Lexon.Input.Interfaces;
using Lexon.Overlay.Interfaces;
using Lexon.Input;
using System.Runtime.InteropServices;

namespace Lexon.Service;

/// <summary>
/// Background service orchestrator that coordinates all Lexon components
/// </summary>
public class LexonService
{
    private readonly ISuggestionPipeline _suggestionPipeline;
    private readonly IKeyboardListener _keyboardListener;
    private readonly IFocusTracker _focusTracker;
    private readonly ISuggestionOverlay _suggestionOverlay;
    private readonly ISuggestionOverlay? _grammarOverlay;
    private readonly IPrivacyGuard _privacyGuard;
    private readonly ITextInjector _textInjector;
    private readonly Core.Expansion.TextExpansionManager _textExpansionManager;
    private readonly KeyboardShortcutManager _keyboardShortcutManager;
    private readonly UndoManager _undoManager;
    private readonly Func<bool> _autoCorrectEnabled;
    private readonly PersonalizationManager? _personalization;
    private SelectionRewriteService? _rewrite;
    private GrammarCheckService? _grammar;
    private MouseListener? _mouseListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _suppressSuggestionOverlay;
    private bool _isOverlayVisible = false;
    private long _suggestionGeneration = 0;

    // Win32 API declarations for proper virtual-key to character conversion
    [DllImport("user32.dll")]
    private static extern int GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern int ToUnicode(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out] char[] pwszBuff,
        int cchBuff,
        uint wFlags);

    // Diagnostic-only: identify exactly which window has keyboard focus at
    // the moment of injection, to check for a focus/timing mismatch.
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    private static string DescribeForegroundWindow()
    {
        try
        {
            var hWnd = GetForegroundWindow();
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return $"hwnd=0x{hWnd:X} title=\"{sb}\"";
        }
        catch
        {
            return "hwnd=<error>";
        }
    }

    public LexonService(
        ISuggestionPipeline suggestionPipeline,
        IKeyboardListener keyboardListener,
        IFocusTracker focusTracker,
        ISuggestionOverlay suggestionOverlay,
        IPrivacyGuard privacyGuard,
        ITextInjector textInjector,
        Core.Expansion.TextExpansionManager textExpansionManager,
        KeyboardShortcutManager keyboardShortcutManager,
        UndoManager undoManager,
        ISuggestionOverlay? grammarOverlay = null,
        Func<bool>? autoCorrectEnabled = null,
        PersonalizationManager? personalization = null)
    {
        _suggestionPipeline = suggestionPipeline ?? throw new ArgumentNullException(nameof(suggestionPipeline));
        _keyboardListener = keyboardListener ?? throw new ArgumentNullException(nameof(keyboardListener));
        _focusTracker = focusTracker ?? throw new ArgumentNullException(nameof(focusTracker));
        _suggestionOverlay = suggestionOverlay ?? throw new ArgumentNullException(nameof(suggestionOverlay));
        _grammarOverlay = grammarOverlay;
        _privacyGuard = privacyGuard ?? throw new ArgumentNullException(nameof(privacyGuard));
        _textInjector = textInjector ?? throw new ArgumentNullException(nameof(textInjector));
        _textExpansionManager = textExpansionManager ?? throw new ArgumentNullException(nameof(textExpansionManager));
        _keyboardShortcutManager = keyboardShortcutManager ?? throw new ArgumentNullException(nameof(keyboardShortcutManager));
        _undoManager = undoManager ?? throw new ArgumentNullException(nameof(undoManager));
        _autoCorrectEnabled = autoCorrectEnabled ?? (() => false);
        _personalization = personalization;

        // Wire up event handlers
        _keyboardListener.KeyPressed += OnKeyPressed;
        _focusTracker.ContextChanged += OnContextChanged;
        _suggestionOverlay.SuggestionSelected += OnSuggestionSelected;
        _suggestionOverlay.SuggestionDismissed += OnSuggestionDismissed;
        if (_grammarOverlay != null)
        {
            _grammarOverlay.SuggestionSelected += OnSuggestionSelected;
            _grammarOverlay.SuggestionDismissed += OnSuggestionDismissed;
        }
        _textExpansionManager.ExpansionTriggered += OnExpansionTriggered;
    }

    private void OnGrammarSuggestionsOffered(IReadOnlyList<Suggestion> suggestions)
    {
        if (suggestions == null || suggestions.Count == 0)
        {
            return;
        }

        DisplaySuggestions(suggestions.ToList());
    }

    public void AttachWritingEnhancement(SelectionRewriteService rewrite, GrammarCheckService grammar, MouseListener mouseListener)
    {
        _rewrite = rewrite;
        _grammar = grammar;
        _mouseListener = mouseListener;
        _grammar.SuggestionsOffered += OnGrammarSuggestionsOffered;
        _mouseListener.RightButtonDown += (_, args) =>
        {
            var x = args.X;
            var y = args.Y;
            // Never run UIA or show UI on the low-level hook thread.
            ThreadPool.QueueUserWorkItem(_ => _rewrite?.ShowRewriteMenuAt(x, y));
        };
        _mouseListener.LeftButtonUp += (_, args) =>
        {
            var x = args.X;
            var y = args.Y;
            _focusTracker.NotePointerScreenPosition(x, y);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(40);
                _rewrite?.ConsiderSelectionAffordance();
            });
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start input listeners
        _keyboardListener.Start();
        _focusTracker.Start();
        _mouseListener?.Start();
        ThreadPool.QueueUserWorkItem(_ => PrefetchFirstLetterSuggestions());

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource?.Cancel();

        // Stop input listeners
        _keyboardListener.Stop();
        _focusTracker.Stop();
        _mouseListener?.Stop();

        // Hide and dispose overlay
        _suggestionOverlay.Hide();
        _suggestionOverlay.Dispose();

        await Task.CompletedTask;
    }

    private TextContext _currentContext = new();
    private string _activeSuggestionPrefix = string.Empty;
    private List<string> _lastOfferedCompletions = new();
    private List<Suggestion> _lastFetchedSuggestions = new();
    private readonly Dictionary<char, List<Suggestion>> _letterPrefetch = new();
    private readonly object _letterPrefetchLock = new();
    private int _lastCaretX;
    private int _lastCaretY;
    private int _frozenLineHeight = 20;
    private bool _anchorLocked;
    private static readonly TimeSpan UndoLearnWindow = TimeSpan.FromSeconds(3);

    private void QueueOffHook(Action action)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"QueueOffHook: {ex}");
            }
        });
    }

    private static bool IsWordBoundary(char character)
        => SuggestionInsertion.IsWordSeparator(character);

    /// <summary>
    /// Converts a virtual key code to the actual character, respecting keyboard state (Shift, CapsLock, etc.)
    /// </summary>
    private char? VirtualKeyToCharacter(uint virtualKey, bool isShiftPressed)
    {
        byte[] keyState = new byte[256];
        GetKeyboardState(keyState);

        // Set shift state based on the event
        const int VK_SHIFT = 0x10;
        const int VK_LSHIFT = 0xA0;
        const int VK_RSHIFT = 0xA1;
        
        if (isShiftPressed)
        {
            keyState[VK_SHIFT] = 0x80;
            keyState[VK_LSHIFT] = 0x80;
            keyState[VK_RSHIFT] = 0x80;
        }

        char[] charBuffer = new char[2];
        int result = ToUnicode(virtualKey, 0, keyState, charBuffer, charBuffer.Length, 0);

        if (result == 1)
        {
            return charBuffer[0];
        }

        return null;
    }

    /// <summary>
    /// Gets the current caret screen position from the focused application.
    /// </summary>
    private (int x, int y) GetCaretPosition()
    {
        try
        {
            return _focusTracker.GetCaretScreenPosition();
        }
        catch
        {
            return (100, 100);
        }
    }

    private (int x, int y) GetWordAnchorPosition()
    {
        try
        {
            return _focusTracker.GetWordAnchorScreenPosition(_currentContext.CurrentWord);
        }
        catch
        {
            return GetCaretPosition();
        }
    }

    private void OnContextChanged(object? sender, TextContext context)
    {
        // Before switching, feed whatever the user finished typing in the
        // previous field into writing-style learning — skip secure fields,
        // which never reach personalization.
        var previousContext = _currentContext;
        var windowChanged = !string.Equals(previousContext.ApplicationName, context.ApplicationName, StringComparison.Ordinal)
            || !string.Equals(previousContext.WindowTitle, context.WindowTitle, StringComparison.Ordinal);
        if (windowChanged && !previousContext.IsPasswordField && !string.IsNullOrWhiteSpace(previousContext.FullText))
        {
            _suggestionPipeline.LearnWritingStyle(previousContext.FullText, previousContext);
        }

        _currentContext = context;

        if (_privacyGuard.IsApplicationBlocked(context.ApplicationName))
        {
            HideSuggestions();
            return;
        }

        if (!HasInProgressWord() && !IsShowingPinnedSuggestion())
        {
            HideSuggestions();
        }
    }

    private void OnKeyPressed(object? sender, Input.Interfaces.KeyboardEventArgs e)
    {
        if (_rewrite?.TryHandleKey(e) == true || _grammar?.TryHandleKey(e) == true)
        {
            return;
        }

        // Tab must be decided inside the hook callback (set Handled) before any slow work.
        // Otherwise Windows delivers Tab to the editor and you get indentation spaces.
        if (e.VirtualKey == 9 && !e.IsShiftPressed && !e.IsControlPressed && !e.IsAltPressed)
        {
            if (SuggestionListVisible())
            {
                e.Handled = true;
                QueueOffHook(AcceptFromTab);
                return;
            }
        }

        if (TryHandlePredictionNumberKey(e))
        {
            return;
        }

        _keyboardShortcutManager.OnKeyPressed(e);
        if (e.Handled)
        {
            return;
        }

        if (e.VirtualKey == 32 && !e.IsControlPressed && !e.IsAltPressed)
        {
            _suppressSuggestionOverlay = false;
            // Space finishes the current word in the document. Never treat it
            // as accept — only Tab (and Enter while the list is focused) does that.
            _focusTracker.AddTypedCharacter(' ');
            _textExpansionManager.OnCharacterTyped(' ');
            Interlocked.Increment(ref _suggestionGeneration);
            HideSuggestions();
            var typed = _focusTracker.GetTypedBufferText();
            var offered = _lastOfferedCompletions.ToList();
            QueueOffHook(() =>
            {
                _suggestionPipeline.LearnWritingStyle(typed, _currentContext, offered);
                OnWordCompleted(typed);
            });
            _grammar?.NoteActivity();
            _grammar?.SchedulePauseCheck();
            return;
        }

        // Arrow VKs (38/40) sit inside 32–126, so overlay navigation must run
        // before the "printable character" branch or Up/Down are ignored.
        if (SuggestionListVisible()
            && !e.IsShiftPressed && !e.IsControlPressed && !e.IsAltPressed)
        {
            var navOverlay = NavigationOverlay();
            if (e.VirtualKey == 38)
            {
                e.Handled = true;
                navOverlay?.SelectPrevious();
                return;
            }

            if (e.VirtualKey == 40)
            {
                e.Handled = true;
                navOverlay?.SelectNext();
                return;
            }

            if (e.VirtualKey == 13)
            {
                e.Handled = true;
                QueueOffHook(AcceptFromTab);
                return;
            }
        }

        if (IsTypedCharacterKey(e.VirtualKey) && !e.IsControlPressed && !e.IsAltPressed)
        {
            var character = VirtualKeyToCharacter((uint)e.VirtualKey, e.IsShiftPressed);
            if (character.HasValue)
            {
                _suppressSuggestionOverlay = false;
                _focusTracker.AddTypedCharacter(character.Value);
                _textExpansionManager.OnCharacterTyped(character.Value);
                var generation = Interlocked.Increment(ref _suggestionGeneration);

                if (IsWordBoundary(character.Value))
                {
                    HideSuggestions();
                    var typed = _focusTracker.GetTypedBufferText();
                    var offered = _lastOfferedCompletions.ToList();
                    QueueOffHook(() =>
                    {
                        _suggestionPipeline.LearnWritingStyle(typed, _currentContext, offered);
                        OnWordCompleted(typed);
                    });
                }
                else
                {
                    QueueOffHook(() => _ = ShowSuggestionsAsync(generation));
                }
            }
        }
        else if (e.VirtualKey == 8) // Backspace
        {
            _suppressSuggestionOverlay = false;
            var typed = _focusTracker.GetTypedBufferText();
            var undoingBoundary = typed.Length > 0 && IsWordBoundary(typed[^1]);
            _focusTracker.AddTypedCharacter('\b');
            _textExpansionManager.OnCharacterTyped('\b');
            var generation = Interlocked.Increment(ref _suggestionGeneration);
            if (!HasInProgressWord())
            {
                HideSuggestionsIfNotPinned();
            }

            QueueOffHook(() =>
            {
                if (undoingBoundary)
                {
                    _suggestionPipeline.UndoLastLearn(UndoLearnWindow);
                }

                _ = ShowSuggestionsAsync(generation);
            });
        }
        else if (e.VirtualKey == 27)
        {
            if (_isOverlayVisible || AnySuggestionOverlayVisible())
            {
                e.Handled = true;
            }
            HideSuggestions();
        }

        if (e.VirtualKey is 8 or 13 or 32 || IsTypedCharacterKey(e.VirtualKey))
        {
            _grammar?.NoteActivity();
            _grammar?.SchedulePauseCheck();
        }
    }

    private static bool IsTypedCharacterKey(int virtualKey)
    {
        if (virtualKey == 32)
        {
            return true;
        }

        // 33–40 are PageUp/PageDown/End/Home/arrows — not typed characters,
        // even though those VK codes overlap the ASCII range.
        if (virtualKey is >= 33 and <= 40 or 45 or 46)
        {
            return false;
        }

        return virtualKey is >= 48 and <= 90 or >= 186;
    }

    private bool TryHandlePredictionNumberKey(Input.Interfaces.KeyboardEventArgs e)
    {
        if (e.IsShiftPressed || e.IsControlPressed || e.IsAltPressed || !_suggestionOverlay.HasPredictions)
        {
            return false;
        }

        var index = e.VirtualKey switch
        {
            0x31 or 0x61 => 0,
            0x32 or 0x62 => 1,
            0x33 or 0x63 => 2,
            _ => -1
        };
        if (index < 0)
        {
            return false;
        }

        e.Handled = true;
        QueueOffHook(() => _suggestionOverlay.ConfirmPrediction(index));
        return true;
    }

    private void OnWordCompleted(string typed)
    {
        var generation = Interlocked.Read(ref _suggestionGeneration);
        TextContext context;
        try
        {
            context = _focusTracker.GetCurrentContext();
        }
        catch
        {
            context = _currentContext;
        }

        if (_privacyGuard.ShouldBlockAssistance(context))
        {
            return;
        }

        if (generation != Interlocked.Read(ref _suggestionGeneration))
        {
            return;
        }

        _currentContext = context;

        var latest = _focusTracker.GetTypedBufferText() ?? typed;
        if (SuggestionInsertion.LastCompletedWord(latest).Length == 0)
        {
            return;
        }

        if (TryApplyTypoAutoCorrect(latest, context))
        {
            latest = _focusTracker.GetTypedBufferText() ?? latest;
        }

        if (generation != Interlocked.Read(ref _suggestionGeneration))
        {
            return;
        }

        ShowNextWordPredictions(latest, context);
    }

    private bool TryApplyTypoAutoCorrect(string typed, TextContext context)
    {
        var enabled = _autoCorrectEnabled();
        var word = SuggestionInsertion.LastCompletedWord(typed);
        if (!TypoAutoCorrect.TryGetCorrection(word, enabled, _suggestionPipeline.GetLearnedWords(), out var correction))
        {
            return false;
        }

        var separator = typed[^1];
        var (deleteCount, insertText) = TypoAutoCorrect.GetEdit(word, correction, separator);

        for (var i = 0; i < deleteCount; i++)
        {
            _focusTracker.AddTypedCharacter('\b');
        }

        foreach (var ch in insertText)
        {
            _focusTracker.AddTypedCharacter(ch);
        }

        _textInjector.DeleteBackward(deleteCount);
        _textInjector.InjectText(insertText);

        var (x, y) = GetWordAnchorPosition();
        _suggestionOverlay.FlashCorrection(correction, x, y, OverlayLineHeight());
        return true;
    }

    private void ShowNextWordPredictions(string typed, TextContext context)
    {
        if (_personalization == null)
        {
            return;
        }

        var previous = SuggestionInsertion.LastCompletedWord(typed);
        var words = _personalization.GetTopFollowers(previous, 3);
        if (words.Count == 0)
        {
            return;
        }

        var (x, y) = GetWordAnchorPosition();
        _suggestionOverlay.ShowPredictions(
            new PredictedFollowers { PreviousWord = previous, Words = words },
            x,
            y,
            OverlayLineHeight());
        _isOverlayVisible = AnySuggestionOverlayVisible();
    }

    private void AcceptFromTab()
    {
        if (_grammarOverlay is { IsVisible: true })
        {
            _suggestionOverlay.Hide();
            _grammarOverlay.ConfirmSelection();
            return;
        }

        if (!_suggestionOverlay.IsVisible)
        {
            return;
        }

        _suggestionOverlay.ConfirmSelection();
        _isOverlayVisible = false;
    }

    private bool AnySuggestionOverlayVisible()
        => _suggestionOverlay.IsVisible || _grammarOverlay is { IsVisible: true };

    /// <summary>
    /// True when Tab/Enter/arrows should target the suggestion or grammar list.
    /// Next-word chips are accepted with 1/2/3 or click, not Tab.
    /// </summary>
    private bool SuggestionListVisible()
        => _grammarOverlay is { IsVisible: true }
           || (_suggestionOverlay.IsVisible && !_suggestionOverlay.HasPredictions);

    private ISuggestionOverlay? NavigationOverlay()
    {
        if (_suggestionOverlay.IsVisible)
        {
            return _suggestionOverlay;
        }

        return _grammarOverlay is { IsVisible: true } ? _grammarOverlay : null;
    }

    private async Task ShowSuggestionsAsync(long generation)
    {
        if (_suppressSuggestionOverlay)
        {
            return;
        }
        if (!_suggestionPipeline.IsEnabled)
        {
            HideSuggestions();
            return;
        }

        if (generation != Interlocked.Read(ref _suggestionGeneration))
        {
            return;
        }

        if (!HasInProgressWord())
        {
            HideSuggestionsIfNotPinned();
            return;
        }

        var typedPrefix = SuggestionInsertion.CurrentToken(_focusTracker.GetTypedBufferText());
        if (typedPrefix.Length >= 1)
        {
            ShowInstantFilter(typedPrefix);

            var bufferContext = ContextFromTypedBuffer(typedPrefix);
            var quickSuggestions = (await _suggestionPipeline.GetSuggestionsAsync(
                bufferContext,
                _cancellationTokenSource?.Token ?? default)).ToList();
            if (generation != Interlocked.Read(ref _suggestionGeneration) || !HasInProgressWord())
            {
                if (generation == Interlocked.Read(ref _suggestionGeneration) && !HasInProgressWord())
                {
                    HideSuggestionsIfNotPinned();
                }
                return;
            }

            if (quickSuggestions.Count > 0)
            {
                _currentContext = bufferContext;
                _activeSuggestionPrefix = typedPrefix;
                RememberLetterPrefetch(typedPrefix, quickSuggestions);
                DisplaySuggestions(quickSuggestions);
            }
            else
            {
                DisplaySuggestions([]);
            }
        }

        if (!HasInProgressWord())
        {
            if (generation == Interlocked.Read(ref _suggestionGeneration))
            {
                HideSuggestionsIfNotPinned();
            }

            return;
        }

        if (generation != Interlocked.Read(ref _suggestionGeneration))
        {
            return;
        }

        if (_privacyGuard.IsSecureField(_currentContext) ||
            _privacyGuard.IsApplicationBlocked(_currentContext.ApplicationName))
        {
            HideSuggestions();
            return;
        }

        typedPrefix = SuggestionInsertion.CurrentToken(_focusTracker.GetTypedBufferText());
        if (typedPrefix.Length > 0)
        {
            _currentContext.CurrentWord = typedPrefix;
        }

        if (!HasInProgressWord())
        {
            HideSuggestionsIfNotPinned();
            return;
        }

        var word = typedPrefix.Length > 0 ? typedPrefix : (_currentContext.CurrentWord ?? string.Empty);
        if (word.Length < 1)
        {
            var grammarOnly = GrammarFixesFromTypedBuffer();
            if (grammarOnly.Count > 0)
            {
                DisplaySuggestions(grammarOnly);
                return;
            }

            HideSuggestionsIfNotPinned();
            return;
        }

        _activeSuggestionPrefix = word;
        var suggestions = (await _suggestionPipeline.GetSuggestionsAsync(
            _currentContext,
            _cancellationTokenSource?.Token ?? default)).ToList();

        if (generation != Interlocked.Read(ref _suggestionGeneration) || !HasInProgressWord())
        {
            if (generation == Interlocked.Read(ref _suggestionGeneration) && !HasInProgressWord())
            {
                HideSuggestionsIfNotPinned();
            }
            return;
        }

        DisplaySuggestions(suggestions);

        var contextSnapshot = _currentContext;
        _ = MergeSupplementalSuggestionsAsync(generation, contextSnapshot);
    }

    private TextContext ContextFromTypedBuffer(string prefix)
    {
        var buffer = _focusTracker.GetTypedBufferText() ?? string.Empty;
        var before = buffer;
        if (prefix.Length > 0 && before.EndsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            before = before[..^prefix.Length];
        }

        var parts = before.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var previous = parts.Length == 0
            ? string.Empty
            : string.Join(" ", parts.Length > 12 ? parts[^12..] : parts);

        var ctx = new TextContext
        {
            CurrentWord = prefix,
            PreviousWords = previous,
            FullText = buffer,
            CursorPosition = buffer.Length,
            ApplicationName = _currentContext.ApplicationName,
            WindowTitle = _currentContext.WindowTitle
        };
        return _rewrite?.Enrich(ctx) ?? ctx;
    }

    private void PrefetchFirstLetterSuggestions()
    {
        try
        {
            for (var letter = 'a'; letter <= 'z'; letter++)
            {
                if (_cancellationTokenSource?.IsCancellationRequested == true)
                {
                    return;
                }

                var context = new TextContext { CurrentWord = letter.ToString() };
                var suggestions = _suggestionPipeline.GetSuggestionsAsync(context)
                    .GetAwaiter()
                    .GetResult()
                    .ToList();
                if (suggestions.Count == 0)
                {
                    continue;
                }

                lock (_letterPrefetchLock)
                {
                    _letterPrefetch[letter] = suggestions;
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"PrefetchFirstLetterSuggestions: {ex}");
        }
    }

    private void RememberLetterPrefetch(string prefix, List<Suggestion> suggestions)
    {
        if (prefix.Length != 1 || suggestions.Count == 0)
        {
            return;
        }

        var letter = char.ToLowerInvariant(prefix[0]);
        if (letter < 'a' || letter > 'z')
        {
            return;
        }

        lock (_letterPrefetchLock)
        {
            _letterPrefetch[letter] = suggestions;
        }
    }

    private List<Suggestion> InstantSourceFor(string prefix)
    {
        if (_lastFetchedSuggestions.Count > 0
            && _lastFetchedSuggestions.Any(s =>
                s.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Source, GrammarSuggestionMapper.Source, StringComparison.Ordinal)
                || string.Equals(s.Source, "Spelling", StringComparison.Ordinal)))
        {
            return _lastFetchedSuggestions;
        }

        if (prefix.Length == 0)
        {
            return _lastFetchedSuggestions;
        }

        var letter = char.ToLowerInvariant(prefix[0]);
        lock (_letterPrefetchLock)
        {
            if (_letterPrefetch.TryGetValue(letter, out var cached))
            {
                return cached;
            }
        }

        return _lastFetchedSuggestions;
    }

    private void ShowInstantFilter(string prefix)
    {
        if (_suppressSuggestionOverlay)
        {
            return;
        }
        if (!_suggestionPipeline.IsEnabled || prefix.Length < 1 || !HasInProgressWord())
        {
            HideSuggestionsIfNotPinned();
            return;
        }

        var grammarFixes = GrammarFixesFromTypedBuffer(prefix);
        var source = InstantSourceFor(prefix);
        if (source.Count == 0 && grammarFixes.Count == 0)
        {
            return;
        }

        var filtered = source
            .Where(s => s.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && !GrammarSuggestionMapper.IsGrammarFix(s))
            .ToList();
        if (filtered.Count == 0 && grammarFixes.Count == 0)
        {
            return;
        }

        var ranked = filtered.Count == 0
            ? filtered
            : _suggestionPipeline.Rerank(filtered, ContextFromTypedBuffer(prefix)).ToList();
        if (ranked.Count == 0)
        {
            ranked = filtered;
        }

        _activeSuggestionPrefix = prefix;
        _currentContext.CurrentWord = prefix;
        DisplaySuggestions(ranked.Concat(grammarFixes).ToList());
    }

    private List<Suggestion> GrammarFixesFromTypedBuffer(string? prefix = null)
    {
        if (_grammar != null && !_grammar.IsAutomaticEnabled)
        {
            return [];
        }

        prefix ??= SuggestionInsertion.CurrentToken(_focusTracker.GetTypedBufferText());
        var fromBuffer = GrammarSuggestionMapper.Suggest(ContextFromTypedBuffer(prefix ?? string.Empty));
        var fromLive = GrammarSuggestionMapper.Suggest(_currentContext);
        var merged = new List<Suggestion>();
        foreach (var fix in fromBuffer.Concat(fromLive))
        {
            if (!merged.Any(s => s.Text.Equals(fix.Text, StringComparison.OrdinalIgnoreCase)))
            {
                merged.Add(fix);
            }
        }

        return merged;
    }

    private void DisplaySuggestions(List<Suggestion> suggestions)
    {
        if (_suppressSuggestionOverlay)
        {
            return;
        }

        foreach (var fix in GrammarFixesFromTypedBuffer())
        {
            if (!suggestions.Any(s => GrammarSuggestionMapper.IsGrammarFix(s)
                && s.Text.Equals(fix.Text, StringComparison.OrdinalIgnoreCase)))
            {
                suggestions.Add(fix);
            }
        }

        GrammarSuggestionMapper.SplitFrom(suggestions, out var grammar, out var completions);
        if (completions.Count == 0 && grammar.Count == 0)
        {
            HideSuggestionsIfNotPinned();
            return;
        }

        if (!HasInProgressWord() && grammar.Count == 0
            && !completions.Any(s => string.Equals(s.Source, "Spelling", StringComparison.Ordinal)))
        {
            HideSuggestionsIfNotPinned();
            return;
        }

        _lastFetchedSuggestions = suggestions;
        _lastOfferedCompletions = completions.Select(s => s.Text).ToList();
        PresentOverlay(_suggestionOverlay, completions, freezeWhileTyping: true);
        PresentOverlay(_grammarOverlay, grammar, freezeWhileTyping: false, offsetX: 12);
        _isOverlayVisible = AnySuggestionOverlayVisible();
    }

    private void PresentOverlay(ISuggestionOverlay? overlay, List<Suggestion> items, bool freezeWhileTyping, int offsetX = 0)
    {
        if (overlay == null)
        {
            return;
        }

        if (items.Count == 0)
        {
            if (!(overlay.HasPredictions && !HasInProgressWord()))
            {
                overlay.Hide();
            }

            return;
        }

        var (anchorX, anchorY) = TrackWordAnchor();
        if (CaretAnchorPolicy.IsDummy(anchorX, anchorY) && overlay.IsVisible)
        {
            overlay.ReplaceSuggestions(items);
            return;
        }

        anchorX += offsetX;
        if (overlay.IsVisible)
        {
            var skipMove = freezeWhileTyping
                && CaretAnchorPolicy.FreezeOverlayWhileWordContinues(_currentContext.ApplicationName);
            if (!skipMove && !CaretAnchorPolicy.IsDummy(anchorX, anchorY))
            {
                overlay.MoveTo(anchorX, anchorY, OverlayLineHeight());
            }

            overlay.ReplaceSuggestions(items);
            return;
        }

        overlay.ShowSuggestions(items, anchorX, anchorY, OverlayLineHeight());
    }

    private int OverlayLineHeight()
    {
        var height = _focusTracker.LastAnchorLineHeight;
        if (height < 14)
        {
            height = 20;
        }

        if (_suggestionOverlay.IsVisible && _anchorLocked)
        {
            return _frozenLineHeight;
        }

        _frozenLineHeight = height;
        return height;
    }

    private (int x, int y) TrackWordAnchor()
    {
        var (x, y) = GetWordAnchorPosition();
        var freeze = CaretAnchorPolicy.FreezeOverlayWhileWordContinues(_currentContext.ApplicationName);

        if (CaretAnchorPolicy.IsDummy(x, y))
        {
            if (_lastCaretX != 0 || _lastCaretY != 0)
            {
                return (_lastCaretX, _lastCaretY);
            }

            return (x, y);
        }

        if (freeze && _anchorLocked && (_lastCaretX != 0 || _lastCaretY != 0))
        {
            return (_lastCaretX, _lastCaretY);
        }

        var line = Math.Max(14, _focusTracker.LastAnchorLineHeight);
        if (!freeze
            && _anchorLocked
            && (_lastCaretX != 0 || _lastCaretY != 0)
            && CaretAnchorPolicy.IsTeleport(_lastCaretX, _lastCaretY, x, y, line)
            && !CaretAnchorPolicy.IsTypingDrift(_lastCaretX, _lastCaretY, x, y, line))
        {
            return (_lastCaretX, _lastCaretY);
        }

        _lastCaretX = x;
        _lastCaretY = y;
        _anchorLocked = true;
        return (x, y);
    }

    private bool IsShowingPinnedSuggestion()
        => _grammarOverlay is { IsVisible: true };

    private bool HasInProgressWord()
    {
        var buffer = _focusTracker.GetTypedBufferText() ?? string.Empty;
        var bufferWord = SuggestionInsertion.CurrentToken(buffer);

        string before = string.Empty;
        var liveReadable = false;
        try
        {
            var ctx = _focusTracker.GetCurrentContext();
            before = SuggestionInsertion.TextBeforeCaret(ctx.FullText, ctx.CursorPosition);
            liveReadable = !string.IsNullOrEmpty(ctx.FullText);
        }
        catch
        {
            // Fall back to the keystroke buffer.
        }

        var liveWord = SuggestionInsertion.CurrentToken(before);

        if (liveReadable && liveWord.Length < 1)
        {
            if (bufferWord.Length == 0 || SuggestionInsertion.EndsWithWordSeparator(buffer))
            {
                return false;
            }

            var finishedWord = SuggestionInsertion.GetLastWord(before);
            if (finishedWord.Length > 0
                && string.Equals(bufferWord, finishedWord, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        return bufferWord.Length >= 1 || liveWord.Length >= 1;
    }

    private void HideSuggestionsIfNotPinned()
    {
        // Keep next-word chips after a completed word. Drop them as soon as
        // the next word is in progress so they never sit mid-token.
        if (_suggestionOverlay.HasPredictions && !HasInProgressWord())
        {
            return;
        }

        if (IsShowingPinnedSuggestion())
        {
            _suggestionOverlay.Hide();
            _isOverlayVisible = AnySuggestionOverlayVisible();
            return;
        }

        HideSuggestions();
    }

    private void HideSuggestions()
    {
        _anchorLocked = false;
        _suggestionOverlay.Hide();
        _grammarOverlay?.Hide();
        _isOverlayVisible = false;
    }

    private async Task MergeSupplementalSuggestionsAsync(long generation, TextContext context)
    {
        try
        {
            var extra = (await _suggestionPipeline.GetSupplementalSuggestionsAsync(
                context,
                _cancellationTokenSource?.Token ?? default)).ToList();
            if (extra.Count == 0
                || generation != Interlocked.Read(ref _suggestionGeneration)
                || !HasInProgressWord())
            {
                if (generation == Interlocked.Read(ref _suggestionGeneration) && !HasInProgressWord())
                {
                    HideSuggestionsIfNotPinned();
                }
                return;
            }

            var merged = _lastFetchedSuggestions
                .Concat(extra)
                .GroupBy(s => s.Text, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(s => s.Score).First())
                .OrderByDescending(s => s.Score)
                .Take(40)
                .ToList();
            DisplaySuggestions(merged);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"MergeSupplementalSuggestionsAsync: {ex}");
        }
    }

    private void OnSuggestionSelected(object? sender, Overlay.Interfaces.SuggestionSelectedEventArgs e)
    {
        var liveContext = _focusTracker.GetCurrentContext();
        var suggestionText = e.SelectedSuggestion.Text ?? string.Empty;
        var beforeCaret = SuggestionInsertion.TextBeforeCaret(liveContext.FullText, liveContext.CursorPosition);
        var typedBuffer = _focusTracker.GetTypedBufferText();
        _currentContext = liveContext;

        int deleteCount;
        string insertText;
        string undoOriginal;
        var isPrediction = string.Equals(e.SelectedSuggestion.Source, "Prediction", StringComparison.Ordinal);
        if (isPrediction)
        {
            // Next-word chips insert after a completed word. Never treat the
            // previous token as a prefix to replace.
            deleteCount = 0;
            insertText = suggestionText;
            if (!insertText.EndsWith(' '))
            {
                insertText += " ";
            }

            undoOriginal = string.Empty;
        }
        else if (TryGrammarReplacement(e.SelectedSuggestion, typedBuffer, beforeCaret, out deleteCount, out insertText, out undoOriginal))
        {
        }
        else
        {
            var prefix = SuggestionInsertion.ResolveInProgressWord(
                suggestionText,
                typedBuffer,
                beforeCaret,
                _activeSuggestionPrefix,
                liveContext.CurrentWord,
                _currentContext.CurrentWord);
            if (!string.IsNullOrEmpty(prefix))
            {
                _currentContext.CurrentWord = prefix;
            }

            (deleteCount, insertText) = SuggestionInsertion.GetReplacement(prefix, suggestionText);
            if (!insertText.EndsWith(' '))
            {
                insertText += " ";
            }

            undoOriginal = deleteCount > 0 ? prefix : string.Empty;
        }

        DiagnosticLog.Write($"ACCEPT: computed deleteCount={deleteCount} insertText={DiagnosticLog.Escape(insertText)}");

        _suppressSuggestionOverlay = true;
        Interlocked.Increment(ref _suggestionGeneration);
        _lastFetchedSuggestions.Clear();
        _grammar?.DismissAssistance();
        HideSuggestions();

        var grammarFix = GrammarSuggestionMapper.IsGrammarFix(e.SelectedSuggestion);
        var webEditor = WebEditorSupport.IsWebDocumentEditor(
            _currentContext.ApplicationName,
            _currentContext.WindowTitle,
            null);
        if (!isPrediction && (webEditor || grammarFix))
        {
            ApplyWebEditorAccept(e.SelectedSuggestion, typedBuffer, beforeCaret);
        }
        else
        {
            _undoManager.RecordOperation(undoOriginal, insertText);

            if (deleteCount > 0)
            {
                for (int i = 0; i < deleteCount; i++)
                {
                    _focusTracker.AddTypedCharacter('\b');
                }
            }

            if (!string.IsNullOrEmpty(insertText))
            {
                foreach (var ch in insertText)
                {
                    _focusTracker.AddTypedCharacter(ch);
                }
            }

            if (deleteCount > 0)
            {
                _textInjector.DeleteBackward(deleteCount);
            }

            if (!string.IsNullOrEmpty(insertText))
            {
                _textInjector.InjectText(insertText);
            }
        }

        if (!string.Equals(e.SelectedSuggestion.Source, GrammarSuggestionMapper.Source, StringComparison.Ordinal))
        {
            _suggestionPipeline.RecordInteraction(e.SelectedSuggestion, _currentContext, InteractionType.Accepted);
        }
    }

    private static bool TryGrammarReplacement(
        Suggestion suggestion,
        string? typedBuffer,
        string? beforeCaret,
        out int deleteCount,
        out string insertText,
        out string original)
    {
        deleteCount = 0;
        insertText = string.Empty;
        original = string.Empty;
        if (!GrammarSuggestionMapper.TryGetSpanReplacement(suggestion, out original, out var replacement))
        {
            return false;
        }

        var haystack = !string.IsNullOrEmpty(typedBuffer) ? typedBuffer : beforeCaret ?? string.Empty;
        if (SuggestionInsertion.TryReplaceTrailingPhrase(
            original, replacement, haystack, out deleteCount, out insertText))
        {
            return true;
        }

        return SuggestionInsertion.TryReplaceTrailingPhrase(
            original, replacement, beforeCaret, out deleteCount, out insertText);
    }

    /// <summary>
    /// Whether the text a grammar suggestion flagged is still at the trailing edge of
    /// what has been typed. False means typing continued after the suggestion was
    /// raised, so the flagged words are no longer where selecting backward by word
    /// count from the caret would land.
    /// </summary>
    private static bool IsGrammarFixStillApplicable(
        string? original,
        string? replacement,
        string? typedBuffer,
        string? beforeCaret)
    {
        var haystack = !string.IsNullOrEmpty(typedBuffer) ? typedBuffer : beforeCaret ?? string.Empty;
        return SuggestionInsertion.TryReplaceTrailingPhrase(original, replacement, haystack, out _, out _)
            || SuggestionInsertion.TryReplaceTrailingPhrase(original, replacement, beforeCaret, out _, out _);
    }

    private void ApplyWebEditorAccept(Suggestion suggestion, string? typedBuffer, string? beforeCaret)
    {
        string insert;
        string removed;
        int words;
        if (GrammarSuggestionMapper.TryGetSpanReplacement(suggestion, out var original, out var replacement))
        {
            // SelectBackwardWords takes the last N words before the caret and cannot
            // tell whether they are the flagged phrase. Grammar checking lags typing,
            // so by the time the suggestion is clicked the person has often typed on
            // and those N words are something else. Confirm the flagged text is still
            // trailing before selecting anything.
            if (!IsGrammarFixStillApplicable(original, replacement, typedBuffer, beforeCaret))
            {
                DiagnosticLog.Write(
                    $"ACCEPT web: declining stale grammar fix, original={DiagnosticLog.Escape(original)} is no longer trailing");
                return;
            }

            words = WebEditorSupport.CountWords(original);
            insert = replacement;
            removed = original;
        }
        else
        {
            words = 1;
            insert = suggestion.Text ?? string.Empty;
            removed = SuggestionInsertion.CurrentToken(typedBuffer);
            if (removed.Length == 0)
            {
                removed = SuggestionInsertion.CurrentToken(beforeCaret);
            }
        }

        if (insert.Length > 0 && !insert.EndsWith(' '))
        {
            insert += " ";
        }

        DiagnosticLog.Write($"ACCEPT web: selectWords={words} insert={DiagnosticLog.Escape(insert)}");
        _undoManager.RecordOperation(removed, insert);

        for (var i = 0; i < removed.Length; i++)
        {
            _focusTracker.AddTypedCharacter('\b');
        }

        foreach (var ch in insert)
        {
            _focusTracker.AddTypedCharacter(ch);
        }

        _textInjector.SelectBackwardWords(words);
        if (!string.IsNullOrEmpty(insert))
        {
            _textInjector.InjectText(insert);
        }
    }

    private void OnSuggestionDismissed(object? sender, Overlay.Interfaces.SuggestionDismissedEventArgs e)
    {
        // Suggestions were shown but none was picked (Esc, typed past them,
        // or focus moved away). Record each as ignored so personalization
        // doesn't keep re-surfacing suggestions the user consistently skips.
        foreach (var suggestion in e.DismissedSuggestions)
        {
            _suggestionPipeline.RecordInteraction(suggestion, _currentContext, InteractionType.Ignored);
        }
    }

    private void OnExpansionTriggered(object? sender, Core.Models.TextExpansionTriggeredEventArgs e)
    {
        // Record operation for undo
        _undoManager.RecordOperation(e.Expansion.Trigger, e.Expansion.Expansion);

        // Delete the trigger text
        _textInjector.DeleteBackward(e.Expansion.Trigger.Length);
        
        // Inject the expansion
        _textInjector.InjectText(e.Expansion.Expansion);
    }
}
