using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace Lexon.Input;

/// <summary>
/// Resolves the on-screen caret / current-word box via UI Automation.
/// Must run on an STA thread. Chrome's collapsed caret range usually has
/// an empty bounding rect — expand by one character to get a real box.
/// Never use the first TextPattern in the Chrome window: that is often the
/// document, whose box sits at the bottom of the page.
/// </summary>
internal static class UiaScreenAnchor
{
    public static int LastLineHeight { get; private set; } = 20;

    public static bool TryGetWordAnchor(string? currentWord, out int x, out int y)
        => TryGetWordAnchor(currentWord, IntPtr.Zero, out x, out y);

    public static bool TryGetWordAnchor(
        string? currentWord,
        IntPtr contentHwnd,
        out int x,
        out int y,
        bool constrainToComposer = false,
        int charsBeforeWord = 0)
    {
        x = 0;
        y = 0;
        try
        {
            var result = StaInvoker.Invoke(
                () => TryGetWordAnchorSta(currentWord, contentHwnd, constrainToComposer, charsBeforeWord),
                timeoutMs: 800);
            if (result is not { } point)
            {
                return false;
            }

            LastLineHeight = Math.Clamp(point.LineHeight, 14, 64);
            x = point.X;
            y = point.Y;
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.WritePlacement($"UIA anchor failed: {ex.GetType().Name} {ex.Message}");
        }

        return false;
    }

    private static (int X, int Y, int LineHeight)? TryGetWordAnchorSta(
        string? currentWord,
        IntPtr contentHwnd,
        bool constrainToComposer,
        int charsBeforeWord)
    {
        var hwnd = GetForegroundWindow();
        if (GetWindowClassName(hwnd).Equals("LexonSuggestionOverlay", StringComparison.Ordinal)
            || GetWindowClassName(hwnd).Equals("LexonGrammarOverlay", StringComparison.Ordinal))
        {
            return null;
        }

        GetWindowRect(hwnd, out var window);
        var content = window;
        if (contentHwnd != IntPtr.Zero)
        {
            GetWindowRect(contentHwnd, out content);
        }

        (int X, int Y, int LineHeight)? caretScreen = null;
        var element = FindFocusedTextElement(contentHwnd != IntPtr.Zero ? contentHwnd : hwnd);
        if (element != null
            && element.TryGetCurrentPattern(TextPattern.Pattern, out var raw)
            && raw is TextPattern text)
        {
            var caret = GetCaretRange(text);
            if (caret == null)
            {
                DiagnosticLog.WritePlacement("UIA GetSelection empty");
            }
            else if (!string.IsNullOrEmpty(currentWord)
                && TryWordStart(caret, currentWord, out var wordRect)
                && IsPlausibleGlyphRect(wordRect, window, allowWide: true)
                && TryMapToScreen(wordRect, hwnd, window, out var wordScreen)
                && IsInsideWindow(wordScreen.X, wordScreen.Y, content))
            {
                caretScreen = wordScreen;
            }
            else if (TryCaretRect(caret, currentWord, out var caretRect)
                && IsPlausibleGlyphRect(caretRect, window, allowWide: false)
                && TryMapToScreen(caretRect, hwnd, window, out var mappedCaret)
                && IsInsideWindow(mappedCaret.X, mappedCaret.Y, content))
            {
                caretScreen = mappedCaret;
            }
            else
            {
                DiagnosticLog.WritePlacement("UIA rect rejected (document-sized or outside window)");
            }
        }
        else
        {
            DiagnosticLog.WritePlacement("UIA no focused TextPattern");
        }

        if (!constrainToComposer)
        {
            return caretScreen;
        }

        if (!TryFindComposer(hwnd, window, out var composer))
        {
            return null;
        }

        var chosen = caretScreen is { } hit
            ? CaretAnchorPolicy.ChooseComposerAnchor(
                (int)composer.X,
                (int)composer.Y,
                (int)(composer.X + composer.Width),
                (int)(composer.Y + composer.Height),
                charsBeforeWord,
                (hit.X, hit.Y))
            : CaretAnchorPolicy.ChooseComposerAnchor(
                (int)composer.X,
                (int)composer.Y,
                (int)(composer.X + composer.Width),
                (int)(composer.Y + composer.Height),
                charsBeforeWord);
        if (chosen == null)
        {
            return null;
        }

        var line = Math.Clamp((int)Math.Round(composer.Height), 14, 64);
        return (chosen.Value.X, chosen.Value.Y, line);
    }

    private static long _composerTicks;
    private static IntPtr _composerHwnd;
    private static Rect _composerRect;

