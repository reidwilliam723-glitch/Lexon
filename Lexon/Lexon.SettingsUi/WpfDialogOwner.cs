using System.Windows;
using System.Windows.Interop;
using IWin32Window = System.Windows.Forms.IWin32Window;

namespace Lexon.SettingsUi;

public static class WpfDialogOwner
{
    public static IWin32Window From(Window window) => new Owner(window);

    private sealed class Owner : IWin32Window
    {
        public Owner(Window window)
        {
            Handle = new WindowInteropHelper(window).EnsureHandle();
        }

        public IntPtr Handle { get; }
    }
}
