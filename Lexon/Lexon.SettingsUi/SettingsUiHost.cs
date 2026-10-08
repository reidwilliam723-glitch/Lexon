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
    private static SettingsWindow? _settings;
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
    }

    public static Action<Window>? ShowAbout { get; set; }

    internal static Func<ThemeManager?, GallerySettingsServices, SettingsWindow>? SettingsFactory { get; set; }

    public static void ShowSettings(ThemeManager themes, GallerySettingsServices services)
    {
        Warm(themes);
        if (_gallery is { IsVisible: true })
        {
            _gallery.FlushPendingSaves();
            _gallery.Hide();
        }

        var window = EnsureSettings(services);
        ElementHost.EnableModelessKeyboardInterop(window);
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
        window.NotifyShown();
    }

    public static void ShowGallery(ThemeManager themes, GallerySettingsServices? services = null)
    {
        Warm(themes);
        if (_settings is { IsVisible: true })
        {
            _settings.FlushPendingSaves();
            _settings.Hide();
        }

        var window = EnsureGallery(services);
        ElementHost.EnableModelessKeyboardInterop(window);
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
        window.NotifyShown();
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
            _settings?.Destroy();
            _settings = null;
            Application.Current?.Shutdown();
        }
        catch
        {
            // The tray message loop owns process lifetime.
        }
    }

    public static (long ColdMs, long WarmMs) MeasureSettingsOpen(ThemeManager themes, GallerySettingsServices services)
    {
        var cold = System.Diagnostics.Stopwatch.StartNew();
        ShowSettings(themes, services);
        Pump();
        cold.Stop();

        EnsureSettings(services).Hide();
        Pump();

        var warm = System.Diagnostics.Stopwatch.StartNew();
        ShowSettings(themes, services);
        Pump();
        warm.Stop();
        return (cold.ElapsedMilliseconds, warm.ElapsedMilliseconds);
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

    private static void PrepareForWindow()
    {
        if (_themes != null)
        {
            Warm(_themes);
            return;
        }

        WpfBootstrap.EnsureApplication();
    }

    private static void Pump()
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public static SettingsWindow EnsureSettings(GallerySettingsServices services)
    {
        PrepareForWindow();
        if (_settings != null)
        {
            _settings.RetargetHost();
            return _settings;
        }

        _settings = SettingsFactory != null
            ? SettingsFactory(_themes, services)
            : new SettingsWindow(_themes, services, ShowAbout);
        return _settings;
    }

    public static ControlGalleryWindow EnsureGallery(GallerySettingsServices? services = null)
    {
        PrepareForWindow();
        if (_gallery != null)
        {
            return _gallery;
        }

        _gallery = new ControlGalleryWindow(_themes, services);
        _gallery.Closing += (_, e) =>
        {
            e.Cancel = true;
            _gallery.FlushPendingSaves();
            _gallery.Hide();
        };
        return _gallery;
    }
}
