using Lexon.Core.Models;
using Lexon.Overlay.Interfaces;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Lexon.Overlay;

/// <summary>
/// Non-activating suggestion overlay window
/// </summary>
public class SuggestionOverlay : ISuggestionOverlay
{
    private IntPtr _windowHandle = IntPtr.Zero;
    private List<Suggestion> _currentSuggestions = new();
    private List<string> _predictions = new();
    private readonly List<Rectangle> _chipRects = new();
    private string? _flashText;
    private long _flashGeneration;
    private int _selectedIndex = 0;
    private int _scrollOffset = 0;
    public const string WordClassName = "LexonSuggestionOverlay";
    public const string GrammarClassName = "LexonGrammarOverlay";

    private const int VisibleRowCount = 3;
    private const int ItemHeight = 30;
    private const int ChipHeight = 26;
    private const int ChipGap = 6;
    private const int ListPadding = 10;
    private const int BannerHeight = 22;
    private const int FooterHeight = 20;
    private string? _statusText;
    private readonly OverlayThemePalette _palette;
    private readonly OverlayChrome _chrome;
    private Dictionary<string, Color> _sourceColors = new();
    private WndProcDelegate? _wndProcDelegate;
    private Thread? _messageThread;
    private CancellationTokenSource? _messageThreadCts;
    private readonly ManualResetEventSlim _windowReady = new(false);
    private readonly object _suggestionsLock = new object();
    private bool _isShowing = false;
    private int _overlayCommandId;
    private uint _overlayThreadId;
    private bool _preferAbove;
    private bool? _lockedBelow;
    private readonly string _className;
    private readonly string _windowTitle;
    private readonly string? _banner;
    private readonly int _edgeGap;
    
    // Track last shown position for hide-artifact defense
    private int _lastX = 0;
    private int _lastY = 0;
    private int _lastWidth = 0;
    private int _lastHeight = 0;

    public event EventHandler<SuggestionSelectedEventArgs>? SuggestionSelected;
    public event EventHandler<SuggestionDismissedEventArgs>? SuggestionDismissed;

    // Win32 API declarations
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private static readonly IntPtr DpiAwarenessContextPerMonitorV2 = new(-4);
    
    // RedrawWindow flags
    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_ERASE = 0x0004;
    private const uint RDW_UPDATENOW = 0x0100;
    private const uint RDW_ALLCHILDREN = 0x0080;

    // Constants
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_NOACTIVATE = 0x08000000;
    private const uint SW_SHOWNA = 8;
    private const uint SW_HIDE = 0;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint LWA_ALPHA = 0x0002;
    private const uint WM_PAINT = 0x000F;
    private const uint WM_MOUSEACTIVATE = 0x0021;
    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_MOUSEWHEEL = 0x020A;
    private const uint WM_DESTROY = 0x0002;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_CHAR = 0x0102;
    private const uint WM_SYSKEYDOWN = 0x0104;
    private const uint WM_SYSKEYUP = 0x0105;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_APP = 0x8000;
    private const uint WM_LEXON_SHOW = WM_APP + 1;
    private const uint WM_LEXON_HIDE = WM_APP + 2;
    private const uint WM_LEXON_REPAINT = WM_APP + 3;
    private const uint WM_LEXON_MOVE = WM_APP + 4;
    private const uint MA_NOACTIVATE = 3;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    // Structures
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fErase;
        public RECT rcPaint;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fRestore;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // Delegate for window procedure
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    public SuggestionOverlay(OverlayThemeHost themeHost)
        : this(themeHost, WordClassName, "Lexon Suggestions", null)
    {
    }

    public SuggestionOverlay(OverlayThemeHost themeHost, string className, string windowTitle, string? banner, int edgeGap = 8)
    {
        ArgumentNullException.ThrowIfNull(themeHost);
        _palette = themeHost.Palette;
        _chrome = new OverlayChrome(_palette);
        _palette.Changed += OnPaletteChanged;
        _className = className;
        _windowTitle = windowTitle;
        _banner = banner;
        _edgeGap = Math.Clamp(edgeGap, 8, 40);
        InitializeWindow();
        SyncSourceColors();
    }

    private void OnPaletteChanged(object? sender, EventArgs e)
    {
        SyncSourceColors();
        if (_windowHandle != IntPtr.Zero)
        {
            InvalidateRect(_windowHandle, IntPtr.Zero, true);
        }
    }

    private int BannerOffset => string.IsNullOrEmpty(_banner) ? 0 : BannerHeight;

