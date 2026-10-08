using Lexon.Core;
using Lexon.Core.Models;
using Lexon.Input.Interfaces;
using System.Runtime.InteropServices;
using System.Text;

namespace Lexon.Input;

/// <summary>
/// Tracks window focus and extracts text context using UI Automation
/// </summary>
public class FocusTracker : IFocusTracker
{
    private const int MaxTextBuffers = 50;
    private bool _isRunning = false;
    private System.Threading.Timer? _focusCheckTimer;
    private readonly Dictionary<uint, StringBuilder> _textBuffers = new();
    private readonly LinkedList<uint> _bufferAccessOrder = new();
    private IntPtr _lastWindowHandle = IntPtr.Zero;
    private readonly object _bufferLock = new();
    
    // UI Automation helper for reliable text reading
    private readonly UIAutomationHelper _uiAutomationHelper = new UIAutomationHelper();
    
    // Track when buffer was last modified to detect stale data
    private readonly Dictionary<uint, DateTime> _bufferLastModified = new();
    private int _estimateX;
    private int _estimateY;
    private bool _hasEstimate;

    public int LastAnchorLineHeight { get; private set; } = 20;

    public event EventHandler<TextContext>? ContextChanged;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint processAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll")]
    private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder baseName, uint size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetCaretPos(out POINT lpPoint);

    private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private static readonly IntPtr DpiAwarenessContextPerMonitorV2 = new(-4);

