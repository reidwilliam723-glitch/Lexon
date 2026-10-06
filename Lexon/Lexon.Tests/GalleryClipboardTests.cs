using System.Windows;
using Lexon.AI;
using Lexon.Core.Theming;
using Lexon.SettingsModel;
using Lexon.SettingsUi;
using Lexon.Storage;
using Xunit;

namespace Lexon.Tests;

[Collection("WpfSta")]
public class GalleryClipboardTests
{
    private readonly WpfStaFixture _sta;

    public GalleryClipboardTests(WpfStaFixture sta)
    {
        _sta = sta;
    }

    [Fact]
    public void Gallery_ClipboardListener_OnlyWhileWatching()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            var window = CreateAiGallery(themes);
            window.Show();
            window.UpdateLayout();
            Assert.False(window.ClipboardListener.IsListening);

            window.SelectAiTab();
            window.UpdateLayout();
            var vm = window.AiViewModel!;
            Assert.False(window.ClipboardListener.IsListening);

            vm.GetApiKey();
            Assert.True(vm.IsWaitingForClipboard);
            Assert.True(window.ClipboardListener.IsListening);

            vm.CancelWait();
            Assert.False(window.ClipboardListener.IsListening);

            vm.GetApiKey();
            Assert.True(window.ClipboardListener.IsListening);
            vm.OnTabLeft();
            Assert.False(window.ClipboardListener.IsListening);

            vm.GetApiKey();
            Assert.True(window.ClipboardListener.IsListening);
            vm.OnWindowHidden();
            Assert.False(window.ClipboardListener.IsListening);

            vm.GetApiKey();
            Assert.True(window.ClipboardListener.IsListening);
            vm.OnLocalOnlyChanged(true);
            Assert.False(window.ClipboardListener.IsListening);

            vm.OnLocalOnlyChanged(false);
            vm.GetApiKey();
            Assert.True(window.ClipboardListener.IsListening);
            vm.ShowMoreProviders();
            vm.ProviderIndex = Array.FindIndex(
                AiProviderCatalog.AllProviders,
                p => p.Equals("Gemini", StringComparison.OrdinalIgnoreCase));
            Assert.False(window.ClipboardListener.IsListening);