    private int OverlayPixelHeight()
    {
        int suggestionCount;
        int predictionCount;
        bool flashing;
        bool hasStatus;
        lock (_suggestionsLock)
        {
            suggestionCount = _currentSuggestions.Count;
            predictionCount = _predictions.Count;
            flashing = _flashText != null;
            hasStatus = !string.IsNullOrEmpty(_statusText);
        }
        var rows = Math.Clamp(suggestionCount, 0, VisibleRowCount);
        var chips = predictionCount > 0 || flashing;
        var height = BannerOffset + ListPadding * 2;
        if (rows > 0)
        {
            height += rows * ItemHeight;
        }
        else if (hasStatus)
        {
            height += ItemHeight;
        }

        if (chips)
        {
            height += ChipHeight + 4;
        }

        if (rows > 0 || chips || hasStatus)
        {
            height += FooterHeight;
        }

        return height;
    }

    private int OverlayPixelWidth()
    {
        lock (_suggestionsLock)
        {
            var words = new List<string>();
            if (_flashText != null)
            {
                words.Add(_flashText);
            }

            words.AddRange(_predictions);
            if (words.Count == 0)
            {
                return 300;
            }
            var width = ListPadding * 2;
            for (var i = 0; i < words.Count; i++)
            {
                width += ChipGap + 28 + Math.Max(24, words[i].Length * 8);
            }

            return Math.Max(300, width);
        }
    }