    private const uint WM_GETFONT = 0x0031;
    private const uint EM_POSFROMCHAR = 0x00D6;
    private const uint EM_POSFROMCHAR_RICHEDIT = 0x0426;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool GetTextExtentPoint32(IntPtr hdc, string text, int length, out SIZE size);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int fnObject);

    private const int DefaultGuiFont = 17;

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    /// <summary>
    /// Measures the on-screen pixel width of <paramref name="text"/> as it
    /// would render in <paramref name="hWnd"/>'s current font. Returns null
    /// if measurement isn't possible (falls back gracefully).
    /// </summary>
    private static int? MeasureTextWidth(IntPtr hWnd, string text)
    {
        if (string.IsNullOrEmpty(text) || hWnd == IntPtr.Zero) return null;

        var hdc = GetDC(hWnd);
        if (hdc == IntPtr.Zero) return null;

        try
        {
            var hFont = SendMessage(hWnd, WM_GETFONT, IntPtr.Zero, IntPtr.Zero);
            if (hFont == IntPtr.Zero)
            {
                hFont = GetStockObject(DefaultGuiFont);
            }

            var oldFont = hFont != IntPtr.Zero ? SelectObject(hdc, hFont) : IntPtr.Zero;
            try
            {
                if (GetTextExtentPoint32(hdc, text, text.Length, out var size))
                {
                    return size.cx;
                }
                return null;
            }
            finally
            {
                if (oldFont != IntPtr.Zero)
                {
                    SelectObject(hdc, oldFont);
                }
            }
        }
        finally
        {
            ReleaseDC(hWnd, hdc);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const int GwlStyle = -16;
    private const int EsPassword = 0x0020;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public int rcCaretLeft;
        public int rcCaretTop;
        public int rcCaretRight;
        public int rcCaretBottom;
    }

    private IntPtr GetEditorForegroundWindow()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd != IntPtr.Zero && !IsLexonOverlay(hWnd))
        {
            return hWnd;
        }

        if (_lastWindowHandle != IntPtr.Zero && !IsLexonOverlay(_lastWindowHandle))
        {
            return _lastWindowHandle;
        }

        return IntPtr.Zero;
    }

    private static bool IsLexonOverlay(IntPtr hWnd)
    {
        var name = GetWindowClassName(hWnd);
        return name.Equals("LexonSuggestionOverlay", StringComparison.Ordinal)
            || name.Equals("LexonGrammarOverlay", StringComparison.Ordinal);
    }

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _focusCheckTimer = new System.Threading.Timer(CheckFocus, null, 0, 100); // Check every 100ms
    }

    public void Stop()
    {
        _isRunning = false;
        _focusCheckTimer?.Dispose();
        ClearTypedBuffers();
    }

    public void ClearTypedBuffers()
    {
        lock (_bufferLock)
        {
            _textBuffers.Clear();
            _bufferAccessOrder.Clear();
            _bufferLastModified.Clear();
        }
    }

    public bool IsCurrentFieldSecure()
    {
        var hWnd = GetEditorForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return true;
        }

        return SecureFieldProbe.IsSecure(GetFocusedControlHandle(hWnd));
    }

    public string GetForegroundApplicationName()
    {
        var hWnd = GetEditorForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return string.Empty;
        }

        return ApplicationName.Normalize(GetProcessBaseName(hWnd));
    }

    public TextContext GetCurrentContext()
    {
        var hWnd = GetEditorForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return new TextContext { IsPasswordField = true };
        }

        var title = new StringBuilder(256);
        GetWindowText(hWnd, title, title.Capacity);

        var processName = ApplicationName.Normalize(GetProcessBaseName(hWnd));
        var controlHandle = GetFocusedControlHandle(hWnd);
        var isSecure = SecureFieldProbe.IsSecure(controlHandle);

        if (isSecure)
        {
            DiagnosticLog.Write($"GetCurrentContext: hwnd=0x{hWnd:X} process={processName} title=\"{title}\" secure=true (skipped text read)");
            return new TextContext
            {
                ApplicationName = processName,
                WindowTitle = title.ToString(),
                Timestamp = DateTime.UtcNow,
                IsPasswordField = true
            };
        }

        var textContext = ExtractTextContext(hWnd);
        textContext.IsPasswordField = false;
        textContext.ApplicationName = processName;
        textContext.WindowTitle = title.ToString();
        textContext.Timestamp = DateTime.UtcNow;

        DiagnosticLog.Write($"GetCurrentContext: hwnd=0x{hWnd:X} process={processName} title=\"{title}\" currentWord={DiagnosticLog.Escape(textContext.CurrentWord)}");

        return textContext;
    }

    public string GetTypedBufferText()
    {
        var hWnd = GetEditorForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return string.Empty;
        }

        lock (_bufferLock)
        {
            return GetOrCreateTextBuffer(hWnd).ToString();
        }
    }

    public bool TryGetSelectedText(out string selected)
    {
        selected = string.Empty;
        var info = _uiAutomationHelper.TryReadSelection();
        if (info == null || string.IsNullOrEmpty(info.Value.Text))
        {
            return false;
        }

        selected = info.Value.Text;
        return true;
    }

    public (int X, int Y) GetCaretScreenPosition()
    {
        var previousDpi = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorV2);
        try
        {
            return GetCaretScreenPositionCore();
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousDpi);
        }
    }

    private (int X, int Y) GetCaretScreenPositionCore()
        => GetWordAnchorScreenPositionCore(null);

    /// <summary>
    /// Screen position of the START of <paramref name="currentWord"/> at the
    /// top of the text line, so the overlay can sit above that word.
    /// </summary>
    public (int X, int Y) GetWordAnchorScreenPosition(string? currentWord)
    {
        var previousDpi = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorV2);
        try
        {
            return GetWordAnchorScreenPositionCore(currentWord);
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousDpi);
        }
    }

    private (int X, int Y) GetWordAnchorScreenPositionCore(string? currentWord)
    {
        var hWnd = GetEditorForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return EstimatedOrDummy(currentWord, "no-hwnd");
        }

        var title = new StringBuilder(256);
        GetWindowText(hWnd, title, title.Capacity);
        var processName = GetProcessBaseName(hWnd);
        var className = GetWindowClassName(hWnd);
        var codeEditor = WebEditorSupport.IsCodeEditorShell(processName);
        var webDoc = WebEditorSupport.IsWebDocumentEditor(processName, title.ToString(), className);
        var render = FindChildByClass(hWnd, "Chrome_RenderWidgetHostHWND");
        var content = render != IntPtr.Zero ? render : hWnd;
        GetWindowRect(hWnd, out var window);
        GetWindowRect(content, out var contentRect);

        bool Accept(int x, int y)
        {
            if (CaretAnchorPolicy.IsDummy(x, y) || !IsUsableScreenPoint(hWnd, x, y))
            {
                return false;
            }

            if ((webDoc || codeEditor) && !IsUsableScreenPoint(content, x, y))
            {
                return false;
            }

            return !webDoc || !CaretAnchorPolicy.IsLikelyBrowserChrome(y, window.top, contentRect.top);
        }

        (int X, int Y) Finish(int x, int y, int lineHeight, string via)
        {
            LastAnchorLineHeight = Math.Max(14, lineHeight);
            RememberEstimate(x, y);
            DiagnosticLog.WritePlacement(
                $"ANCHOR hwnd=0x{hWnd:X} class={className} process={processName} word={DiagnosticLog.Escape(currentWord)} finalScreen=({x},{y}) lineH={LastAnchorLineHeight} via={via}");
            return (x, y);
        }

        var threadId = GetWindowThreadProcessId(hWnd, out _);
        var currentThreadId = GetCurrentThreadId();
        var attached = false;
        if (threadId != currentThreadId)
        {
            attached = AttachThreadInput(currentThreadId, threadId, true);
        }

        try
        {
            var guiInfo = new GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() };
            GetGUIThreadInfo(threadId, ref guiInfo);

            var caretWindow = ResolveEditorWindow(hWnd, guiInfo.hwndCaret, guiInfo.hwndFocus);
            var focusWindow = guiInfo.hwndFocus != IntPtr.Zero ? guiInfo.hwndFocus : caretWindow;
            var omniboxCaret = guiInfo.hwndCaret != IntPtr.Zero && IsLikelyEditorClass(guiInfo.hwndCaret);

            var whatsApp = WebEditorSupport.IsWhatsApp(processName);
            if (whatsApp
                && UiaScreenAnchor.TryGetWordAnchor(
                    currentWord,
                    content,
                    out var composerX,
                    out var composerY,
                    constrainToComposer: true,
                    charsBeforeWord: CharsBeforeCurrentWord(hWnd, currentWord),
                    trackedCaretX: _hasEstimate ? _estimateX : 0)
                && Accept(composerX, composerY))
            {
                return Finish(composerX, composerY, UiaScreenAnchor.LastLineHeight, "whatsapp-composer");
            }

            if (!codeEditor
                && !whatsApp
                && UiaScreenAnchor.TryGetWordAnchor(currentWord, content, out var uiaX, out var uiaY)
                && Accept(uiaX, uiaY))
            {
                return Finish(uiaX, uiaY, UiaScreenAnchor.LastLineHeight, "uia");
            }

            if (!whatsApp
                && AccessibleCaret.TryGetTopLeft(content, out var accX, out var accY, out var accH)
                && Accept(accX, accY))
            {
                return Finish(accX, accY, accH, "msaa-caret");
            }

            if (!whatsApp
                && content != hWnd
                && AccessibleCaret.TryGetTopLeft(hWnd, out accX, out accY, out accH)
                && Accept(accX, accY))
            {
                return Finish(accX, accY, accH, "msaa-caret-frame");
            }

            if (!whatsApp
                && !codeEditor && !omniboxCaret
                && TryChromeRenderCaret(hWnd, caretWindow, currentWord, out var chromeCaret)
                && Accept(chromeCaret.X, chromeCaret.Y))
            {
                return Finish(chromeCaret.X, chromeCaret.Y, chromeCaret.LineHeight, "chrome-render-caret");
            }

            var wordStart = GetCurrentWordStartIndex(currentWord);
            if (wordStart >= 0 && IsLikelyEditorClass(caretWindow))
            {
                if (TryGetCharScreenPosition(caretWindow, wordStart, out var fromCaret) && Accept(fromCaret.X, fromCaret.Y))
                {
                    return Finish(fromCaret.X, fromCaret.Y, LastAnchorLineHeight, "posFromChar");
                }

                if (focusWindow != caretWindow
                    && IsLikelyEditorClass(focusWindow)
                    && TryGetCharScreenPosition(focusWindow, wordStart, out var fromFocus)
                    && Accept(fromFocus.X, fromFocus.Y))
                {
                    return Finish(fromFocus.X, fromFocus.Y, LastAnchorLineHeight, "posFromChar-focus");
                }
            }

            var mapWindow = guiInfo.hwndCaret != IntPtr.Zero && !omniboxCaret
                ? guiInfo.hwndCaret
                : (webDoc || codeEditor ? content : caretWindow);
            var hasCaretRect = guiInfo.rcCaretBottom > guiInfo.rcCaretTop
                || guiInfo.rcCaretRight > guiInfo.rcCaretLeft;
            if (!whatsApp && hasCaretRect && !(codeEditor && guiInfo.hwndCaret == IntPtr.Zero) && !omniboxCaret)
            {
                var point = new POINT
                {
                    X = guiInfo.rcCaretLeft,
                    Y = guiInfo.rcCaretTop
                };
                var caretHeight = Math.Max(0, guiInfo.rcCaretBottom - guiInfo.rcCaretTop);
                ShiftLeftByWordWidth(mapWindow, focusWindow, currentWord, ref point, caretHeight);
                if (ClientToScreen(mapWindow, ref point) && Accept(point.X, point.Y))
                {
                    return Finish(point.X, point.Y, Math.Max(16, caretHeight), "caretRect");
                }
            }

            var gotCaretPos = GetCaretPos(out var caretPos);
            var trustCaretPos = !whatsApp && gotCaretPos && guiInfo.hwndCaret != IntPtr.Zero && !omniboxCaret;
            if (trustCaretPos)
            {
                ShiftLeftByWordWidth(mapWindow, focusWindow, currentWord, ref caretPos);
                if (ClientToScreen(mapWindow, ref caretPos) && Accept(caretPos.X, caretPos.Y))
                {
                    return Finish(caretPos.X, caretPos.Y, LastAnchorLineHeight, "GetCaretPos");
                }
            }

            if (codeEditor
                && UiaScreenAnchor.TryGetWordAnchor(currentWord, content, out uiaX, out uiaY)
                && Accept(uiaX, uiaY))
            {
                return Finish(uiaX, uiaY, UiaScreenAnchor.LastLineHeight, "uia-ide");
            }

            if (TryEstimateAnchor(currentWord, out var estimated) && Accept(estimated.X, estimated.Y))
            {
                return Finish(estimated.X, estimated.Y, LastAnchorLineHeight, "pointer-estimate");
            }

            DiagnosticLog.WritePlacement(
                $"ANCHOR fallback dummy (100,100) word={DiagnosticLog.Escape(currentWord)} hwnd=0x{hWnd:X} class={className} editor=0x{caretWindow:X} hwndCaret=0x{guiInfo.hwndCaret:X} hasCaretRect={hasCaretRect} gotCaretPos={gotCaretPos} caretPos=({caretPos.X},{caretPos.Y})");
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThreadId, threadId, false);
            }
        }

        return EstimatedOrDummy(currentWord, "fallback");
    }

    public void NotePointerScreenPosition(int x, int y)
    {
        if (x == 0 && y == 0)
        {
            return;
        }

        RememberEstimate(x, y - 6);
    }

    private void RememberEstimate(int x, int y)
    {
        if (CaretAnchorPolicy.IsDummy(x, y))
        {
            return;
        }

        _hasEstimate = true;
        _estimateX = x;
        _estimateY = y;
    }

    private void AdvanceEstimate(char character)
    {
        if (!_hasEstimate)
        {
            return;
        }

        var width = CaretAnchorPolicy.EstimateCharWidth(LastAnchorLineHeight);
        if (character == '\b')
        {
            _estimateX = Math.Max(0, _estimateX - width);
        }
        else if (character is '\r' or '\n')
        {
            _estimateY += Math.Max(16, LastAnchorLineHeight);
        }
        else if (!char.IsControl(character))
        {
            _estimateX += width;
        }
    }

    private bool TryEstimateAnchor(string? currentWord, out (int X, int Y) point)
    {
        point = default;
        if (!_hasEstimate)
        {
            return false;
        }

        var x = _estimateX;
        if (!string.IsNullOrEmpty(currentWord) && currentWord.Length > 1)
        {
            x -= (currentWord.Length - 1) * CaretAnchorPolicy.EstimateCharWidth(LastAnchorLineHeight);
        }

        point = (x, _estimateY);
        return true;
    }

    private (int X, int Y) EstimatedOrDummy(string? currentWord, string via)
    {
        if (TryEstimateAnchor(currentWord, out var estimated))
        {
            DiagnosticLog.WritePlacement($"ANCHOR via={via}-estimate finalScreen=({estimated.X},{estimated.Y})");
            return estimated;
        }

        return (100, 100);
    }

    private static bool IsUsableScreenPoint(IntPtr hwnd, int x, int y)
    {
        if (hwnd == IntPtr.Zero || (x == 0 && y == 0))
        {
            return false;
        }

        if (!GetWindowRect(hwnd, out var window))
        {
            return true;
        }

        const int slack = 48;
        return x >= window.left - slack
            && x <= window.right + slack
            && y >= window.top - slack
            && y <= window.bottom + slack;
    }

    private bool TryChromeRenderCaret(IntPtr foreground, IntPtr caretWindow, string? currentWord, out (int X, int Y, int LineHeight) result)
    {
        result = default;
        if (WebEditorSupport.IsCodeEditorShell(GetProcessBaseName(foreground)))
        {
            return false;
        }
        var className = GetWindowClassName(foreground);
        if (!className.Contains("Chrome", StringComparison.OrdinalIgnoreCase)
            && !className.Contains("Mozilla", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var render = FindChildByClass(foreground, "Chrome_RenderWidgetHostHWND");
        if (render == IntPtr.Zero)
        {
            render = caretWindow != IntPtr.Zero ? caretWindow : foreground;
        }

        var threadId = GetWindowThreadProcessId(foreground, out _);
        var guiInfo = new GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(threadId, ref guiInfo))
        {
            return false;
        }

        var hasCaretRect = guiInfo.rcCaretBottom > guiInfo.rcCaretTop
            || guiInfo.rcCaretRight > guiInfo.rcCaretLeft;
        if (!hasCaretRect)
        {
            return false;
        }

        var point = new POINT
        {
            X = guiInfo.rcCaretLeft,
            Y = guiInfo.rcCaretTop
        };
        var height = Math.Max(16, guiInfo.rcCaretBottom - guiInfo.rcCaretTop);
        ShiftLeftByWordWidth(render, render, currentWord, ref point, height);
        if (!ClientToScreen(render, ref point) || !IsUsableScreenPoint(foreground, point.X, point.Y))
        {
            return false;
        }

        result = (point.X, point.Y, height);
        return true;
    }

    private static IntPtr FindChildByClass(IntPtr root, string className)
    {
        var found = IntPtr.Zero;
        if (root == IntPtr.Zero)
        {
            return found;
        }

        EnumChildWindows(root, (child, _) =>
        {
            if (GetWindowClassName(child).Equals(className, StringComparison.OrdinalIgnoreCase))
            {
                found = child;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static IntPtr ResolveEditorWindow(IntPtr foreground, IntPtr hwndCaret, IntPtr hwndFocus)
    {
        if (IsLikelyEditorClass(hwndCaret))
        {
            return hwndCaret;
        }

        if (IsLikelyEditorClass(hwndFocus))
        {
            return hwndFocus;
        }

        var nested = FindChildEditor(foreground);
        if (nested != IntPtr.Zero)
        {
            return nested;
        }

        if (hwndCaret != IntPtr.Zero)
        {
            return hwndCaret;
        }

        if (hwndFocus != IntPtr.Zero)
        {
            return hwndFocus;
        }

        return foreground;
    }

    private static IntPtr FindChildEditor(IntPtr root)
    {
        if (root == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var found = IntPtr.Zero;
        EnumChildWindows(root, (child, _) =>
        {
            if (IsLikelyEditorClass(child))
            {
                found = child;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static bool IsLikelyEditorClass(IntPtr hWnd)
    {
        var className = GetWindowClassName(hWnd);
        return className.Contains("Edit", StringComparison.OrdinalIgnoreCase)
            || className.Contains("RichEdit", StringComparison.OrdinalIgnoreCase)
            || className.Equals("Scintilla", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWindowClassName(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return "<none>";
        }

        var buffer = new StringBuilder(256);
        return GetClassName(hWnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "<unknown>";
    }

    private int CharsBeforeCurrentWord(IntPtr hWnd, string? currentWord)
    {
        string text;
        lock (_bufferLock)
        {
            text = GetOrCreateTextBuffer(hWnd).ToString();
        }

        if (text.Length == 0)
        {
            return 0;
        }

        var lineStart = Math.Max(text.LastIndexOf('\n'), text.LastIndexOf('\r'));
        var line = lineStart >= 0 ? text[(lineStart + 1)..] : text;
        var word = currentWord ?? string.Empty;
        if (word.Length > 0 && line.EndsWith(word, StringComparison.OrdinalIgnoreCase))
        {
            return line.Length - word.Length;
        }

        return Math.Max(0, line.Length - word.Length);
    }

    private int GetCurrentWordStartIndex(string? currentWord)
    {
        var editor = TryReadEditorText();
        if (editor == null || string.IsNullOrEmpty(currentWord))
        {
            return -1;
        }

        var caret = editor.Value.CaretIndex;
        var start = caret - currentWord.Length;
        if (start < 0 || start > editor.Value.Text.Length)
        {
            return -1;
        }

        return start;
    }

    private static bool TryGetCharScreenPosition(IntPtr hWnd, int charIndex, out (int X, int Y) screen)
    {
        screen = default;
        if (hWnd == IntPtr.Zero || charIndex < 0)
        {
            return false;
        }

        if (!TryGetCharClientPosition(hWnd, charIndex, out var client))
        {
            return false;
        }

        if (!ClientToScreen(hWnd, ref client))
        {
            return false;
        }

        screen = (client.X, client.Y);
        return true;
    }

    private static bool TryGetCharClientPosition(IntPtr hWnd, int charIndex, out POINT point)
    {
        point = default;

        var packed = SendMessage(hWnd, EM_POSFROMCHAR, (IntPtr)charIndex, IntPtr.Zero);
        var value = packed.ToInt64();
        if (value != -1)
        {
            point.X = unchecked((short)(value & 0xFFFF));
            point.Y = unchecked((short)((value >> 16) & 0xFFFF));
            if (point.X >= 0 && point.Y >= 0 && (charIndex == 0 || value != 0))
            {
                return true;
            }
        }

        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<POINT>());
        try
        {
            Marshal.StructureToPtr(new POINT(), buffer, false);
            SendMessage(hWnd, EM_POSFROMCHAR_RICHEDIT, buffer, (IntPtr)charIndex);
            point = Marshal.PtrToStructure<POINT>(buffer);
            return point.X >= 0 && point.Y >= 0 && (charIndex == 0 || point.X != 0 || point.Y != 0);
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int? ShiftLeftByWordWidth(IntPtr caretWindow, IntPtr focusWindow, string? currentWord, ref POINT point, int caretHeight = 0)
    {
        if (string.IsNullOrEmpty(currentWord))
        {
            return null;
        }

        var wordWidth = MeasureTextWidth(caretWindow, currentWord);
        if (!wordWidth.HasValue && focusWindow != caretWindow)
        {
            wordWidth = MeasureTextWidth(focusWindow, currentWord);
        }

        if (!wordWidth.HasValue && caretHeight > 0)
        {
            wordWidth = Math.Max(1, (int)Math.Round(currentWord.Length * caretHeight * 0.5));
        }

        if (wordWidth.HasValue)
        {
            point.X = Math.Max(0, point.X - wordWidth.Value);
        }

        return wordWidth;
    }

    private TextContext ExtractTextContext(IntPtr hWnd)
    {
        var context = new TextContext();

        // Prefer the live text read directly from the focused control
        // (WM_GETTEXT/EM_GETSEL). This reflects reality exactly, including
        // edits our keystroke buffer can never observe correctly — select-all
        // + backspace, mouse-based selection + typing, Ctrl+Z, cut/paste,
        // clicking elsewhere and typing, etc. Trust it completely whenever
        // it succeeds, including when the control is legitimately empty —
        // an empty result is real information, not a reason to fall back.
        // Browser documents (Google Docs) are not Win32 edit controls.
        // WM_GETTEXT is the tab title; trust the keystroke buffer instead.
        var title = new StringBuilder(256);
        GetWindowText(hWnd, title, title.Capacity);
        var className = GetWindowClassName(hWnd);
        var browser = WebEditorSupport.IsBrowserWindowClass(className)
            || WebEditorSupport.IsGoogleDocs(title.ToString());

        var editorText = browser ? null : TryReadEditorText();
        if (editorText != null
            && WebEditorSupport.ShouldIgnoreLiveText(editorText.Value.Text, title.ToString()))
        {
            editorText = null;
        }

        if (editorText != null)
        {
            context.FullText = editorText.Value.Text;
            ExtractWordContext(editorText.Value.Text, editorText.Value.CaretIndex, context);
            return context;
        }

        // Live read failed outright (e.g. a control that doesn't support
        // WM_GETTEXT). Fall back to the keystroke buffer as a best-effort
        // guess — it can drift from reality, but it's better than nothing
        // for controls we can't read directly.
        string bufferSnapshot;
        lock (_bufferLock)
        {
            bufferSnapshot = GetOrCreateTextBuffer(hWnd).ToString();
        }

        context.FullText = bufferSnapshot;
        ExtractWordContext(bufferSnapshot, bufferSnapshot.Length, context);
        return context;
    }

    private IntPtr GetFocusedControlHandle(IntPtr foregroundWindow)
    {
        if (foregroundWindow == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
        var currentThreadId = GetCurrentThreadId();
        var attached = false;

        if (foregroundThreadId != currentThreadId)
        {
            attached = AttachThreadInput(currentThreadId, foregroundThreadId, true);
        }

        try
        {
            var guiInfo = new GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() };
            if (GetGUIThreadInfo(foregroundThreadId, ref guiInfo) && guiInfo.hwndFocus != IntPtr.Zero)
            {
                return guiInfo.hwndFocus;
            }

            return GetFocus();
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    private static bool IsPasswordControl(IntPtr controlHandle)
    {
        if (controlHandle == IntPtr.Zero)
        {
            return false;
        }

        var style = GetWindowLongPtr(controlHandle, GwlStyle).ToInt64();
        return (style & EsPassword) != 0;
    }

    private static uint ProcessIdOf(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return 0;
        }

        GetWindowThreadProcessId(hWnd, out var processId);
        return processId;
    }

    private string GetProcessBaseName(IntPtr hWnd)
    {
        var processId = ProcessIdOf(hWnd);
        if (processId == 0)
        {
            return string.Empty;
        }

        var processHandle = OpenProcess(0x0410, false, processId);
        if (processHandle == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            var name = new StringBuilder(256);
            GetModuleBaseName(processHandle, IntPtr.Zero, name, (uint)name.Capacity);
            return name.ToString();
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    private StringBuilder GetOrCreateTextBuffer(IntPtr hWnd)
    {
        var key = ProcessIdOf(hWnd);
        if (key == 0)
        {
            return new StringBuilder();
        }

        if (_textBuffers.TryGetValue(key, out var buffer))
        {
            TouchTextBuffer(key);
            return buffer;
        }

        buffer = new StringBuilder();
        _textBuffers[key] = buffer;
        _bufferAccessOrder.AddLast(key);
        TrimTextBuffersIfNeeded();
        return buffer;
    }

    private void TouchTextBuffer(uint processId)
    {
        _bufferAccessOrder.Remove(processId);
        _bufferAccessOrder.AddLast(processId);
    }

    private void TrimTextBuffersIfNeeded()
    {
        while (_textBuffers.Count > MaxTextBuffers && _bufferAccessOrder.First != null)
        {
            var oldest = _bufferAccessOrder.First.Value;
            _bufferAccessOrder.RemoveFirst();
            _textBuffers.Remove(oldest);
            _bufferLastModified.Remove(oldest);
        }
    }
    private FocusedTextInfo? TryReadEditorText()
    {
        return _uiAutomationHelper.TryReadFocusedText();
    }

    private void ExtractWordContext(string fullText, int cursorPosition, TextContext context)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            context.CurrentWord = string.Empty;
            context.PreviousWords = string.Empty;
            context.FollowingWords = string.Empty;
            context.CursorPosition = 0;
            return;
        }

        var actualCursorPosition = cursorPosition < 0 ? fullText.Length : cursorPosition;
        context.CursorPosition = actualCursorPosition;

        // Find current word (word before cursor)
        var wordStart = actualCursorPosition;
        while (wordStart > 0 && !SuggestionInsertion.IsWordSeparator(fullText[wordStart - 1]))
        {
            wordStart--;
        }

        context.CurrentWord = fullText.Substring(wordStart, actualCursorPosition - wordStart);

        // Get previous words (words before current word)
        var textBeforeCurrent = fullText.Substring(0, wordStart).Trim();
        if (!string.IsNullOrEmpty(textBeforeCurrent))
        {
            var words = textBeforeCurrent.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var previousWordsCount = Math.Min(16, words.Length);
            context.PreviousWords = string.Join(" ", words.Skip(words.Length - previousWordsCount));
        }
        else
        {
            context.PreviousWords = string.Empty;
        }

        if (actualCursorPosition < fullText.Length)
        {
            var after = fullText[actualCursorPosition..];
            var following = after.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            context.FollowingWords = string.Join(" ", following.Take(8));
        }
        else
        {
            context.FollowingWords = string.Empty;
        }
    }

    public void AddTypedCharacter(char character)
    {
        var hWnd = GetEditorForegroundWindow();
        if (hWnd == IntPtr.Zero) return;

        if (SecureFieldProbe.IsSecure(GetFocusedControlHandle(hWnd)))
        {
            return;
        }

        DiagnosticLog.Write($"AddTypedCharacter: char={DiagnosticLog.Escape(character.ToString())} foreground hwnd=0x{hWnd:X}");
        AdvanceEstimate(character);

        lock (_bufferLock)
        {
            var buffer = GetOrCreateTextBuffer(hWnd);

            if (character == '\b' && buffer.Length > 0)
            {
                buffer.Length--;
            }
            else if (!char.IsControl(character))
            {
                buffer.Append(character);
            }
        }

        _lastWindowHandle = hWnd;
    }

    private void CheckFocus(object? state)
    {
        if (!_isRunning) return;

        var currentContext = GetCurrentContext();
        ContextChanged?.Invoke(this, currentContext);
    }
}
