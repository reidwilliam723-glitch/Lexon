using System.Windows;
using System.Windows.Forms.Integration;
using Lexon.Core.Theming;

namespace Lexon.SettingsUi;

/// <summary>
/// Hosts WPF windows inside the WinForms <c>Application.Run()</c> loop.
/// No WPF Application shutdown is used; the tray owns process lifetime.
/// </summary>
public static class SettingsUiHost
{
    private static ControlGalleryWindow? _gallery;
    private static ThemeManager? _themes;
    private static bool _themeHooked;

    public static void Warm(ThemeManager themes)
    {
        _themes = themes;
        var app = WpfBootstrap.EnsureApplication();
        WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);
        if (!_themeHooked)
        {
            _themeHooked = true;
            themes.ThemeChanged += (_, e) => WpfThemeBridge.ApplyTo(app, e.NewTheme);
        }

        _ = EnsureGallery();
    }

    public static void ShowGallery(ThemeManager themes)
    {
        Warm(themes);
        var window = EnsureGallery();
        ElementHost.EnableModelessKeyboardInterop(window);
        window.Show();
        window.Activate();
    }

    public static void Shutdown()
    {
        try
        {
            Application.Current?.Shutdown();
        }
        catch
        {
            // The tray message loop owns process lifetime.
        }
    }

    public static (long ColdMs, long WarmMs) MeasureGalleryOpen(ThemeManager themes)
    {
        var cold = System.Diagnostics.Stopwatch.StartNew();
        ShowGallery(themes);
        Pump();
        cold.Stop();

        EnsureGallery().Hide();
        Pump();

        var warm = System.Diagnostics.Stopwatch.StartNew();
        ShowGallery(themes);
        Pump();
        warm.Stop();
        return (cold.ElapsedMilliseconds, warm.ElapsedMilliseconds);
    }

    private static void Pump()
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public static ControlGalleryWindow EnsureGallery()
    {
        if (_gallery != null)
        {
            return _gallery;
        }

        _gallery = new ControlGalleryWindow(_themes);
        _gallery.Closing += (_, e) =>
        {
            e.Cancel = true;
            _gallery.Hide();
        };
        return _gallery;
    }
}
