using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Lexon.Core.Theming;
using Lexon.Profiles;
using Lexon.Settings;
using Lexon.SettingsModel;
using Lexon.SettingsUi;
using Lexon.Storage;
using Xunit;

namespace Lexon.Tests;

[Collection("WpfSta")]
public class SettingsWindowTests
{
    private readonly WpfStaFixture _sta;

    public SettingsWindowTests(WpfStaFixture sta) => _sta = sta;

    [Fact]
    public void ShowsSixTabsInOrder_WithoutControls()
    {
        _sta.Run(() =>
        {
            var window = Show(FullServices(out _));
            Assert.Equal(["General", "AI", "Privacy", "Appearance", "App tone", "Writing", "Apps"], window.TabNames);
            Assert.DoesNotContain("Controls", window.TabNames);
            Assert.True(window.IsGeneralPageVisible);
            window.Destroy();
        });
    }

    [Fact]
    public void Close_FlushesDirtyPages_ThenHidesAndReopens()
    {
        _sta.Run(() =>
        {
            var services = FullServices(out var profile);
            var window = Show(services);
            window.GeneralViewModel!.CheckForUpdates = false;
            Assert.True(window.GeneralViewModel.IsDirty);

            window.Close();

            Assert.False(window.IsVisible);
            Assert.False(profile.GetSetting(AppSettings.EnableAutoUpdatesKey, true));
            window.Show();
            Assert.True(window.IsVisible);
            window.Destroy();
        });
    }

    [Fact]
    public void CleanPages_DoNotWriteOnDeactivate()
    {
        _sta.Run(() =>
        {
            var services = FullServices(out var profile);
            profile.SetSetting(AppSettings.ThemeKey, "System");
            var window = Show(services);
            window.NotifyDeactivated();
            Assert.Equal("System", profile.GetSetting(AppSettings.ThemeKey, string.Empty));
            Assert.False(window.GeneralViewModel!.IsDirty);
            window.Destroy();
        });
    }

    [Fact]
    public void Activate_ReloadsCleanPages_AndLeavesDirtyPages()
    {
        _sta.Run(() =>
        {
            var services = FullServices(out var profile);
            var window = Show(services);
            var general = window.GeneralViewModel!;
            Assert.True(general.CheckForUpdates);

            profile.SetSetting(AppSettings.EnableAutoUpdatesKey, false);
            services.Settings.Read(profile);
            window.NotifyActivated();
            Assert.False(general.CheckForUpdates);

            general.MinimizeToTray = false;
            Assert.True(general.IsDirty);
            profile.SetSetting(AppSettings.MinimizeToTrayKey, true);
            services.Settings.Read(profile);
            window.NotifyActivated();
            Assert.False(general.MinimizeToTray);

            window.NotifyDeactivated();
            Assert.False(profile.GetSetting(AppSettings.MinimizeToTrayKey, true));
            window.Destroy();
        });
    }

    [Fact]
    public void ClassicWrite_ThenActivate_ShowsNewerValue_AndTheReverse()
    {
        _sta.Run(() =>
        {
            var services = FullServices(out var profile);
            var window = Show(services);
            profile.SetSetting(AppSettings.EnableAutoUpdatesKey, false);
            services.Settings.Read(profile);
            window.NotifyActivated();
            Assert.False(window.GeneralViewModel!.CheckForUpdates);

            window.GeneralViewModel.MinimizeToTray = false;
            window.FlushPendingSaves();
            var classic = new AppSettings();
            classic.Read(profile);
            Assert.False(classic.MinimizeToTray);
            window.Destroy();
        });
    }

    [Fact]
    public void TabLabels_UseThemeColourBeforeTheyAreClicked()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            var window = new SettingsWindow(null, FullServices(out _))
            {
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false
            };
            var themes = new ThemeManager(new EncryptedStorage(Path.Combine(Path.GetTempPath(), "LexonTabColor" + Guid.NewGuid().ToString("N"))));
            var dark = themes.AvailableThemes.Single(theme => theme.Name == "Dark");
            WpfThemeBridge.ApplyTo(app, dark);
            window.Show();
            window.UpdateLayout();

