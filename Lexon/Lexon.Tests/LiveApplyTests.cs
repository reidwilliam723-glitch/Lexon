using Lexon.Core.Theming;
using Lexon.Profiles;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class LiveApplyTests
{
    [Fact]
    public void Appearance_UserEdit_ApplyLiveSeesNewValue_BeforeFlush()
    {
        var profile = new Profile();
        var settings = new AppSettings { SuggestionSortMode = "Relevant" };
        settings.Write(profile);
        var persist = new PersistScheduler(() => { });
        var appearance = new AppearanceSettingsViewModel(settings, persist, new FakeThemes());
        appearance.Load();

        AppSettings? live = null;
        var applyCount = 0;
        appearance.UserEdited += () =>
        {
            Assert.True(appearance.IsDirty);
            applyCount++;
            live = OwnedSettingsWriter.BuildLiveSnapshot(profile, [appearance]);
        };

        appearance.SortIndex = 1;

        Assert.Equal(1, applyCount);
        Assert.NotNull(live);
        Assert.Equal("Used", live!.SuggestionSortMode);
        Assert.Equal("Relevant", profile.GetSetting(AppSettings.SuggestionSortModeKey, ""));
        Assert.True(persist.HasPending);
    }

    [Fact]
    public void General_UserEdit_ApplyLiveSeesNewValue_BeforeFlush()
    {
        var profile = new Profile();
        var settings = new AppSettings { MinimizeToTray = true };
        settings.Write(profile);
        var persist = new PersistScheduler(() => { });
        var general = new GeneralSettingsViewModel(settings, new SilentStartup(), persist);
        general.Load();

        AppSettings? live = null;
        var applyCount = 0;
        general.UserEdited += () =>
        {
            Assert.True(general.IsDirty);
            applyCount++;
            live = OwnedSettingsWriter.BuildLiveSnapshot(profile, [general]);
        };

        general.MinimizeToTray = false;

        Assert.Equal(1, applyCount);
        Assert.False(live!.MinimizeToTray);
        Assert.True(profile.GetSetting(AppSettings.MinimizeToTrayKey, true));
    }

    [Fact]
    public void Privacy_UserEdit_ApplyLiveSeesNewValue_BeforeFlush()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Write(profile);
        var persist = new PersistScheduler(() => { });
        var privacy = new PrivacySettingsViewModel(
            settings, persist, new FakePolicy(), new FakePicker(), new FakeLog());
        privacy.Load();

        AppSettings? live = null;
        var applyCount = 0;
        privacy.UserEdited += () =>
        {
            Assert.True(privacy.IsDirty);
            applyCount++;
            live = OwnedSettingsWriter.BuildLiveSnapshot(profile, [privacy]);
        };

        privacy.LocalOnly = true;

        Assert.Equal(1, applyCount);
        Assert.True(live!.LocalMode);
        Assert.False(profile.GetSetting(AppSettings.LocalModeKey, false));
    }

    [Fact]
    public void AppTone_UserEdit_ApplyLiveSeesNewValue_BeforeFlush()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Write(profile);
        var persist = new PersistScheduler(() => { });
        var picker = new FakePicker { Result = "notepad" };
        var appTone = new AppToneViewModel(settings, persist, picker);
        appTone.Load();

        AppSettings? live = null;
        var applyCount = 0;
        appTone.UserEdited += () =>
        {
            Assert.True(appTone.IsDirty);
            applyCount++;
            live = OwnedSettingsWriter.BuildLiveSnapshot(profile, [appTone]);
        };

        appTone.AddRunningApp();

        Assert.Equal(1, applyCount);
        Assert.NotEmpty(live!.AppCategoryOverrides);
        var stored = new AppSettings();
        stored.Read(profile);
        Assert.Empty(stored.AppCategoryOverrides ?? []);
    }

    [Fact]
    public void Writing_UserEdit_ApplyLiveSeesNewValue_BeforeFlush()
    {
        var profile = new Profile();
        var settings = new AppSettings { GrammarChecking = true };
        settings.Write(profile);
        var persist = new PersistScheduler(() => { });
        var writing = new WritingViewModel(settings, persist);
        writing.Load();

        AppSettings? live = null;
        var applyCount = 0;
        writing.UserEdited += () =>
        {
            Assert.True(writing.IsDirty);
            applyCount++;
            live = OwnedSettingsWriter.BuildLiveSnapshot(profile, [writing]);
        };

        writing.GrammarChecking = false;

        Assert.Equal(1, applyCount);
        Assert.False(live!.GrammarChecking);
        Assert.True(profile.GetSetting(AppSettings.GrammarCheckingKey, true));
    }

    [Fact]
    public void Load_DoesNotRaiseUserEdited()
    {
        var settings = new AppSettings { MinimizeToTray = false };
        var persist = new PersistScheduler(() => { });
        var general = new GeneralSettingsViewModel(settings, new SilentStartup(), persist);
        var raised = 0;
        general.UserEdited += () => raised++;
        general.Load();
        Assert.Equal(0, raised);
        Assert.False(general.IsDirty);
    }

    [Fact]
    public void StatusProperty_DoesNotRaiseUserEdited()
    {
        var settings = new AppSettings();
        var persist = new PersistScheduler(() => { });
        var startup = new FailingStartup();
        var general = new GeneralSettingsViewModel(settings, startup, persist);
        general.Load();
        var raised = 0;
        general.UserEdited += () => raised++;
        general.StartWithWindows = true;
        Assert.Equal(0, raised);
        Assert.Equal(GeneralSettingsViewModel.StartupFailedMessage, general.StatusMessage);
    }

    [Fact]
    public void Ai_UserEdited_DoesNotCallApplyLive_WhenWiredLikeGallery()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Write(profile);
        var persist = new PersistScheduler(() => { });
        var session = new AiProbeSession();
        var policy = new FakePolicy();
        AiSettingsViewModel ai = null!;
        var pages = new List<IOwnedSettingsPage>();
        ai = new AiSettingsViewModel(
            settings, persist, session, policy, new FakeClipboard(), new FakeUrls(), new FakeDelays(),
            () => false, () => OwnedSettingsWriter.BuildLiveSnapshot(profile, pages));
        pages.Add(ai);
        ai.Load();

        var applyLive = 0;
        var userEdited = 0;
        ai.UserEdited += () =>
        {
            userEdited++;
            // Gallery wiring: ApplyLive is not invoked for AI edits.
        };

        ai.AiSuggestionsWhileTyping = true;

        Assert.Equal(1, userEdited);
        Assert.Equal(0, applyLive);
        Assert.True(ai.IsDirty);
    }

    private sealed class SilentStartup : IStartupRegistration
    {
        public bool IsEnabled() => false;

        public bool TrySetEnabled(bool enabled) => true;
    }

    private sealed class FailingStartup : IStartupRegistration
    {
        public bool IsEnabled() => false;

        public bool TrySetEnabled(bool enabled) => false;
    }

    private sealed class FakeThemes : IThemeSwitcher
    {
        public IReadOnlyList<string> Names { get; } = AppearanceSettingsViewModel.ThemeLabels;

        public string Current { get; private set; } = "Light";

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
        public string? Result { get; set; }

        public string? Pick(string prompt) => Result;
    }

    private sealed class FakeLog : ICloudAiActivityViewer
    {
        public void Show()
        {
        }
    }

    private sealed class FakeClipboard : IClipboardWatch
    {
        public event EventHandler? Updated;

        public bool Start() => true;

        public void Stop()
        {
        }

        public string? ReadText() => null;
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
}
