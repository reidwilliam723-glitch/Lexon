using System.Windows.Automation;
using Lexon.Core;

namespace Lexon.Input;

/// <summary>
/// Password detection before any text is read. Native ES_PASSWORD, then UIA
/// IsPassword. If neither can answer, fail closed.
/// </summary>
internal static class SecureFieldProbe
{
    private static IntPtr _cachedControl;
    private static bool _cachedSecure;
    private static long _cachedAt;

    public static bool IsSecure(IntPtr controlHandle)
    {
        var now = Environment.TickCount64;
        if (controlHandle != IntPtr.Zero
            && controlHandle == _cachedControl
            && now - _cachedAt < 250)
        {
            return _cachedSecure;
        }

        var secure = Probe(controlHandle);
        _cachedControl = controlHandle;
        _cachedSecure = secure;
        _cachedAt = now;
        return secure;
    }

    private static bool Probe(IntPtr controlHandle)
    {
        var nativePassword = controlHandle != IntPtr.Zero && NativePasswordStyle(controlHandle);
        var uia = controlHandle == IntPtr.Zero ? null : TryUiaIsPassword(controlHandle);
        return SecureFieldPolicy.IsSecure(nativePassword, uia, controlHandle != IntPtr.Zero);
    }

    private static bool NativePasswordStyle(IntPtr controlHandle)
    {
        const int gwlStyle = -16;
        const long esPassword = 0x0020;
        try
        {
            var style = GetWindowLongPtr(controlHandle, gwlStyle).ToInt64();
            return (style & esPassword) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool? TryUiaIsPassword(IntPtr controlHandle)
    {
        try
        {
            var element = AutomationElement.FromHandle(controlHandle);
            if (element == null)
            {
                return null;
            }

            return element.Current.IsPassword;
        }
        catch
        {
            return null;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
}
