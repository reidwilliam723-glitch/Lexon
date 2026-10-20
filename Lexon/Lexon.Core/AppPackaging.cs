using System.Runtime.InteropServices;

namespace Lexon.Core;

/// <summary>
/// Whether this process has Windows package identity (MSIX / Store).
/// Cached for the process lifetime; override only for tests.
/// </summary>
public static class AppPackaging
{
    private const int AppModelErrorNoPackage = 15700;

    private static readonly object Gate = new();
    private static bool? _override;
    private static bool? _cached;

    /// <summary>
    /// True when running inside an MSIX / Store package.
    /// </summary>
    public static bool IsPackaged
    {
        get
        {
            lock (Gate)
            {
                if (_override is bool forced)
                {
                    return forced;
                }

                return _cached ??= Detect();
            }
        }
    }

    /// <summary>
    /// Test-only override. Pass <c>null</c> to clear and use real detection again.
    /// </summary>
    internal static void SetOverride(bool? packaged)
    {
        lock (Gate)
        {
            _override = packaged;
        }
    }

    private static bool Detect()
    {
        try
        {
            var length = 0;
            var hr = GetCurrentPackageFullName(ref length, null);
            if (hr == AppModelErrorNoPackage)
            {
                return false;
            }

            // First call returns ERROR_INSUFFICIENT_BUFFER (122) when packaged.
            if (length <= 0)
            {
                return hr == 0;
            }

            var buffer = new char[length];
            hr = GetCurrentPackageFullName(ref length, buffer);
            return hr == 0;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}
