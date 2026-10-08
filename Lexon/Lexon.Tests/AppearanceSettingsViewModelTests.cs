using Lexon.Core.Theming;
using Lexon.Profiles;
using Lexon.SettingsModel;
using Lexon.Storage;
using Xunit;

namespace Lexon.Tests;

public class AppearanceSettingsViewModelTests
{
    [Fact]
    public void Load_MapsKnownValues()
    {
        var settings = new AppSettings
        {
            Theme = "Dark",
            SuggestionSortMode = "Used",
            SuggestionPlacement = "Above",
            SuggestionAcceptKey = "Right",
            RequireConfirmationForEdits = false
        };
        var themes = new FakeThemes();
        var persist = CountingPersist(out var flushes);
        var vm = new AppearanceSettingsViewModel(settings, persist, themes);

        vm.Load();

        Assert.Equal(1, vm.ThemeIndex);
        Assert.Equal(1, vm.SortIndex);
        Assert.Equal(1, vm.PlacementIndex);
        Assert.Equal(2, vm.AcceptKeyIndex);
        Assert.False(vm.PreviewRewrites);
        Assert.Equal(0, themes.ApplyCalls);
        Assert.False(persist.HasPending);
        Assert.Equal(0, flushes.Value);
    }

    [Theory]
    [InlineData("Nope", 0)]
    [InlineData("", 0)]
    [InlineData("High Contrast", 2)]
    [InlineData("light", 0)]
    public void Load_ThemeFallback(string stored, int index)
    {
        var settings = new AppSettings { Theme = stored };
        var vm = new AppearanceSettingsViewModel(settings, CountingPersist(out _), new FakeThemes());
        vm.Load();
        Assert.Equal(index, vm.ThemeIndex);
    }

    [Theory]
    [InlineData("Used", 1)]
    [InlineData("used", 1)]
    [InlineData("Relevant", 0)]
    [InlineData("Other", 0)]
    public void Load_SortFallback(string stored, int index)
    {
        var settings = new AppSettings { SuggestionSortMode = stored };
        var vm = new AppearanceSettingsViewModel(settings, CountingPersist(out _), new FakeThemes());
        vm.Load();
        Assert.Equal(index, vm.SortIndex);
    }

    [Theory]
    [InlineData("Above", 1)]
    [InlineData("above", 1)]
    [InlineData("Below", 0)]
    [InlineData("Side", 0)]
    public void Load_PlacementFallback(string stored, int index)
    {
        var settings = new AppSettings { SuggestionPlacement = stored };
        var vm = new AppearanceSettingsViewModel(settings, CountingPersist(out _), new FakeThemes());
        vm.Load();
        Assert.Equal(index, vm.PlacementIndex);
    }

    [Fact]
    public void ChangingTheme_AppliesOnceAndSchedulesPersist()
    {
        var settings = new AppSettings();
        var themes = new FakeThemes();
        var persist = CountingPersist(out var flushes);
        var vm = new AppearanceSettingsViewModel(settings, persist, themes);
        vm.Load();

        vm.ThemeIndex = 1;

        Assert.Equal("Dark", settings.Theme);
        Assert.Equal(["Dark"], themes.Applied);
        persist.Flush();
        Assert.Equal(1, flushes.Value);
    }

    [Fact]
    public void ChangingOtherProperties_UpdatesAppSettingsAndSchedulesOnce()
    {
        var settings = new AppSettings();
        var persist = CountingPersist(out var flushes);
        var vm = new AppearanceSettingsViewModel(settings, persist, new FakeThemes());
        vm.Load();

        vm.SortIndex = 1;
        Assert.Equal("Used", settings.SuggestionSortMode);
        vm.PlacementIndex = 1;
        Assert.Equal("Above", settings.SuggestionPlacement);
        vm.AcceptKeyIndex = 3;
        Assert.Equal("Numbers", settings.SuggestionAcceptKey);
        vm.PreviewRewrites = false;
        Assert.False(settings.RequireConfirmationForEdits);
        persist.Flush();
        Assert.Equal(1, flushes.Value);
    }

    [Fact]
    public void ExternalThemeChange_UpdatesSelectionWithoutApplyOrPersist()
    {
        var settings = new AppSettings();
        var themes = new FakeThemes();
        var persist = CountingPersist(out var flushes);
        var vm = new AppearanceSettingsViewModel(settings, persist, themes);
        vm.Load();

        themes.Raise("High Contrast");

        Assert.Equal(2, vm.ThemeIndex);
        Assert.Equal("High Contrast", settings.Theme);
        Assert.Empty(themes.Applied);
        Assert.False(persist.HasPending);
        Assert.Equal(0, flushes.Value);
    }

    [Fact]
    public void RoundTrip_OwnedKeysOnly()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Read(profile);
        profile.SetSetting(AppSettings.AIModelKey, "keep");
        AppearanceSettingsViewModel vm = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, vm));
        vm = new AppearanceSettingsViewModel(settings, persist, new FakeThemes());
        vm.Load();
        vm.ThemeIndex = 1;
        vm.SortIndex = 1;
        vm.PlacementIndex = 1;
        vm.AcceptKeyIndex = 1;
        vm.PreviewRewrites = false;
        persist.Flush();

        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.Equal("Dark", loaded.Theme);
        Assert.Equal("Used", loaded.SuggestionSortMode);
        Assert.Equal("Above", loaded.SuggestionPlacement);
        Assert.Equal("Enter", loaded.SuggestionAcceptKey);
        Assert.False(loaded.RequireConfirmationForEdits);
        Assert.Equal("keep", loaded.AIModel);
    }

    [Fact]
    public void Load_SecondTimePicksUpProfileChanges()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Read(profile);
        var vm = new AppearanceSettingsViewModel(settings, CountingPersist(out _), new FakeThemes());
        vm.Load();
        Assert.Equal(0, vm.ThemeIndex);

        profile.SetSetting(AppSettings.ThemeKey, "Dark");
        profile.SetSetting(AppSettings.SuggestionSortModeKey, "Used");
        settings.Read(profile);
        vm.Load();
        Assert.Equal(1, vm.ThemeIndex);
        Assert.Equal(1, vm.SortIndex);
    }

    [Fact]
    public void ThemeManager_IncludesTheThreeSettingsThemes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lexon-theme-names-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var themes = new ThemeManager(new EncryptedStorage(dir));
            var names = themes.AvailableThemes.Select(theme => theme.Name).ToList();
            Assert.Contains("Light", names);
            Assert.Contains("Dark", names);
            Assert.Contains("High Contrast", names);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* temp */ }
        }
    }

    private static PersistScheduler CountingPersist(out Holder flushes)
    {
        var holder = new Holder();
        flushes = holder;
        return new PersistScheduler(() => holder.Value++);
    }

    private sealed class Holder
    {
        public int Value;
    }

    private sealed class FakeThemes : IThemeSwitcher
    {
        public List<string> Applied { get; } = [];

        public int ApplyCalls => Applied.Count;

        public IReadOnlyList<string> Names { get; } = AppearanceSettingsViewModel.ThemeLabels;

        public string Current { get; set; } = "Light";

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public void Apply(string name)
        {
            Applied.Add(name);
            Current = name;
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs { NewTheme = new Theme { Name = name } });
        }

        public void Raise(string name)
        {
            Current = name;
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs { NewTheme = new Theme { Name = name } });
        }
    }
}
