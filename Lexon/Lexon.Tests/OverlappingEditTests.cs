using Lexon.Profiles;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class OverlappingEditTests
{
    [Fact]
    public void PrivacyThenApps_FlushKeepsBothBlockedApps_AndClearsTheEarlierDirtyFlag()
    {
        var harness = Harness.Create();
        harness.Privacy.BlockedAppsText = "outlook";
        Assert.True(harness.Privacy.IsDirty);

        harness.Apps.SelectApp("notepad");
        harness.Apps.BlockAssistance = true;

        Assert.False(harness.Privacy.IsDirty);
        harness.Persist.Flush();

        var loaded = new AppSettings();
        loaded.Read(harness.Profile);
        Assert.Contains("outlook", loaded.BlockedApplications, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("notepad", loaded.BlockedApplications, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppsThenPrivacy_FlushKeepsBothBlockedApps_AndClearsTheEarlierDirtyFlag()
    {
        var harness = Harness.Create();
        harness.Apps.SelectApp("notepad");
        harness.Apps.BlockAssistance = true;
        Assert.True(harness.Apps.IsDirty);

        harness.Privacy.BlockedAppsText = harness.Privacy.BlockedAppsText + ", outlook";

        Assert.False(harness.Apps.IsDirty);
        harness.Persist.Flush();

        var loaded = new AppSettings();
        loaded.Read(harness.Profile);
        Assert.Contains("outlook", loaded.BlockedApplications, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("notepad", loaded.BlockedApplications, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToneEdits_InEitherOrder_BothSurvive()
    {
        var first = Harness.Create(pick: "slack");
        first.Tone.SelectedToneIndex = 0;
        first.Tone.AddRunningApp();
        first.Apps.SelectApp("notepad");
        first.Apps.ToneIndex = 2;
        Assert.False(first.Tone.IsDirty);
        first.Persist.Flush();
        var loaded = new AppSettings();
        loaded.Read(first.Profile);
        Assert.Contains(loaded.AppCategoryOverrides, row => row.Contains("slack", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(loaded.AppCategoryOverrides, row => row.Contains("notepad", StringComparison.OrdinalIgnoreCase));

        var second = Harness.Create(pick: "slack");
        second.Apps.SelectApp("notepad");
        second.Apps.ToneIndex = 2;
        Assert.True(second.Apps.IsDirty);
        second.Tone.SelectedToneIndex = 0;
        second.Tone.AddRunningApp();
        Assert.False(second.Apps.IsDirty);
        second.Persist.Flush();
        loaded = new AppSettings();
        loaded.Read(second.Profile);
        Assert.Contains(loaded.AppCategoryOverrides, row => row.Contains("slack", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(loaded.AppCategoryOverrides, row => row.Contains("notepad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GrammarAndLearnedMutes_InEitherOrder_BothSurvive()
    {
        var first = Harness.Create();
        first.Writing.MutedAppsText = "slack.exe";
        first.Apps.SelectApp("notepad");
        first.Apps.Grammar = false;
        Assert.False(first.Writing.IsDirty);
        first.Persist.Flush();
        var loaded = new AppSettings();
        loaded.Read(first.Profile);
        Assert.Contains(loaded.GrammarMutedApps, name => name.Contains("slack", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(loaded.GrammarMutedApps, name => name.Contains("notepad", StringComparison.OrdinalIgnoreCase));

        var second = Harness.Create();
        second.Apps.SelectApp("notepad");
        second.Apps.LearnedWords = false;
        Assert.True(second.Apps.IsDirty);
        second.Writing.LearnedMutedAppsText = second.Writing.LearnedMutedAppsText + ", slack";
        Assert.False(second.Apps.IsDirty);
        second.Persist.Flush();
        loaded = new AppSettings();
        loaded.Read(second.Profile);
        Assert.Contains(loaded.LearnedWordsMutedApps, name => name.Contains("notepad", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(loaded.LearnedWordsMutedApps, name => name.Contains("slack", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NonOverlappingDirtyPage_IsNotFlushedByAnOverlappingEdit()
    {
        var harness = Harness.Create();
        harness.General.MinimizeToTray = false;
        Assert.True(harness.General.IsDirty);

        harness.Privacy.BlockedAppsText = "outlook";
        harness.Apps.SelectApp("notepad");
        harness.Apps.BlockAssistance = true;

        Assert.True(harness.General.IsDirty);
        Assert.True(harness.Profile.GetSetting(AppSettings.MinimizeToTrayKey, false));
    }

    private sealed class Harness
    {
        public Profile Profile { get; }
        public PersistScheduler Persist { get; }
        public PrivacySettingsViewModel Privacy { get; }
        public AppToneViewModel Tone { get; }
        public WritingViewModel Writing { get; }
        public AppsSettingsViewModel Apps { get; }
        public GeneralSettingsViewModel General { get; }

        private Harness(
            Profile profile,
            PersistScheduler persist,
            PrivacySettingsViewModel privacy,
            AppToneViewModel tone,
            WritingViewModel writing,
            AppsSettingsViewModel apps,
            GeneralSettingsViewModel general)
        {
            Profile = profile;
            Persist = persist;
            Privacy = privacy;
            Tone = tone;
            Writing = writing;
            Apps = apps;
            General = general;
        }

        public static Harness Create(string pick = "unused")
        {
            var profile = new Profile();
            var settings = new AppSettings();
            settings.Write(profile);
            PrivacySettingsViewModel privacy = null!;
            AppToneViewModel tone = null!;
            WritingViewModel writing = null!;
            AppsSettingsViewModel apps = null!;
            GeneralSettingsViewModel general = null!;
            var persist = new PersistScheduler(() =>
                OwnedSettingsWriter.Flush(profile, general, privacy, tone, writing, apps));
            privacy = new PrivacySettingsViewModel(settings, persist, new Policy(), new Picker(pick), new Log());
            tone = new AppToneViewModel(settings, persist, new Picker(pick));
            writing = new WritingViewModel(settings, persist);
            apps = new AppsSettingsViewModel(settings, persist, new Picker(pick));
            general = new GeneralSettingsViewModel(settings, new Startup(), persist);
            privacy.Load();
            tone.Load();
            writing.Load();
            apps.Attach(privacy, tone, writing);
            apps.Load();
            general.Load();
            var guard = new OverlappingEditGuard(
                [privacy, tone, writing, apps],
                earlier =>
                {
                    OwnedSettingsWriter.Flush(profile, earlier.ToArray());
                    settings.Read(profile);
                },
                page =>
                {
                    settings.Read(profile);
                    switch (page)
                    {
                        case PrivacySettingsViewModel privacyPage:
                            privacyPage.Load();
                            break;
                        case AppToneViewModel tonePage:
                            tonePage.Load();
                            break;
                        case WritingViewModel writingPage:
                            writingPage.Load();
                            break;
                        case AppsSettingsViewModel appsPage:
                            appsPage.Load();
                            break;
                    }
                });
            privacy.UseOverlapGuard(guard);
            tone.UseOverlapGuard(guard);
            writing.UseOverlapGuard(guard);
            apps.UseOverlapGuard(guard);
            return new Harness(profile, persist, privacy, tone, writing, apps, general);
        }

        private sealed class Policy : IAiPolicyPublisher
        {
            public void Publish(bool localOnly, bool typing, bool rewrite, bool prefetch)
            {
            }
        }

        private sealed class Picker(string name) : IProcessPicker
        {
            public string? Pick(string title) => name;
        }

        private sealed class Log : ICloudAiActivityViewer
        {
            public void Show()
            {
            }
        }

        private sealed class Startup : IStartupRegistration
        {
            public bool IsEnabled() => false;

            public bool TrySetEnabled(bool enabled) => true;

        public StartupAvailability Availability => StartupAvailability.Available;

        public string? StatusHint => null;
        }
    }
}
