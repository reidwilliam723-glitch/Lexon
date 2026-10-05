using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using Lexon.Core.Theming;
using Lexon.SettingsModel;
using Lexon.SettingsUi;
using Lexon.Storage;
using Xunit;
using WpfApplication = System.Windows.Application;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfPasswordBox = System.Windows.Controls.PasswordBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Lexon.Tests;

[Collection("WpfSta")]
public class GeneralPageTests
{
    private readonly WpfStaFixture _sta;

    public GeneralPageTests(WpfStaFixture sta)
    {
        _sta = sta;
    }

    [Fact]
    public void GeneralPage_LaysOutInEveryTheme_WithoutBindingErrors()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            var listener = BindListener.Attach();
            try
            {
                foreach (var theme in themes.AvailableThemes)
                {
                    WpfThemeBridge.ApplyTo(app, theme);
                    LayoutPage();
                }

                ApplyWindowsHighContrast(app, themes.CurrentTheme);
                LayoutPage();
                Assert.Empty(listener.Errors);
            }
            finally
            {
                listener.Detach();
            }
        });
    }

    [Fact]
    public void GeneralPage_InteractiveControlsHaveAutomationNames()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            var page = CreatePage();
            var window = Offscreen(page, 700, 720);
            window.Show();
            window.UpdateLayout();
            page.ApplyTemplate();
            page.UpdateLayout();

            var missing = Logical(page)
                .OfType<FrameworkElement>()
                .Where(IsInteractive)
                .Where(el => string.IsNullOrWhiteSpace(AutomationProperties.GetName(el)))
                .Select(el => el.GetType().Name)
                .ToList();
            Assert.Empty(missing);
            window.Close();
            _ = app;
        });
    }

    [Fact]
    public void Gallery_WithoutServices_HasNoGeneralTab()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            var window = new ControlGalleryWindow(themes);
            window.Left = -20000;
            window.Top = -20000;
            window.ShowInTaskbar = false;
            window.Show();
            window.UpdateLayout();

            Assert.False(window.HasGeneralTab);
            Assert.False(window.IsGeneralPageVisible);
            Assert.Null(window.GeneralViewModel);
            window.Close();
            _ = app;
        });
    }

    [Fact]
    public void GeneralPage_DoesNotOverflowAtNarrowWidth()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            var page = CreatePage();
            var window = Offscreen(page, 700, 720);
            window.Show();
            window.UpdateLayout();
            page.UpdateLayout();

            var scroll = Logical(page).OfType<System.Windows.Controls.ScrollViewer>().First();
            Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1,
                $"Extent {scroll.ExtentWidth} > viewport {scroll.ViewportWidth}");
            window.Close();
            _ = app;
        });
    }

    [Fact]
    public void Gallery_OpenFullScreen_MaximizesAndRestores()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            var settings = new AppSettings();
            var persist = new PersistScheduler(() => { });
            var services = new GallerySettingsServices(settings, new CountingStartup(), persist, persist.Flush);
            var window = new ControlGalleryWindow(themes, services)
            {
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false,
                WindowState = WindowState.Normal
            };
            window.Show();
            window.UpdateLayout();
            Assert.Equal(WindowState.Normal, window.WindowState);

            window.SelectGeneralTab();
            window.GeneralViewModel!.OpenSettingsFullScreen = true;
            Assert.Equal(WindowState.Maximized, window.WindowState);
            window.GeneralViewModel.OpenSettingsFullScreen = false;
            Assert.Equal(WindowState.Normal, window.WindowState);
            window.Close();
            _ = app;
        });
    }

    [Fact]
    public void Gallery_WithServices_SelectingGeneralShowsPageAndLoads()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            var settings = new AppSettings();
            var startup = new CountingStartup();
            var persist = new PersistScheduler(() => { });
            var services = new GallerySettingsServices(settings, startup, persist, persist.Flush);
            var window = new ControlGalleryWindow(themes, services)
            {
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false
            };
            window.Show();
            window.UpdateLayout();

            Assert.True(window.HasGeneralTab);
            Assert.False(window.IsGeneralPageVisible);
            var loadsBeforeSelect = startup.GetCalls;

            window.SelectGeneralTab();
            window.UpdateLayout();

            Assert.True(window.IsGeneralPageVisible);
            Assert.NotNull(window.GeneralViewModel);
            Assert.True(startup.GetCalls > loadsBeforeSelect);
            window.Close();
            _ = app;
        });
    }

    private static GeneralPage CreatePage()
    {
        var persist = new PersistScheduler(() => { });
        var vm = new GeneralSettingsViewModel(new AppSettings(), new CountingStartup(), persist);
        vm.Load();
        return new GeneralPage(vm);
    }

    private static void LayoutPage()
    {
        var page = CreatePage();
        var window = Offscreen(page, 700, 720);
        window.Show();
        window.UpdateLayout();
        page.ApplyTemplate();
        page.UpdateLayout();
        Pump(20);
        window.Close();
    }

    private static bool IsInteractive(FrameworkElement element)
        => element is WpfButtonBase or WpfComboBox or WpfTextBox or WpfListBox or WpfPasswordBox;

    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in Logical(child))
            {
                yield return nested;
            }
        }
    }

    private static void ApplyWindowsHighContrast(WpfApplication app, Theme theme)
    {
        var next = WpfThemeBridge.Create(theme, windowsHighContrast: true);
        next["LexonThemeId"] = WpfThemeBridge.DictionaryKey;
        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => Equals(d["LexonThemeId"], WpfThemeBridge.DictionaryKey));
        if (existing != null)
        {
            app.Resources.MergedDictionaries[app.Resources.MergedDictionaries.IndexOf(existing)] = next;
        }
        else
        {
            app.Resources.MergedDictionaries.Insert(0, next);
        }
    }

    private static Window Offscreen(UIElement content, double width, double height)
        => new()
        {
            Width = width,
            Height = height,
            Content = content,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Opacity = 0,
            Left = -20000,
            Top = -20000
        };

    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private sealed class CountingStartup : IStartupRegistration
    {
        public int GetCalls;

        public bool IsEnabled()
        {
            GetCalls++;
            return false;
        }

        public bool TrySetEnabled(bool enabled) => true;
    }

    private sealed class BindListener : TraceListener
    {
        public List<string> Errors { get; } = [];

        public static BindListener Attach()
        {
            var listener = new BindListener();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            return listener;
        }

        public void Detach()
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
        }

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Errors.Add(message);
            }
        }
    }

    private sealed class TempThemeStore : IDisposable
    {
        public EncryptedStorage Storage { get; }
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "lexon-general-ui-" + Guid.NewGuid().ToString("N"));

        public TempThemeStore()
        {
            Directory.CreateDirectory(_dir);
            Storage = new EncryptedStorage(_dir);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch
            {
                // temp
            }
        }
    }
}
