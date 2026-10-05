using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
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
public class WritingAppTonePageTests
{
    private readonly WpfStaFixture _sta;

    public WritingAppTonePageTests(WpfStaFixture sta)
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
                    Layout(CreateAppTone());
                    Layout(CreateWriting());
                }

                ApplyWindowsHighContrast(app, themes.CurrentTheme);
                Layout(CreateAppTone());
                Layout(CreateWriting());
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

            foreach (var page in new FrameworkElement[] { CreateAppTone(), CreateWriting() })
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
    public void Gallery_ShowsSixTabsWithAllServices_NoOverflowAt700()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            var settings = new AppSettings();
            var persist = new PersistScheduler(() => { });
            var services = new GallerySettingsServices(settings, new CountingStartup(), persist, persist.Flush)
            {
                ThemeSwitcher = new FakeThemes(),
                AiPolicy = new FakePolicy(),
                ProcessPicker = new FakePicker(),
                ActivityViewer = new FakeLog(),
                Personalization = new FakePersonalization(),
                WritingDialogs = new FakeWritingDialogs(),
                FileDialogs = new FakeFiles(),
                Messages = new FakeMessages(),
                Reload = () => { }
            };
            var window = new ControlGalleryWindow(themes, services)
            {
                Left = -20000,
                Top = -20000,
                Width = 700,
                ShowInTaskbar = false
            };
            window.Show();
            window.UpdateLayout();

            Assert.Equal(6, window.GalleryTabCount);
            Assert.True(window.HasWritingTab);
            Assert.True(window.HasAppToneTab);
            Assert.True(window.ActualWidth <= 700 + 1 || window.Width <= 700 + 1);

            window.SelectWritingTab();
            window.UpdateLayout();
            Assert.True(window.IsWritingPageVisible);
            window.Close();
            _ = app;
        });
    }

    private static AppTonePage CreateAppTone()
    {
        var persist = new PersistScheduler(() => { });
        var vm = new AppToneViewModel(new AppSettings(), persist, new FakePicker());
        vm.Load();
        return new AppTonePage(vm);
    }

    private static WritingPage CreateWriting()
    {
        var persist = new PersistScheduler(() => { });
        var vm = new WritingViewModel(
            new AppSettings(),
            persist,
            new FakePersonalization(),
            new FakeWritingDialogs(),
            new FakeFiles(),
            new FakeMessages());
        vm.Load();
        return new WritingPage(vm);
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
            app.Resources.MergedDictionaries.Remove(existing);
        }

        app.Resources.MergedDictionaries.Add(next);
    }

    private static Window Offscreen(FrameworkElement content, double width, double height)
        => new()
        {
            Content = content,
            Width = width,
            Height = height,
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.ToolWindow
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

    private sealed class TempThemeStore : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "lexon-wpf-write-" + Guid.NewGuid().ToString("N"));

        public TempThemeStore()
        {
            Directory.CreateDirectory(_dir);
            Storage = new EncryptedStorage(_dir);
        }

        public EncryptedStorage Storage { get; }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { /* temp */ }
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

    private sealed class CountingStartup : IStartupRegistration
    {
        public bool IsEnabled() => false;

        public bool TrySetEnabled(bool enabled) => true;
    }

    private sealed class FakeThemes : IThemeSwitcher
    {
        public IReadOnlyList<string> Names { get; } = AppearanceSettingsViewModel.ThemeLabels;

        public string Current { get; set; } = "Light";

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public void Apply(string name) => Current = name;
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

    private sealed class FakePersonalization : IPersonalizationService
    {
        public string GetStyleSummary() => "summary";

        public IReadOnlyList<AdaptationItem> GetAdaptations() => [];

        public void UndoAdaptation(string id)
        {
        }

        public void ResetWritingStyle()
        {
        }

        public string ExportLearningData() => "{}";

        public bool ImportLearningData(string json) => true;
    }

    private sealed class FakeWritingDialogs : IWritingDialogs
    {
        public void ShowWritingStats()
        {
        }

        public void ShowLearnedWords()
        {
        }
    }

    private sealed class FakeFiles : IFileDialogService
    {
        public string? PickSavePath(string filter, string suggestedName) => null;

        public string? PickOpenPath(string filter) => null;
    }

    private sealed class FakeMessages : IMessageService
    {
        public void Info(string text, string caption)
        {
        }

        public bool Confirm(string text, string caption) => false;
    }
}