    private static bool TryFindComposer(IntPtr hwnd, RECT window, out Rect composer)
    {
        var now = Environment.TickCount64;
        if (_composerHwnd == hwnd && now - _composerTicks < 500 && _composerRect.Height > 0)
        {
            composer = _composerRect;
            return true;
        }

        try
        {
            var focused = AutomationElement.FocusedElement;
            for (var depth = 0; depth < 8 && focused != null; depth++)
            {
                if (IsComposerEdit(focused, window, out composer))
                {
                    RememberComposer(hwnd, composer);
                    return true;
                }

                var parent = TreeWalker.ControlViewWalker.GetParent(focused);
                if (parent != null)
                {
                    var childEdit = parent.FindFirst(
                        TreeScope.Children,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                    if (childEdit != null && IsComposerEdit(childEdit, window, out composer))
                    {
                        RememberComposer(hwnd, composer);
                        return true;
                    }
                }

                focused = parent;
            }
        }
        catch
        {
            // Accessibility tree may be mid-update.
        }

        if (_composerHwnd == hwnd && _composerRect.Height > 0)
        {
            composer = _composerRect;
            return true;
        }

        composer = Rect.Empty;
        return false;
    }

    private static void RememberComposer(IntPtr hwnd, Rect composer)
    {
        _composerHwnd = hwnd;
        _composerRect = composer;
        _composerTicks = Environment.TickCount64;
    }

    private static bool IsComposerEdit(AutomationElement node, RECT window, out Rect rect)
    {
        rect = Rect.Empty;
        try
        {
            if (node.Current.ControlType != ControlType.Edit)
            {
                return false;
            }

            rect = node.Current.BoundingRectangle;
            if (rect.IsEmpty || double.IsNaN(rect.X) || double.IsNaN(rect.Y))
            {
                return false;
            }

            if (rect.Height < 16 || rect.Height > 180 || rect.Width < 40)
            {
                return false;
            }

            var windowHeight = window.bottom - window.top;
            if (windowHeight > 80 && rect.Height > windowHeight * 0.35)
            {
                return false;
            }

            return IsInsideWindow((int)Math.Round(rect.X), (int)Math.Round(rect.Y), window);
        }
        catch
        {
            return false;
        }
    }

    private static AutomationElement? FindFocusedTextElement(IntPtr hwnd)
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            for (var node = focused; node != null; node = TreeWalker.ControlViewWalker.GetParent(node))
            {
                if (HasTextPattern(node))
                {
                    return node;
                }
            }

            foreach (var rootHandle in EnumerateSearchRoots(hwnd))
            {
                var root = AutomationElement.FromHandle(rootHandle);
                if (root == null)
                {
                    continue;
                }

                var focusedText = root.FindFirst(
                    TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.HasKeyboardFocusProperty, true),
                        new PropertyCondition(AutomationElement.IsTextPatternAvailableProperty, true)));
                if (focusedText != null)
                {
                    return focusedText;
                }

