using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Lexon.SettingsUi;

public sealed class ClipboardHwndListener : IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private HwndSource? _source;
    private bool _listening;

    public event EventHandler? ClipboardUpdated;

    public void Attach(Window window)
    {
        Detach();
        _source = (HwndSource?)PresentationSource.FromVisual(window) ?? HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle());
        if (_source == null)
        {
            return;
        }

        _source.AddHook(Hook);
        _listening = AddClipboardFormatListener(_source.Handle);
    }

    /// <summary>True when a clipboard format listener is registered on the window.</summary>
    public bool IsListening => _listening;

    public void Detach()
    {
        if (_source != null)
        {
            if (_listening)
            {
                RemoveClipboardFormatListener(_source.Handle);
            }

            _source.RemoveHook(Hook);
            _source = null;
        }

        _listening = false;
    }

    public void Dispose() => Detach();

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmClipboardUpdate)
        {
            ClipboardUpdated?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
