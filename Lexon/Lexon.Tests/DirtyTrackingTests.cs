using Lexon.Profiles;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class DirtyTrackingTests
{
    [Fact]
    public void Flush_AfterClassicPrivacyChange_DoesNotOverwriteStaleGalleryPrivacy()
    {
        // Reproduction of the 1.4.0 bug: gallery had Privacy loaded (clean but
        // stale), classic form wrote LocalMode + blocked apps, then a General
        // edit flushed every page and restored LocalMode=false.
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Write(profile);

        var persist = new PersistScheduler(() => { });
        var general = new GeneralSettingsViewModel(settings, new SilentStartup(), persist);
        var privacy = new PrivacySettingsViewModel(
            settings, persist, new FakePolicy(), new FakePicker(), new FakeLog());
        general.Load();
        privacy.Load();
        Assert.False(privacy.LocalOnly);
        Assert.False(privacy.IsDirty);

        profile.SetSetting(AppSettings.LocalModeKey, true);
        profile.SetSetting(AppSettings.BlockedApplicationsKey, new List<string> { "putty", "mstsc" });
        // Gallery AppSettings still has stale LocalMode=false; Privacy VM too.

        general.MinimizeToTray = false;
        Assert.True(general.IsDirty);
        Assert.False(privacy.IsDirty);
        OwnedSettingsWriter.Flush(profile, general, privacy);

        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.True(loaded.LocalMode);
        Assert.Equal(["putty", "mstsc"], loaded.BlockedApplications);
        Assert.False(loaded.MinimizeToTray);
        Assert.False(general.IsDirty);
    }

    [Fact]
    public void Flush_WritesOnlyDirtyPageKeys()
    {
        var profile = new SpyingProfile();
        var settings = new AppSettings { MinimizeToTray = true, LocalMode = false };
        settings.Write(profile);
        profile.Written.Clear();

        var persist = new PersistScheduler(() => { });
        var general = new GeneralSettingsViewModel(settings, new SilentStartup(), persist);
        var privacy = new PrivacySettingsViewModel(
            settings, persist, new FakePolicy(), new FakePicker(), new FakeLog());
        general.Load();
        privacy.Load();

        general.CheckForUpdates = false;
        OwnedSettingsWriter.Flush(profile, general, privacy);

        Assert.Equal(
            GeneralSettingsViewModel.OwnedKeyList.OrderBy(k => k, StringComparer.Ordinal).ToArray(),
            profile.Written.Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(AppSettings.LocalModeKey, profile.Written);
        Assert.DoesNotContain(AppSettings.BlockedApplicationsKey, profile.Written);
    }

    [Fact]
    public void LoadAndFlush_WithoutEdit_WritesNothing_PreservesSystemTheme()
    {
        var profile = new SpyingProfile();
        profile.SetSetting(AppSettings.ThemeKey, "System");
        var settings = new AppSettings();
        settings.Read(profile);
        Assert.Equal("System", settings.Theme);
        profile.Written.Clear();

        var persist = new PersistScheduler(() => { });
        var appearance = new AppearanceSettingsViewModel(settings, persist, new FakeThemes());
        appearance.Load();
        Assert.False(appearance.IsDirty);
        Assert.Equal(0, appearance.ThemeIndex); // UI maps unknown to Light

        OwnedSettingsWriter.Flush(profile, appearance);

        Assert.Empty(profile.Written);
        Assert.Equal("System", profile.GetSetting(AppSettings.ThemeKey, ""));
    }

    [Fact]
    public void ActivateReload_PicksUpChangedProfile_ForCleanPages()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Write(profile);

        PrivacySettingsViewModel privacy = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, privacy));
        privacy = new PrivacySettingsViewModel(
            settings, persist, new FakePolicy(), new FakePicker(), new FakeLog(),
            () => settings.Read(profile));
        privacy.Load();
        Assert.False(privacy.LocalOnly);

        profile.SetSetting(AppSettings.LocalModeKey, true);
        profile.SetSetting(AppSettings.BlockedApplicationsKey, new List<string> { "slack" });
        Assert.False(privacy.IsDirty);
        settings.Read(profile);
        privacy.Load();

        Assert.True(privacy.LocalOnly);
        Assert.Equal("slack", privacy.BlockedAppsText);
        Assert.False(privacy.IsDirty);
    }

    [Fact]
    public void DeactivateFlush_WritesPendingDirtyEdit()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Write(profile);
        GeneralSettingsViewModel general = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, general));
        general = new GeneralSettingsViewModel(settings, new SilentStartup(), persist);
        general.Load();
        general.MinimizeToTray = false;
        Assert.True(general.IsDirty);
        Assert.True(persist.HasPending);

        persist.Flush(); // deactivate path

        Assert.False(general.IsDirty);
        Assert.False(persist.HasPending);
        Assert.False(profile.GetSetting(AppSettings.MinimizeToTrayKey, true));
    }

    [Fact]
    public void BlockedApps_ActivateReload_KeepsTextWhenParsedListUnchanged()
    {
        var settings = new AppSettings { BlockedApplications = ["Outlook"] };
        var persist = new PersistScheduler(() => { });
        var privacy = new PrivacySettingsViewModel(
            settings, persist, new FakePolicy(), new FakePicker(), new FakeLog());
        privacy.Load();
        Assert.Equal("Outlook", privacy.BlockedAppsText);

        const string odd = "Outlook.exe,";
        privacy.BlockedAppsText = odd;
        Assert.True(privacy.IsDirty);
        // Simulate a successful flush that wrote the parsed list, then activate.
        settings.BlockedApplications = BlockedAppList.Parse(odd);
        privacy.MarkClean();
        privacy.Load();

        Assert.Equal(odd, privacy.BlockedAppsText);
    }

    [Fact]
    public void LiveSnapshot_IncludesDirtyValues_BeforeProfileWrite()
    {
        var profile = new Profile();
        var settings = new AppSettings { SuggestionSortMode = "Relevant" };
        settings.Write(profile);

        var persist = new PersistScheduler(() => { });
        var appearance = new AppearanceSettingsViewModel(settings, persist, new FakeThemes());
        appearance.Load();
        appearance.SortIndex = 1;
        Assert.True(appearance.IsDirty);
        Assert.Equal("Relevant", profile.GetSetting(AppSettings.SuggestionSortModeKey, ""));

        var snapshot = OwnedSettingsWriter.BuildLiveSnapshot(profile, [appearance]);
        Assert.Equal("Used", snapshot.SuggestionSortMode);

        // Profile still unchanged until Flush.
        Assert.Equal("Relevant", profile.GetSetting(AppSettings.SuggestionSortModeKey, ""));
        OwnedSettingsWriter.Flush(profile, appearance);
        Assert.Equal("Used", profile.GetSetting(AppSettings.SuggestionSortModeKey, ""));
    }

    [Fact]
    public void GalleryThemeButtons_MarkDirtyAndPersistThemeKey_ThemeConfigIsAuthoritativeAtStartup()
    {
        // ThemeManager persists theme_config on Apply; that is what startup reads.
        // SelectThemeByName also dirties so the profile Theme key stays aligned
        // with the classic combo.
        var profile = new Profile();
        profile.SetSetting(AppSettings.ThemeKey, "Light");
        var settings = new AppSettings();
        settings.Read(profile);
        var themes = new FakeThemes();
        var persist = new PersistScheduler(() => { });
        var appearance = new AppearanceSettingsViewModel(settings, persist, themes);
        appearance.Load();

        appearance.SelectThemeByName("Dark");

        Assert.True(appearance.IsDirty);
        Assert.Equal(["Dark"], themes.Applied);
        Assert.Equal("Dark", settings.Theme);
        OwnedSettingsWriter.Flush(profile, appearance);
        Assert.Equal("Dark", profile.GetSetting(AppSettings.ThemeKey, ""));
        Assert.False(appearance.IsDirty);
    }

    private sealed class SpyingProfile : Profile
    {
        public List<string> Written { get; } = [];

        public override void SetSetting<T>(string key, T value)
        {
            Written.Add(key);
            base.SetSetting(key, value);
        }
    }

    private sealed class SilentStartup : IStartupRegistration
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

    private sealed class FakeThemes : IThemeSwitcher
    {
        public List<string> Applied { get; } = [];

        public IReadOnlyList<string> Names { get; } = AppearanceSettingsViewModel.ThemeLabels;

        public string Current { get; set; } = "Light";

        public event EventHandler<Core.Theming.ThemeChangedEventArgs>? ThemeChanged;

        public void Apply(string name)
        {
            Applied.Add(name);
            Current = name;
            ThemeChanged?.Invoke(this, new Core.Theming.ThemeChangedEventArgs
            {
                NewTheme = new Core.Theming.Theme { Name = name }
            });
        }
    }
}