            vm.GetApiKey();
            Assert.True(window.ClipboardListener.IsListening);
            window.Close();
            Assert.False(window.ClipboardListener.IsListening);
            _ = app;
        });
    }

    [Fact]
    public void AiPage_ShowHide_DoesNotProbe()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            var probeCalls = 0;
            var settings = new AppSettings();
            var persist = new PersistScheduler(() => { });
            var session = new AiProbeSession
            {
                ProbeAsync = (_, _, _, _) =>
                {
                    probeCalls++;
                    return Task.FromResult(AiProbeResult.Ok("ok"));
                }
            };
            var delays = new ControllableDelays();
            var services = new GallerySettingsServices(settings, new CountingStartup(), persist, persist.Flush)
            {
                AiPolicy = new FakePolicy(),
                AiSession = session,
                UrlLauncher = new FakeUrls(),
                DelayScheduler = delays,
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
            window.SelectAiTab();
            window.UpdateLayout();

            var vm = window.AiViewModel!;
            probeCalls = 0;
            vm.ApiKeyText = "sk-test-show-hide-key";
            delays.RunProbeDelay();
            Pump();
            Assert.Equal(1, probeCalls);

            var keyBox = FindVisual<RevealPasswordBox>(window)!;
            keyBox.ApplyTemplate();
            var password = keyBox.Template.FindName("PART_Password", keyBox) as System.Windows.Controls.PasswordBox;
            var reveal = keyBox.Template.FindName("PART_Reveal", keyBox) as System.Windows.Controls.Primitives.ToggleButton;
            Assert.NotNull(password);
            Assert.NotNull(reveal);

            password!.Focus();
            Pump();
            reveal!.Focus();
            reveal.IsChecked = true;
            Pump();
            Assert.Equal(1, probeCalls);

            // Leaving the whole control still probes at most once more if needed;
            // after Connected with the same key it must stay at 1.
            window.SelectGeneralTab();
            Pump();
            Assert.Equal(1, probeCalls);

            window.Close();
            _ = app;
        });
    }

    [Fact]
    public void Gallery_ClipboardMessages_OnlyWhileListening()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempThemeStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);

            var window = CreateAiGallery(themes);
            window.Show();
            window.UpdateLayout();
            window.SelectAiTab();
            var listener = window.ClipboardListener;
            var before = listener.ClipboardMessagesSeen;

            System.Windows.Clipboard.SetText("hello-not-watching-" + Guid.NewGuid().ToString("N"));
            Pump();
            Assert.Equal(before, listener.ClipboardMessagesSeen);

            window.AiViewModel!.GetApiKey();
            Assert.True(listener.IsListening);
            before = listener.ClipboardMessagesSeen;
            System.Windows.Clipboard.SetText("still-not-a-key-" + Guid.NewGuid().ToString("N"));
            Pump();
            Assert.True(listener.ClipboardMessagesSeen > before);
            Assert.Equal(string.Empty, window.AiViewModel.ApiKeyText);
            Assert.True(window.AiViewModel.IsWaitingForClipboard);

            window.Close();
            _ = app;
        });
    }

    private static ControlGalleryWindow CreateAiGallery(ThemeManager themes)
    {
        var settings = new AppSettings();
        var persist = new PersistScheduler(() => { });
        var services = new GallerySettingsServices(settings, new CountingStartup(), persist, persist.Flush)
        {
            AiPolicy = new FakePolicy(),
            AiSession = new AiProbeSession
            {
                ProbeAsync = (_, _, _, _) => Task.FromResult(AiProbeResult.Ok("ok"))
            },
            UrlLauncher = new FakeUrls(),
            DelayScheduler = new FakeDelays(),
            // Real GalleryClipboardWatch — do not inject a fake.
            Reload = () => { }
        };
        return new ControlGalleryWindow(themes, services)
        {
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false
        };
    }

    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static T? FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            return match;
        }

        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            var found = FindVisual<T>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private sealed class CountingStartup : IStartupRegistration
    {
        public bool IsEnabled() => false;

        public bool TrySetEnabled(bool enabled) => true;
    }

    private sealed class FakePolicy : IAiPolicyPublisher
    {
        public void Publish(bool localOnly, bool typing, bool rewrite, bool prefetch)
        {
        }
    }

    private sealed class FakeUrls : IUrlLauncher
    {
        public bool TryOpen(string url) => true;
    }

    private sealed class FakeDelays : IDelayScheduler
    {
        public IDisposable Schedule(TimeSpan delay, Action action) => new Noop();

        private sealed class Noop : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class ControllableDelays : IDelayScheduler
    {
        private readonly List<(TimeSpan Delay, Action Action)> _pending = [];

        public IDisposable Schedule(TimeSpan delay, Action action)
        {
            _pending.Add((delay, action));
            return new Handle(this, action);
        }

        public void RunProbeDelay()
        {
            var hit = _pending.Where(p => p.Delay == AiSettingsViewModel.KeyProbeDelay).ToList();
            _pending.RemoveAll(p => p.Delay == AiSettingsViewModel.KeyProbeDelay);
            foreach (var item in hit)
            {
                item.Action();
            }
        }

        private sealed class Handle : IDisposable
        {
            private readonly ControllableDelays _owner;
            private readonly Action _action;

            public Handle(ControllableDelays owner, Action action)
            {
                _owner = owner;
                _action = action;
            }

            public void Dispose() => _owner._pending.RemoveAll(p => p.Action == _action);
        }
    }

    private sealed class TempThemeStore : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "lexon-gallery-clip-" + Guid.NewGuid().ToString("N"));

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
}
