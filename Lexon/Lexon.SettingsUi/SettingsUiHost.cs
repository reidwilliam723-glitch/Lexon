using System.ComponentModel;
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
    private static PropertyChangedEventHandler? _systemParametersHandler;

    public static void Warm(ThemeManager themes)
    {
        _themes = themes;
        var app = WpfBootstrap.EnsureApplication();
        WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);
        HookTheme(themes);
        HookSystemParameters(app);
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
        if (_systemParametersHandler != null)
        {
            SystemParameters.StaticPropertyChanged -= _systemParametersHandler;
            _systemParametersHandler = null;
        }

        if (_themes != null && _themeHooked)
        {
            _themes.ThemeChanged -= OnThemeChanged;
            _themeHooked = false;
        }

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

    private static void HookTheme(ThemeManager themes)
    {
        if (_themeHooked)
        {
            return;
        }

        _themeHooked = true;
        themes.ThemeChanged += OnThemeChanged;
    }

    private static void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        void Apply() => WpfThemeBridge.ApplyTo(app, e.NewTheme);
        if (app.Dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            app.Dispatcher.BeginInvoke(Apply);
        }
    }

    private static void HookSystemParameters(Application app)
    {
        if (_systemParametersHandler != null)
        {
            return;
        }

        _systemParametersHandler = (_, e) =>
        {
            if (e.PropertyName is not (nameof(SystemParameters.HighContrast)
                or nameof(SystemParameters.ClientAreaAnimation)))
            {
                return;
            }

            var themes = _themes;
            if (themes == null)
            {
                return;
            }

            void Apply() => WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);
            if (app.Dispatcher.CheckAccess())
            {
                Apply();
            }
            else
            {
                app.Dispatcher.BeginInvoke(Apply);
            }
        };
        SystemParameters.StaticPropertyChanged += _systemParametersHandler;
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