                var focusedEdit = root.FindFirst(
                    TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.HasKeyboardFocusProperty, true),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));
                if (focusedEdit != null && HasTextPattern(focusedEdit))
                {
                    return focusedEdit;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<IntPtr> EnumerateSearchRoots(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            yield return hwnd;
        }

        foreach (var child in FindChildrenByClass(hwnd, "Chrome_RenderWidgetHostHWND"))
        {
            yield return child;
        }

        foreach (var child in FindChildrenByClass(hwnd, "Intermediate D3D Window"))
        {
            yield return child;
        }
    }

    private static List<IntPtr> FindChildrenByClass(IntPtr root, string className)
    {
        var found = new List<IntPtr>();
        if (root == IntPtr.Zero)
        {
            return found;
        }

        EnumChildWindows(root, (child, _) =>
        {
            if (GetWindowClassName(child).Equals(className, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(child);
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static string GetWindowClassName(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(256);
        return GetClassName(hWnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : string.Empty;
    }

    private static bool HasTextPattern(AutomationElement element)
    {
        try
        {
            return (bool)element.GetCurrentPropertyValue(AutomationElement.IsTextPatternAvailableProperty);
        }
        catch
        {
            return false;
        }
    }

    private static TextPatternRange? GetCaretRange(TextPattern text)
    {
        try
        {
            var selection = text.GetSelection();
            if (selection is { Length: > 0 })
            {
                return selection[0];
            }
        }
        catch
        {
            // Chrome sometimes throws if a11y is still warming up.
        }

        return null;
    }

    private static bool TryWordStart(TextPatternRange caret, string word, out Rect rect)
    {
        rect = Rect.Empty;
        try
        {
            var wordRange = caret.Clone();
            wordRange.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -word.Length);
            if (TryNonEmptyRect(wordRange, out rect))
            {
                return true;
            }
        }
        catch
        {
            // Provider rejected the move.
        }

        return false;
    }

    private static bool TryCaretRect(TextPatternRange caret, string? word, out Rect rect)
    {
        rect = Rect.Empty;
        if (TryNonEmptyRect(caret, out rect))
        {
            ShiftLeft(ref rect, word);
            return true;
        }

        try
        {
            var next = caret.Clone();
            if (next.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 1) != 0
                && TryNonEmptyRect(next, out rect))
            {
                ShiftLeft(ref rect, word);
                return true;
            }

            var prev = caret.Clone();
            if (prev.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -1) != 0
                && TryNonEmptyRect(prev, out rect))
            {
                rect = new Rect(rect.X + rect.Width, rect.Y, 1, rect.Height);
                ShiftLeft(ref rect, word);
                return true;
            }
        }
        catch
        {
            // Ignore.
        }

        return false;
    }

    private static void ShiftLeft(ref Rect rect, string? word)
    {
        if (string.IsNullOrEmpty(word) || rect.Height <= 0)
        {
            return;
        }

        var estimated = word.Length * rect.Height * 0.52;
        rect = new Rect(Math.Max(0, rect.X - estimated), rect.Y, rect.Width, rect.Height);
    }

    private static bool TryNonEmptyRect(TextPatternRange range, out Rect rect)
    {
        rect = Rect.Empty;
        Rect[]? rects;
        try
        {
            rects = range.GetBoundingRectangles();
        }
        catch
        {
            return false;
        }

        if (rects == null || rects.Length == 0)
        {
            return false;
        }

        rect = rects[0];
        return !rect.IsEmpty && !double.IsNaN(rect.X) && !double.IsNaN(rect.Y) && rect.Height > 0;
    }

    private static bool IsPlausibleGlyphRect(Rect rect, RECT window, bool allowWide)
    {
        if (rect.Height < 8 || rect.Height > 72)
        {
            return false;
        }

        if (!allowWide && rect.Width > 120)
        {
            return false;
        }

        if (allowWide && rect.Width > 1600)
        {
            return false;
        }

        var windowHeight = window.bottom - window.top;
        if (windowHeight > 80 && rect.Height > windowHeight * 0.25)
        {
            return false;
        }

        return true;
    }

    private static bool TryMapToScreen(Rect rect, IntPtr hwnd, RECT window, out (int X, int Y, int LineHeight) screen)
    {
        var physical = ((int)Math.Round(rect.X), (int)Math.Round(rect.Y), HeightOf(rect));
        if (IsInsideWindow(physical.Item1, physical.Item2, window))
        {
            screen = physical;
            return true;
        }

        var dpi = hwnd == IntPtr.Zero ? 96 : GetDpiForWindow(hwnd);
        if (dpi > 96)
        {
            var scaled = (
                (int)Math.Round(rect.X * dpi / 96.0),
                (int)Math.Round(rect.Y * dpi / 96.0),
                Math.Max(14, (int)Math.Round(rect.Height * dpi / 96.0)));
            if (IsInsideWindow(scaled.Item1, scaled.Item2, window))
            {
                screen = scaled;
                return true;
            }
        }

        screen = default;
        return false;
    }

    internal static bool IsInsideWindow(int x, int y, RECT window, int slack = 48)
    {
        if (window.right <= window.left || window.bottom <= window.top)
        {
            return true;
        }

        return x >= window.left - slack
            && x <= window.right + slack
            && y >= window.top - slack
            && y <= window.bottom + slack;
    }

    private static int HeightOf(Rect rect) => Math.Max(14, (int)Math.Round(rect.Height));

    private static readonly IntPtr DpiAwarenessContextPerMonitorV2 = new(-4);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private static class StaInvoker
    {
        private static readonly BlockingCollection<Action> Queue = new();

        static StaInvoker()
        {
            var thread = new Thread(Pump)
            {
                IsBackground = true,
                Name = "Lexon.UIA.STA"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private static void Pump()
        {
            SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorV2);
            try
            {
                _ = AutomationElement.RootElement;
            }
            catch
            {
                // Accessibility tree may not be ready yet.
            }

            foreach (var action in Queue.GetConsumingEnumerable())
            {
                action();
            }
        }

        public static T? Invoke<T>(Func<T> func, int timeoutMs = 250)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Queue.Add(() =>
            {
                try
                {
                    tcs.TrySetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            if (!tcs.Task.Wait(timeoutMs))
            {
                return default;
            }

            return tcs.Task.GetAwaiter().GetResult();
        }
    }
}