    private void SyncSourceColors()
    {
        _sourceColors = _palette.SourceColors.ToDictionary(
            static kvp => kvp.Key,
            static kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private void InitializeWindow()
    {
        _wndProcDelegate = new WndProcDelegate(WindowProc);
        
        var wndClass = new WNDCLASS
        {
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = GetModuleHandle(string.Empty),
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null!,
            lpszClassName = _className
        };

        var atom = RegisterClass(ref wndClass);
        if (atom == 0)
        {
            // Class might already be registered, that's okay
        }

        // Start message pump thread
        _messageThreadCts = new CancellationTokenSource();
        _messageThread = new Thread(() => MessageLoop(_messageThreadCts.Token));
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.IsBackground = true;
        _messageThread.Start();

        // Wait for window to be ready (with timeout)
        if (!_windowReady.Wait(2000))
        {
            throw new TimeoutException("SuggestionOverlay window creation timed out");
        }

        // Check if window creation actually succeeded
        if (_windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("SuggestionOverlay window creation failed");
        }
    }

    private void MessageLoop(CancellationToken cancellationToken)
    {
        SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorV2);
        _overlayThreadId = GetCurrentThreadId();

        // Create the window on this thread
        _windowHandle = CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            _className,
            _windowTitle,
            WS_POPUP,
            0, 0, 300, 200,
            IntPtr.Zero,
            IntPtr.Zero,
            GetModuleHandle(string.Empty),
            IntPtr.Zero
        );

        if (_windowHandle == IntPtr.Zero)
        {
            _windowReady.Set();
            return;
        }

        // Signal that window is ready
        _windowReady.Set();

        // Set window to be semi-transparent
        SetLayeredWindowAttributes(_windowHandle, 0, 240, LWA_ALPHA);

        // Message loop
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = GetMessage(out var msg, IntPtr.Zero, 0, 0);
            if (result == -1)
            {
                break;
            }
            if (result == 0) // WM_QUIT
            {
                break;
            }

            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_windowHandle != IntPtr.Zero)
        {
            DestroyWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }
    }

    private IntPtr WindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
    {
        switch (uMsg)
        {
            case WM_PAINT:
                OnPaint(hWnd);
                return IntPtr.Zero;

            case WM_LEXON_SHOW:
                ApplyPendingShow(wParam);
                return IntPtr.Zero;

            case WM_LEXON_HIDE:
                ApplyPendingHide(wParam);
                return IntPtr.Zero;

            case WM_LEXON_REPAINT:
                if (_windowHandle != IntPtr.Zero)
                {
                    RedrawWindow(_windowHandle, IntPtr.Zero, IntPtr.Zero,
                        RDW_INVALIDATE | RDW_ERASE | RDW_UPDATENOW | RDW_ALLCHILDREN);
                }
                return IntPtr.Zero;

            case WM_LEXON_MOVE:
                ApplyPendingMove();
                return IntPtr.Zero;

            case WM_MOUSEACTIVATE:
                return (IntPtr)MA_NOACTIVATE;

            // Space (and other keys) must never be turned into a click on the
            // highlighted row. DefWindowProc can do that if this window is focused.
            case WM_KEYDOWN:
            case WM_KEYUP:
            case WM_CHAR:
            case WM_SYSKEYDOWN:
            case WM_SYSKEYUP:
                return IntPtr.Zero;

            case WM_LBUTTONDOWN:
                OnMouseClick(lParam);
                return IntPtr.Zero;

            case WM_MOUSEMOVE:
                OnMouseMove(lParam);
                return IntPtr.Zero;

            case WM_MOUSEWHEEL:
                OnMouseWheel(wParam);
                return IntPtr.Zero;

            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;

            default:
                return DefWindowProc(hWnd, uMsg, wParam, lParam);
        }
    }

    private int _lastHoverIndex = -1;
    private bool _ignoreHoverUntilMouseMoves;
    private int _hoverAnchorX = int.MinValue;
    private int _hoverAnchorY = int.MinValue;

    private int HitTestSuggestionIndex(IntPtr lParam)
    {
        int y = ((int)lParam >> 16) & 0xFFFF;
        var row = (y - ListPadding - BannerOffset) / ItemHeight;
        if (row < 0 || row >= VisibleRowCount)
        {
            return -1;
        }

        int index;
        lock (_suggestionsLock)
        {
            index = row + _scrollOffset;
            if (index < 0 || index >= _currentSuggestions.Count)
            {
                return -1;
            }
        }

        return index;
    }

    private int HitTestChipIndex(IntPtr lParam)
    {
        int x = (short)(lParam.ToInt64() & 0xFFFF);
        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        lock (_suggestionsLock)
        {
            for (var i = 0; i < _chipRects.Count; i++)
            {
                if (_chipRects[i].Contains(x, y))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private int MaxScrollOffset(int count) => Math.Max(0, count - VisibleRowCount);

    private void EnsureSelectionVisible()
    {
        if (_selectedIndex < _scrollOffset)
        {
            _scrollOffset = _selectedIndex;
        }
        else if (_selectedIndex >= _scrollOffset + VisibleRowCount)
        {
            _scrollOffset = _selectedIndex - VisibleRowCount + 1;
        }

        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScrollOffset(_currentSuggestions.Count));
    }

    private void OnMouseWheel(IntPtr wParam)
    {
        var delta = unchecked((short)((wParam.ToInt64() >> 16) & 0xFFFF));
        var steps = delta / 120;
        if (steps == 0)
        {
            return;
        }

        lock (_suggestionsLock)
        {
            // Positive wheel delta scrolls up (toward earlier suggestions).
            _scrollOffset = Math.Clamp(_scrollOffset - steps, 0, MaxScrollOffset(_currentSuggestions.Count));
        }

        if (_windowHandle != IntPtr.Zero)
        {
            InvalidateRect(_windowHandle, IntPtr.Zero, true);
        }
    }

    private void OnMouseMove(IntPtr lParam)
    {
        int x = (short)(lParam.ToInt64() & 0xFFFF);
        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        if (_ignoreHoverUntilMouseMoves)
        {
            if (_hoverAnchorX == int.MinValue)
            {
                _hoverAnchorX = x;
                _hoverAnchorY = y;
                return;
            }

            if (Math.Abs(x - _hoverAnchorX) < 4 && Math.Abs(y - _hoverAnchorY) < 4)
            {
                return;
            }

            _ignoreHoverUntilMouseMoves = false;
            _hoverAnchorX = int.MinValue;
            _hoverAnchorY = int.MinValue;
        }

        var hoverIndex = HitTestSuggestionIndex(lParam);
        var changed = false;

        lock (_suggestionsLock)
        {
            if (hoverIndex >= 0 && hoverIndex < _currentSuggestions.Count && hoverIndex != _selectedIndex)
            {
                _selectedIndex = hoverIndex;
                changed = true;
            }
        }

        if (changed && hoverIndex != _lastHoverIndex && _windowHandle != IntPtr.Zero)
        {
            InvalidateRect(_windowHandle, IntPtr.Zero, true);
        }

        _lastHoverIndex = hoverIndex;
    }

    private void OnMouseClick(IntPtr lParam)
    {
        var chipIndex = HitTestChipIndex(lParam);
        if (chipIndex >= 0)
        {
            ConfirmPrediction(chipIndex);
            return;
        }

        var clickedIndex = HitTestSuggestionIndex(lParam);

        lock (_suggestionsLock)
        {
            if (clickedIndex < 0 || clickedIndex >= _currentSuggestions.Count)
            {
                return;
            }

            _selectedIndex = clickedIndex;
        }
        
        if (_windowHandle != IntPtr.Zero)
        {
            InvalidateRect(_windowHandle, IntPtr.Zero, true);
        }
        
        ConfirmSelection();
    }

    private void OnPaint(IntPtr hWnd)
    {
        var hdc = BeginPaint(hWnd, out var ps);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            GetClientRect(hWnd, out var client);
            var width = Math.Max(1, client.right - client.left);
            var height = Math.Max(1, client.bottom - client.top);

            List<Suggestion> suggestionsCopy;
            int selectedIndexCopy;
            int scrollOffsetCopy;
            List<string> predictionsCopy;
            string? flashCopy;
            string? statusCopy;
            lock (_suggestionsLock)
            {
                suggestionsCopy = new List<Suggestion>(_currentSuggestions);
                selectedIndexCopy = _selectedIndex;
                scrollOffsetCopy = _scrollOffset;
                predictionsCopy = new List<string>(_predictions);
                flashCopy = _flashText;
                statusCopy = _statusText;
            }

            using var bitmap = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(_chrome.Background);
                using (var card = OverlayChrome.Rounded(new Rectangle(0, 0, width - 1, height - 1), OverlayChrome.CornerRadius))
                using (var fill = new SolidBrush(_chrome.Background))
                {
                    graphics.FillPath(fill, card);
                }

                using var font = new Font(OverlayChrome.FontName, 10f, FontStyle.Regular, GraphicsUnit.Point);
                var showScroll = suggestionsCopy.Count > VisibleRowCount;
                var textWidth = Math.Max(1, width - ListPadding * 2 - (showScroll ? 12 : 0));
                var visible = suggestionsCopy.Skip(scrollOffsetCopy).Take(VisibleRowCount).ToList();
                if (!string.IsNullOrEmpty(statusCopy) && visible.Count == 0 && predictionsCopy.Count == 0 && flashCopy == null)
                {
                    using var statusBrush = new SolidBrush(_chrome.Muted);
                    using var statusFont = new Font(OverlayChrome.FontName, 9f, FontStyle.Regular, GraphicsUnit.Point);
                    graphics.DrawString(statusCopy, statusFont, statusBrush, new RectangleF(ListPadding, ListPadding, textWidth, ItemHeight), new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    });
                }

                if (!string.IsNullOrEmpty(_banner))
                {
                    using var bannerFont = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
                    using var bannerBrush = new SolidBrush(_chrome.GrammarAccent);
                    graphics.DrawString(_banner, bannerFont, bannerBrush, new RectangleF(ListPadding, 4, textWidth, BannerHeight), new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center
                    });
                }

                for (int i = 0; i < visible.Count; i++)
                {
                    var actualIndex = scrollOffsetCopy + i;
                    var y = ListPadding + BannerOffset + (i * ItemHeight);
                    var itemRect = new Rectangle(ListPadding, y, textWidth, ItemHeight - 2);

                    if (actualIndex == selectedIndexCopy)
                    {
                        var highlightColor = string.IsNullOrEmpty(_banner)
                            ? _chrome.SelectedBackground
                            : _chrome.GrammarHighlight;
                        using var highlight = new SolidBrush(highlightColor);
                        graphics.FillRectangle(highlight, itemRect);
                    }

                    var textColor = actualIndex == selectedIndexCopy ? _chrome.SelectedText : _chrome.Text;
                    using var textBrush = new SolidBrush(textColor);
                    using var format = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    graphics.DrawString(visible[i].Text ?? string.Empty, font, textBrush, itemRect, format);
                }

                DrawPredictionChips(graphics, width, height, suggestionsCopy.Count, predictionsCopy, flashCopy, _chrome);
                DrawOriginFooter(graphics, width, height, suggestionsCopy, predictionsCopy, statusCopy, _chrome);

                if (showScroll)
                {
                    DrawScrollIndicator(graphics, width, height, suggestionsCopy.Count, scrollOffsetCopy, _chrome);
                }

                using var borderPen = new Pen(_chrome.Border);
                using var borderPath = OverlayChrome.Rounded(new Rectangle(0, 0, width - 1, height - 1), OverlayChrome.CornerRadius);
                graphics.DrawPath(borderPen, borderPath);
            }

            using var screen = Graphics.FromHdc(hdc);
            screen.DrawImageUnscaled(bitmap, 0, 0);
        }
        finally
        {
            EndPaint(hWnd, ref ps);
        }
    }

    private static void DrawOriginFooter(
        Graphics graphics,
        int width,
        int height,
        List<Suggestion> suggestions,
        List<string> predictions,
        string? status,
        OverlayChrome chrome)
    {
        var hasContent = suggestions.Count > 0 || predictions.Count > 0 || !string.IsNullOrEmpty(status);
        if (!hasContent)
        {
            return;
        }

        var origin = suggestions.Any(suggestion => IsCloudSource(suggestion.Source))
            ? "Cloud"
            : "Local";
        using var font = new Font(OverlayChrome.FontName, 7.5f, FontStyle.Regular, GraphicsUnit.Point);
        using var brush = new SolidBrush(chrome.Muted);
        var label = status != null && suggestions.Count == 0 && predictions.Count == 0
            ? status
            : origin;
        graphics.DrawString(label, font, brush, new RectangleF(ListPadding, height - FooterHeight - 2, width - ListPadding * 2, FooterHeight), new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        });
    }

    private static bool IsCloudSource(string? source)
        => source is "AI" or "OpenAI" or "Gemini" or "DeepSeek";

    private void DrawPredictionChips(
        Graphics graphics,
        int width,
        int height,
        int suggestionCount,
        List<string> predictions,
        string? flashText,
        OverlayChrome chrome)
    {
        var chips = new List<(string Text, bool Flash)>();
        if (flashText != null)
        {
            chips.Add((flashText, true));
        }

        foreach (var word in predictions)
        {
            chips.Add((word, false));
        }

        if (chips.Count == 0)
        {
            lock (_suggestionsLock)
            {
                _chipRects.Clear();
            }

            return;
        }

        var suggestionRows = Math.Clamp(suggestionCount, 0, VisibleRowCount);
        var y = ListPadding + BannerOffset + (suggestionRows * ItemHeight);
        using var font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(chrome.Text);
        var x = ListPadding;
        var rects = new List<Rectangle>();

        for (var i = 0; i < chips.Count; i++)
        {
            var (labelText, isFlash) = chips[i];
            var predictionIndex = isFlash ? -1 : i - (flashText != null ? 1 : 0);
            var label = isFlash ? labelText : $"{predictionIndex + 1}  {labelText}";
            var size = graphics.MeasureString(label, font);
            var chipWidth = Math.Max(36, (int)Math.Ceiling(size.Width) + 16);
            var rect = new Rectangle(x, y, chipWidth, ChipHeight);
            using var path = OverlayChrome.Rounded(rect, 4);
            using var fill = new SolidBrush(isFlash ? chrome.GrammarHighlight : chrome.SelectedBackground);
            graphics.FillPath(fill, path);
            graphics.DrawString(label, font, isFlash ? textBrush : textBrush, rect.X + 8, rect.Y + (ChipHeight - size.Height) / 2);
            if (!isFlash)
            {
                rects.Add(rect);
            }

            x += chipWidth + ChipGap;
            if (x > width - ListPadding)
            {
                break;
            }
        }

        lock (_suggestionsLock)
        {
            _chipRects.Clear();
            _chipRects.AddRange(rects);
        }
    }

    private static void DrawScrollIndicator(Graphics graphics, int width, int height, int totalCount, int scrollOffset, OverlayChrome chrome)
    {
        const int trackWidth = 4;
        const int margin = 4;
        var trackX = width - trackWidth - margin;
        var trackHeight = Math.Max(1, height - margin * 2);

        using (var trackBrush = new SolidBrush(chrome.ScrollTrack))
        {
            graphics.FillRectangle(trackBrush, trackX, margin, trackWidth, trackHeight);
        }

        var thumbHeight = Math.Max(12, trackHeight * VisibleRowCount / totalCount);
        var maxOffset = Math.Max(1, totalCount - VisibleRowCount);
        var thumbTravel = Math.Max(0, trackHeight - thumbHeight);
        var thumbY = margin + thumbTravel * scrollOffset / maxOffset;

        using var thumbBrush = new SolidBrush(chrome.ScrollThumb);
        graphics.FillRectangle(thumbBrush, trackX, thumbY, trackWidth, thumbHeight);
    }

    public void SetPlacement(string placement)
    {
        _preferAbove = string.Equals(placement, "Above", StringComparison.OrdinalIgnoreCase);
    }

    public void ShowSuggestions(IEnumerable<Suggestion> suggestions, int x, int y, int lineHeight = 20)
    {
        lock (_suggestionsLock)
        {
            _currentSuggestions = suggestions.ToList();
            _predictions = new List<string>();
            _flashText = null;
            _statusText = null;
            _selectedIndex = 0;
            _scrollOffset = 0;
            _isShowing = _currentSuggestions.Count > 0;
        }
        _lastHoverIndex = -1;

        var height = OverlayPixelHeight();
        var width = OverlayPixelWidth();

        var receivedX = x;
        var receivedY = y;
        var previousDpi = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorV2);
        try
        {
            (x, y) = PlaceAgainstAnchor(x, y, width, height, lineHeight);
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousDpi);
        }

        PlacementLog.Write($"SHOW received=({receivedX},{receivedY}) placed=({x},{y}) size={width}x{height} lineH={lineHeight} count={_currentSuggestions.Count}");

        // Track position for hide-artifact defense
        _lastX = x;
        _lastY = y;
        _lastWidth = width;
        _lastHeight = height;

        if (_windowHandle != IntPtr.Zero)
        {
            var commandId = Interlocked.Increment(ref _overlayCommandId);
            PostMessage(_windowHandle, WM_LEXON_SHOW, (IntPtr)commandId, IntPtr.Zero);
        }
    }

    public void ShowPredictions(PredictedFollowers predictions, int x, int y, int lineHeight = 20)
    {
        lock (_suggestionsLock)
        {
            _predictions = predictions.Words?.Where(w => !string.IsNullOrWhiteSpace(w)).Take(3).ToList()
                ?? new List<string>();
            _isShowing = _predictions.Count > 0 || _currentSuggestions.Count > 0 || _flashText != null;
        }

        if (!IsVisible)
        {
            ForceHideWindow();
            return;
        }

        PresentAt(x, y, lineHeight);
    }

    public void ConfirmPrediction(int index)
    {
        string? word = null;
        lock (_suggestionsLock)
        {
            if (!_isShowing || index < 0 || index >= _predictions.Count)
            {
                return;
            }

            word = _predictions[index];
            _isShowing = false;
            _predictions = new List<string>();
            _currentSuggestions = new List<Suggestion>();
        }

        ForceHideWindow();
        if (!string.IsNullOrEmpty(word))
        {
            OnSuggestionSelected(new Suggestion { Text = word, Source = "Prediction", Score = 1 });
        }
    }

    public void ShowStatus(string message, int x, int y, int lineHeight = 20)
    {
        lock (_suggestionsLock)
        {
            _statusText = message;
            _currentSuggestions = new List<Suggestion>();
            _predictions = new List<string>();
            _flashText = null;
            _isShowing = !string.IsNullOrWhiteSpace(message);
        }

        if (!IsVisible)
        {
            ForceHideWindow();
            return;
        }

        PresentAt(x, y, lineHeight);
    }

    public void FlashCorrection(string text, int x, int y, int lineHeight = 20)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var generation = Interlocked.Increment(ref _flashGeneration);
        lock (_suggestionsLock)
        {
            _flashText = text;
            _isShowing = true;
        }

        PresentAt(x, y, lineHeight);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Thread.Sleep(400);
            if (Interlocked.Read(ref _flashGeneration) != generation)
            {
                return;
            }

            lock (_suggestionsLock)
            {
                if (_flashText == null)
                {
                    return;
                }

                _flashText = null;
                if (_predictions.Count == 0 && _currentSuggestions.Count == 0)
                {
                    _isShowing = false;
                }
            }

            if (!IsVisible)
            {
                ForceHideWindow();
            }
            else if (_windowHandle != IntPtr.Zero)
            {
                PostMessage(_windowHandle, WM_LEXON_REPAINT, IntPtr.Zero, IntPtr.Zero);
            }
        });
    }

    private void PresentAt(int x, int y, int lineHeight)
    {
        var height = OverlayPixelHeight();
        var width = OverlayPixelWidth();
        var previousDpi = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorV2);
        try
        {
            (x, y) = PlaceAgainstAnchor(x, y, width, height, lineHeight);
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousDpi);
        }

        _lastX = x;
        _lastY = y;
        _lastWidth = width;
        _lastHeight = height;

        if (_windowHandle != IntPtr.Zero)
        {
            var commandId = Interlocked.Increment(ref _overlayCommandId);
            PostMessage(_windowHandle, WM_LEXON_SHOW, (IntPtr)commandId, IntPtr.Zero);
        }
    }

    public void ReplaceSuggestions(IEnumerable<Suggestion> suggestions)
    {
        lock (_suggestionsLock)
        {
            if (!_isShowing)
            {
                return;
            }

            _currentSuggestions = suggestions.ToList();
            _predictions = new List<string>();
            _flashText = null;
            _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, _currentSuggestions.Count - 1));
            _scrollOffset = 0;
            if (_currentSuggestions.Count == 0)
            {
                _isShowing = false;
            }
            else
            {
                EnsureSelectionVisible();
            }
        }

        if (!_isShowing)
        {
            ForceHideWindow();
            return;
        }

        if (_windowHandle != IntPtr.Zero)
        {
            PostMessage(_windowHandle, WM_LEXON_REPAINT, IntPtr.Zero, IntPtr.Zero);
        }
    }

    /// <summary>
    /// Places the overlay on the preferred side of the typed line.
    /// Flips to the other side only when that preferred side does not fit.
    /// </summary>
    private (int X, int Y) PlaceAgainstAnchor(int anchorX, int anchorTop, int width, int height, int lineHeight = 0)
    {
        var line = lineHeight > 0 ? Math.Clamp(lineHeight, 14, 48) : 20;
        var gap = _edgeGap;

        var work = GetMonitorWorkArea(anchorX, anchorTop);
        var bounds = work;

        var belowY = anchorTop + line + gap;
        var aboveY = anchorTop - height - gap;

        var belowFits = belowY + height <= work.bottom;
        var aboveFits = aboveY >= work.top;
        var useBelow = _preferAbove ? (!aboveFits && belowFits) : belowFits;
        if (_lockedBelow is bool locked)
        {
            if (locked && belowFits)
            {
                useBelow = true;
            }
            else if (!locked && aboveFits)
            {
                useBelow = false;
            }
        }

        _lockedBelow = useBelow;
        var y = useBelow ? belowY : aboveY;
        y = Math.Max(work.top, Math.Min(y, work.bottom - height));

        var maxX = bounds.right - width;
        var x = maxX < bounds.left
            ? bounds.left
            : Math.Max(bounds.left, Math.Min(anchorX, maxX));
        return (x, y);
    }

    private static RECT GetVisiblePlacementBounds(int anchorX, int anchorTop)
    {
        var work = GetMonitorWorkArea(anchorX, anchorTop);
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || !GetWindowRect(foreground, out var window))
        {
            return work;
        }

        if (CoversWorkArea(window, work))
        {
            return work;
        }

        var visible = Intersect(work, window);
        if (visible.right - visible.left < 80 || visible.bottom - visible.top < 80)
        {
            return work;
        }

        return visible;
    }

    private static bool CoversWorkArea(RECT window, RECT work)
    {
        const int slack = 16;
        return window.left <= work.left + slack
            && window.top <= work.top + slack
            && window.right >= work.right - slack
            && window.bottom >= work.bottom - slack;
    }

    private static RECT GetMonitorWorkArea(int x, int y)
    {
        var point = new POINT { x = x, y = y };
        var monitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
        {
            return info.rcWork;
        }

        return new RECT
        {
            left = 0,
            top = 0,
            right = Math.Max(1, GetSystemMetrics(SM_CXSCREEN)),
            bottom = Math.Max(1, GetSystemMetrics(SM_CYSCREEN))
        };
    }

    private static RECT Intersect(RECT a, RECT b) => new()
    {
        left = Math.Max(a.left, b.left),
        top = Math.Max(a.top, b.top),
        right = Math.Min(a.right, b.right),
        bottom = Math.Min(a.bottom, b.bottom)
    };

    public bool IsVisible
    {
        get
        {
            lock (_suggestionsLock)
            {
                return _isShowing;
            }
        }
    }

    public bool HasPredictions
    {
        get
        {
            lock (_suggestionsLock)
            {
                return _isShowing && _predictions.Count > 0;
            }
        }
    }

    private void ApplyPendingShow(IntPtr commandId)
    {
        if (_windowHandle == IntPtr.Zero || commandId.ToInt32() != Volatile.Read(ref _overlayCommandId))
        {
            return;
        }

        lock (_suggestionsLock)
        {
            if (!_isShowing)
            {
                return;
            }
        }

        SetWindowPos(_windowHandle, HWND_TOPMOST, _lastX, _lastY, _lastWidth, _lastHeight, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        ShowWindow(_windowHandle, SW_SHOWNA);
        RedrawWindow(_windowHandle, IntPtr.Zero, IntPtr.Zero,
            RDW_INVALIDATE | RDW_ERASE | RDW_UPDATENOW | RDW_ALLCHILDREN);
    }

    public void Hide()
    {
        List<Suggestion>? dismissed = null;
        lock (_suggestionsLock)
        {
            if (_isShowing)
            {
                dismissed = new List<Suggestion>(_currentSuggestions);
            }

            _isShowing = false;
            _currentSuggestions = new List<Suggestion>();
            _predictions = new List<string>();
            _statusText = null;
            _flashText = null;
            _lockedBelow = null;
        }

        ForceHideWindow();

        if (dismissed != null)
        {
            SuggestionDismissed?.Invoke(this, new SuggestionDismissedEventArgs { DismissedSuggestions = dismissed });
        }
    }

    private void ForceHideWindow()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        var commandId = Interlocked.Increment(ref _overlayCommandId);
        if (GetCurrentThreadId() == _overlayThreadId)
        {
            ApplyPendingHide((IntPtr)commandId);
            return;
        }

        SendMessage(_windowHandle, WM_LEXON_HIDE, (IntPtr)commandId, IntPtr.Zero);
    }

    private void ApplyPendingHide(IntPtr commandId)
    {
        if (_windowHandle == IntPtr.Zero || commandId.ToInt32() != Volatile.Read(ref _overlayCommandId))
        {
            return;
        }

        ShowWindow(_windowHandle, SW_HIDE);
        SetWindowPos(_windowHandle, HWND_TOPMOST, -32000, -32000, 0, 0, SWP_NOACTIVATE | SWP_NOSIZE);

        if (_lastWidth > 0 && _lastHeight > 0)
        {
            var rect = new RECT
            {
                left = _lastX,
                top = _lastY,
                right = _lastX + _lastWidth,
                bottom = _lastY + _lastHeight
            };

            var rectPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RECT>());
            try
            {
                Marshal.StructureToPtr(rect, rectPtr, false);
                RedrawWindow(GetDesktopWindow(), rectPtr, IntPtr.Zero,
                    RDW_INVALIDATE | RDW_ERASE | RDW_UPDATENOW | RDW_ALLCHILDREN);
            }
            finally
            {
                Marshal.FreeHGlobal(rectPtr);
            }
        }

        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero && foreground != _windowHandle)
        {
            InvalidateRect(foreground, IntPtr.Zero, true);
            RedrawWindow(foreground, IntPtr.Zero, IntPtr.Zero,
                RDW_INVALIDATE | RDW_ERASE | RDW_UPDATENOW | RDW_ALLCHILDREN);
        }
    }

    public void MoveTo(int x, int y, int lineHeight = 20)
    {
        lock (_suggestionsLock)
        {
            if (!_isShowing)
            {
                return;
            }
        }

        var width = _lastWidth > 0 ? _lastWidth : 300;
        var height = _lastHeight > 0 ? _lastHeight : OverlayPixelHeight();
        var previousDpi = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorV2);
        try
        {
            (x, y) = PlaceAgainstAnchor(x, y, width, height, lineHeight);
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousDpi);
        }

        if (Math.Abs(x - _lastX) < 4 && Math.Abs(y - _lastY) < 4)
        {
            return;
        }

        _lastX = x;
        _lastY = y;
        _lastWidth = width;
        _lastHeight = height;

        if (_windowHandle != IntPtr.Zero)
        {
            PostMessage(_windowHandle, WM_LEXON_MOVE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private void ApplyPendingMove()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        lock (_suggestionsLock)
        {
            if (!_isShowing)
            {
                return;
            }
        }

        SetWindowPos(_windowHandle, HWND_TOPMOST, _lastX, _lastY, 0, 0, SWP_NOACTIVATE | SWP_NOSIZE);
    }

    public void SelectNext()
    {
        lock (_suggestionsLock)
        {
            if (_currentSuggestions.Count > 0)
            {
                _selectedIndex = (_selectedIndex + 1) % _currentSuggestions.Count;
                EnsureSelectionVisible();
            }
        }
        _ignoreHoverUntilMouseMoves = true;
        _hoverAnchorX = int.MinValue;
        _hoverAnchorY = int.MinValue;
        if (_windowHandle != IntPtr.Zero)
        {
            PostMessage(_windowHandle, WM_LEXON_REPAINT, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public void SelectPrevious()
    {
        lock (_suggestionsLock)
        {
            if (_currentSuggestions.Count > 0)
            {
                _selectedIndex = (_selectedIndex - 1 + _currentSuggestions.Count) % _currentSuggestions.Count;
                EnsureSelectionVisible();
            }
        }
        _ignoreHoverUntilMouseMoves = true;
        _hoverAnchorX = int.MinValue;
        _hoverAnchorY = int.MinValue;
        if (_windowHandle != IntPtr.Zero)
        {
            PostMessage(_windowHandle, WM_LEXON_REPAINT, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public void ConfirmSelection()
    {
        Suggestion? selectedSuggestion = null;
        lock (_suggestionsLock)
        {
            if (!_isShowing)
            {
                return;
            }

            if (_selectedIndex >= 0 && _selectedIndex < _currentSuggestions.Count)
            {
                selectedSuggestion = _currentSuggestions[_selectedIndex];
            }
        }
        if (selectedSuggestion != null)
        {
            lock (_suggestionsLock)
            {
                _isShowing = false;
            }
            ForceHideWindow();
            OnSuggestionSelected(selectedSuggestion);
        }
    }

    public void Dispose()
    {
        _palette.Changed -= OnPaletteChanged;
        _messageThreadCts?.Cancel();
        _messageThread?.Join(1000);
        _messageThreadCts?.Dispose();
        
        if (_windowHandle != IntPtr.Zero)
        {
            DestroyWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }

        // Unregister window class
        UnregisterClass(_className, GetModuleHandle(string.Empty));
        
        _windowReady.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    private void OnSuggestionSelected(Suggestion suggestion)
    {
        SuggestionSelected?.Invoke(this, new SuggestionSelectedEventArgs { SelectedSuggestion = suggestion });
    }

    public Color GetSourceColor(string source)
    {
        return _sourceColors.TryGetValue(source, out var color) ? color : _palette.Muted;
    }
}
