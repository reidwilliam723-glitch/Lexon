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
public class AppearancePrivacyPageTests
{
    private readonly WpfStaFixture _sta;

    public AppearancePrivacyPageTests(WpfStaFixture sta)
    {
        _sta = sta;
    }

    [Fact]
    public void Pages_LayOutInEveryTheme_WithoutBindingErrors()
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
                    Layout(CreateAppearance());
                    Layout(CreatePrivacy());
                }

                ApplyWindowsHighContrast(app, themes.CurrentTheme);
                Layout(CreateAppearance());
                Layout(CreatePrivacy());
                Assert.Empty(listener.Errors);
            }
            finally
            {
                listener.Detach();
            }
        });
    }

    [Fact]
    public void Pages_InteractiveControlsHaveAutomationNames()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            foreach (var page in new FrameworkElement[] { CreateAppearance(), CreatePrivacy() })
            {
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
            }

            _ = app;
        });
    }

    [Fact]
    public void Gallery_ShowsOnlyAvailableSettingsTabs()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            var settings = new AppSettings { Theme = "Dark", LocalMode = true };
            var persist = new PersistScheduler(() => { });
            var themeSwitch = new FakeThemes();
            var policy = new FakePolicy();
            var services = new GallerySettingsServices(settings, new CountingStartup(), persist, persist.Flush)
            {
                ThemeSwitcher = themeSwitch,
                AiPolicy = policy,
                ProcessPicker = new FakePicker(),
                ActivityViewer = new FakeLog(),
                Reload = () => { }
            };
            var window = new ControlGalleryWindow(themes, services)
            {
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false
            };
            window.Show();
            window.UpdateLayout();

            Assert.Equal(5, window.GalleryTabCount);
            Assert.True(window.HasGeneralTab);
            Assert.True(window.HasAppearanceTab);
            Assert.True(window.HasPrivacyTab);
            Assert.True(window.HasAppToneTab);

            window.SelectAppearanceTab();
            window.UpdateLayout();
            Assert.True(window.IsAppearancePageVisible);
            Assert.Equal(1, window.AppearanceViewModel!.ThemeIndex);
            Assert.Empty(themeSwitch.Applied);

            window.SelectPrivacyTab();
            window.UpdateLayout();
            Assert.True(window.IsPrivacyPageVisible);
            Assert.True(window.PrivacyViewModel!.LocalOnly);
            window.Close();
            _ = app;
        });
    }

    private static AppearancePage CreateAppearance()
    {
        var persist = new PersistScheduler(() => { });
        var vm = new AppearanceSettingsViewModel(new AppSettings(), persist, new FakeThemes());
        vm.Load();
        return new AppearancePage(vm);
    }

    private static PrivacyPage CreatePrivacy()
    {
        var persist = new PersistScheduler(() => { });
        var vm = new PrivacySettingsViewModel(new AppSettings(), persist, new FakePolicy(), new FakePicker(), new FakeLog());
        vm.Load();
        return new PrivacyPage(vm);
    }

    private static void Layout(FrameworkElement page)
    {
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
        public bool IsEnabled() => false;

        public bool TrySetEnabled(bool enabled) => true;
    }

    private sealed class FakeThemes : IThemeSwitcher
    {
        public List<string> Applied { get; } = [];

        public IReadOnlyList<string> Names { get; } = AppearanceSettingsViewModel.ThemeLabels;

        public string Current { get; set; } = "Light";

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public void Apply(string name)
        {
            Applied.Add(name);
            Current = name;
        }
    }

    private sealed class FakePolicy : IAiPolicyPublisher
    {
        public void Publish(bool localOnly, bool typing, bool rewrite, bool prefetch)
        {
        }
    }

    private sealed class FakePicker : IProcessPicker
    {
        public string? Pick(string prompt) => null;
    }

    private sealed class FakeLog : ICloudAiActivityViewer
    {
        public void Show()
        {
        }
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
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "lexon-appear-ui-" + Guid.NewGuid().ToString("N"));

        public TempThemeStore()
        {
            Directory.CreateDirectory(_dir);
            Storage = new EncryptedStorage(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { /* temp */ }
        }
    }
}