            var labels = FindTabLabels(window);
            Assert.Equal(window.TabNames.Count, labels.Count);
            var text = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark.Colors.Text)!;
            var primary = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark.Colors.Primary)!;
            Assert.Equal(primary, ((SolidColorBrush)labels[0].Foreground).Color);
            for (var i = 1; i < labels.Count; i++)
            {
                Assert.Equal(text, ((SolidColorBrush)labels[i].Foreground).Color);
            }

            window.Destroy();
        });
    }

    [Fact]
    public void OffScreenBounds_AreClampedOntoAWorkArea()
    {
        var work = new[] { new Rect(0, 0, 1920, 1080) };
        var clamped = SettingsWindowPlacement.Clamp(new Rect(-10000, -8000, 720, 720), work);
        Assert.True(clamped.Left >= 0);
        Assert.True(clamped.Top >= 0);
        Assert.True(clamped.Right <= 1920);
        Assert.True(clamped.Bottom <= 1080);
        Assert.Equal(720, clamped.Width);
        Assert.Equal(720, clamped.Height);
    }

    [Fact]
    public void OpenFullScreen_IsAppliedOnShow()
    {
        _sta.Run(() =>
        {
            var services = FullServices(out var profile);
            profile.SetSetting(AppSettings.OpenSettingsFullScreenKey, true);
            services.Settings.OpenSettingsFullScreen = true;
            var window = Show(services);
            Assert.Equal(WindowState.Maximized, window.WindowState);
            window.Destroy();
        });
    }

    [Fact]
    public void CtrlTab_CyclesPages()
    {
        _sta.Run(() =>
        {
            var window = Show(FullServices(out _));
            window.Cycle(reverse: false);
            Assert.Equal("AI", window.TabNames[1]);
            Assert.False(window.IsGeneralPageVisible);
            window.Cycle(reverse: true);
            Assert.True(window.IsGeneralPageVisible);
            window.Destroy();
        });
    }

    [Fact]
    public void About_OpensAboutFormOwnedByTheSettingsWindow()
    {
        _sta.Run(() =>
        {
            AboutForm? about = null;
            var services = FullServices(out _);
            var window = new SettingsWindow(null, services, owner =>
            {
                about = new AboutForm();
                about.Show(WpfDialogOwner.From(owner));
            })
            {
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false
            };
            window.Show();
            var button = Find(window).OfType<System.Windows.Controls.Button>().First(item => AutomationProperties.GetName(item) == "About");
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Assert.NotNull(about);
            var owner = GetWindow(about!.Handle, GwOwner);
            Assert.Equal(new WindowInteropHelper(window).Handle, owner);
            about.Close();
            window.Destroy();
        });
    }

    [Fact]
    public void Router_DefaultOpensSettingsWindow_ClassicOpensForm_AndDoesNotWarmWpf()
    {
        var normal = new SettingsRouter(classicSettings: false, galleryEnabled: true);
        Assert.Equal(SettingsSurface.SettingsWindow, normal.Resolve(SettingsEntryPoint.Tray));
        Assert.Equal(SettingsSurface.SettingsWindow, normal.Resolve(SettingsEntryPoint.Shortcut));
        Assert.Equal(SettingsSurface.SettingsWindow, normal.Resolve(SettingsEntryPoint.ShowEvent));
        Assert.Equal(SettingsSurface.Gallery, normal.Resolve(SettingsEntryPoint.GalleryMenu));
        Assert.True(normal.ShouldWarmWpf);

        var classic = new SettingsRouter(classicSettings: true, galleryEnabled: false);
        Assert.Equal(SettingsSurface.SettingsForm, classic.Resolve(SettingsEntryPoint.Tray));
        Assert.Equal(SettingsSurface.SettingsForm, classic.Resolve(SettingsEntryPoint.Shortcut));
        Assert.Equal(SettingsSurface.SettingsForm, classic.Resolve(SettingsEntryPoint.ShowEvent));
        Assert.False(classic.ShouldWarmWpf);
    }

    [Fact]
    public void Router_SecondWpfFailure_StopsFurtherWpfAttempts()
    {
        var router = new SettingsRouter(classicSettings: false, galleryEnabled: false);
        var opened = router.OpenOrFallback(SettingsEntryPoint.Tray, () => throw new InvalidOperationException("boom"));
        Assert.Equal(SettingsSurface.SettingsForm, opened);
        Assert.Equal(SettingsSurface.SettingsWindow, router.Resolve(SettingsEntryPoint.Shortcut));

        var second = router.OpenOrFallback(SettingsEntryPoint.Shortcut, () => throw new InvalidOperationException("boom"));
        Assert.Equal(SettingsSurface.SettingsForm, second);
        Assert.Equal(SettingsSurface.SettingsForm, router.Resolve(SettingsEntryPoint.ShowEvent));
        Assert.False(router.ShouldWarmWpf);
    }

    private static List<TextBlock> FindTabLabels(DependencyObject root)
    {
        var found = new List<TextBlock>();
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is NavItem item)
            {
                item.ApplyTemplate();
                found.AddRange(FindTabLabels(item));
            }
            else if (child is TextBlock block && block.TemplatedParent is NavItem)
            {
                found.Add(block);
            }
            else
            {
                found.AddRange(FindTabLabels(child));
            }
        }

        return found;
    }

    private static SettingsWindow Show(GallerySettingsServices services)
    {
        WpfBootstrap.EnsureApplication();
        var window = new SettingsWindow(null, services)
        {
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false
        };
        window.Show();
        window.NotifyShown();
        window.UpdateLayout();
        return window;
    }

    private static GallerySettingsServices FullServices(out Profile profile)
    {
        var stored = new Profile();
        profile = stored;
        var settings = new AppSettings();
        settings.Write(stored);
        GallerySettingsServices services = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(stored, services.Pages.ToArray()));
        services = new GallerySettingsServices(settings, new NoStartup(), persist, persist.Flush)
        {
            Profile = stored,
            Reload = () => settings.Read(stored),
            ThemeSwitcher = new NoThemes(),
            AiPolicy = new NoPolicy(),
            AiSession = new AiProbeSession(),
            UrlLauncher = new NoUrls(),
            DelayScheduler = new NoDelays(),
            ClipboardWatch = new NoClipboard(),
            ProcessPicker = new NoPicker(),
            ActivityViewer = new NoLog(),
            Personalization = new NoPersonalization(),
            WritingDialogs = new NoWritingDialogs(),
            FileDialogs = new NoFiles(),
            Messages = new NoMessages()
        };
        return services;
    }

    private static IEnumerable<DependencyObject> Find(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in Find(child))
            {
                yield return nested;
            }
        }
    }

    private const uint GwOwner = 4;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    private sealed class NoStartup : IStartupRegistration
    {
        public bool IsEnabled() => false;

        public bool TrySetEnabled(bool enabled) => true;
    }

    private sealed class NoThemes : IThemeSwitcher
    {
        public IReadOnlyList<string> Names { get; } = ["Light", "Dark", "High Contrast"];

        public string Current => "Light";

        public void Apply(string name)
        {
        }

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        private void Unused() => ThemeChanged?.Invoke(this, null!);
    }

    private sealed class NoPolicy : IAiPolicyPublisher
    {
        public void Publish(bool localOnly, bool typing, bool rewrite, bool prefetch)
        {
        }
    }

    private sealed class NoUrls : IUrlLauncher
    {
        public bool TryOpen(string url) => true;
    }

    private sealed class NoDelays : IDelayScheduler
    {
        public IDisposable Schedule(TimeSpan delay, Action action) => new Noop();

        private sealed class Noop : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class NoClipboard : IClipboardWatch
    {
        public event EventHandler? Updated;

        public bool Start() => true;

        public void Stop()
        {
        }

        public string? ReadText() => null;

        private void Unused() => Updated?.Invoke(this, EventArgs.Empty);
    }

    private sealed class NoPicker : IProcessPicker
    {
        public string? Pick(string prompt) => null;
    }

    private sealed class NoLog : ICloudAiActivityViewer
    {
        public void Show()
        {
        }
    }

    private sealed class NoPersonalization : IPersonalizationService
    {
        public string GetStyleSummary() => string.Empty;

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

    private sealed class NoWritingDialogs : IWritingDialogs
    {
        public void ShowWritingStats()
        {
        }

        public void ShowLearnedWords()
        {
        }

        public void ShowTerminology(
            IReadOnlyList<string> globalTerms,
            IReadOnlyList<string> appOverrideRows,
            Action<IReadOnlyList<string>, IReadOnlyList<string>> onApply)
        {
        }
    }

    private sealed class NoFiles : IFileDialogService
    {
        public string? PickSavePath(string filter, string suggestedName) => null;

        public string? PickOpenPath(string filter) => null;
    }

    private sealed class NoMessages : IMessageService
    {
        public void Info(string text, string caption)
        {
        }

        public bool Confirm(string text, string caption) => false;
    }
}
