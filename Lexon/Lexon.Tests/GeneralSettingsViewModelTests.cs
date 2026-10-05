using Lexon.Profiles;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class GeneralSettingsViewModelTests
{
    [Fact]
    public void Load_CopiesValuesAndHasNoSideEffects()
    {
        var settings = new AppSettings
        {
            MinimizeToTray = false,
            EnableAutoUpdates = false,
            OpenSettingsFullScreen = true,
            QuickPauseMinutes = 30
        };
        var startup = new FakeStartup { Enabled = true };
        var persist = CountingPersist(out var flushes);
        var fullScreen = 0;
        var vm = new GeneralSettingsViewModel(settings, startup, persist);
        vm.OpenFullScreenChanged += _ => fullScreen++;

        vm.Load();

        Assert.True(vm.StartWithWindows);
        Assert.False(vm.MinimizeToTray);
        Assert.False(vm.CheckForUpdates);
        Assert.True(vm.OpenSettingsFullScreen);
        Assert.Equal(4, vm.QuickPauseIndex);
        Assert.Equal(string.Empty, vm.StatusMessage);
        Assert.Equal(1, startup.GetCalls);
        Assert.Equal(0, startup.SetCalls);
        Assert.False(persist.HasPending);
        Assert.Equal(0, flushes.Value);
        Assert.Equal(0, fullScreen);
    }

    [Fact]
    public void Load_CopiesDefaults()
    {
        var settings = new AppSettings();
        var startup = new FakeStartup();
        var persist = CountingPersist(out _);
        var vm = new GeneralSettingsViewModel(settings, startup, persist);

        vm.Load();

        Assert.False(vm.StartWithWindows);
        Assert.True(vm.MinimizeToTray);
        Assert.True(vm.CheckForUpdates);
        Assert.False(vm.OpenSettingsFullScreen);
        Assert.Equal(3, vm.QuickPauseIndex);
    }

    [Theory]
    [InlineData(nameof(GeneralSettingsViewModel.MinimizeToTray))]
    [InlineData(nameof(GeneralSettingsViewModel.CheckForUpdates))]
    [InlineData(nameof(GeneralSettingsViewModel.OpenSettingsFullScreen))]
    [InlineData(nameof(GeneralSettingsViewModel.QuickPauseIndex))]
    public void ChangingProperty_UpdatesAppSettingsAndSchedulesOnce(string property)
    {
        var settings = new AppSettings();
        var persist = CountingPersist(out var flushes);
        var vm = new GeneralSettingsViewModel(settings, new FakeStartup(), persist);
        vm.Load();

        switch (property)
        {
            case nameof(GeneralSettingsViewModel.MinimizeToTray):
                vm.MinimizeToTray = false;
                Assert.False(settings.MinimizeToTray);
                break;
            case nameof(GeneralSettingsViewModel.CheckForUpdates):
                vm.CheckForUpdates = false;
                Assert.False(settings.EnableAutoUpdates);
                break;
            case nameof(GeneralSettingsViewModel.OpenSettingsFullScreen):
                vm.OpenSettingsFullScreen = true;
                Assert.True(settings.OpenSettingsFullScreen);
                break;
            case nameof(GeneralSettingsViewModel.QuickPauseIndex):
                vm.QuickPauseIndex = 5;
                Assert.Equal(60, settings.QuickPauseMinutes);
                break;
        }

        Assert.True(persist.HasPending);
        persist.Flush();
        Assert.Equal(1, flushes.Value);
        Assert.False(persist.HasPending);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 30)]
    [InlineData(5, 60)]
    [InlineData(-1, 15)]
    [InlineData(99, 15)]
    public void QuickPauseIndex_MapsMinutes(int index, int minutes)
    {
        var settings = new AppSettings { QuickPauseMinutes = 30 };
        var persist = CountingPersist(out _);
        var vm = new GeneralSettingsViewModel(settings, new FakeStartup(), persist);
        vm.Load();
        vm.QuickPauseIndex = index;

        Assert.Equal(QuickPauseOptions.IndexFromMinutes(minutes), vm.QuickPauseIndex);
        Assert.Equal(minutes, settings.QuickPauseMinutes);
    }

    [Fact]
    public void StartWithWindows_CallsRegistrationAndSchedulesOnSuccess()
    {
        var settings = new AppSettings();
        var startup = new FakeStartup();
        var persist = CountingPersist(out var flushes);
        var vm = new GeneralSettingsViewModel(settings, startup, persist);
        vm.Load();

        vm.StartWithWindows = true;

        Assert.True(vm.StartWithWindows);
        Assert.True(startup.Enabled);
        Assert.Equal(1, startup.SetCalls);
        Assert.True(persist.HasPending);
        persist.Flush();
        Assert.Equal(1, flushes.Value);
    }

    [Fact]
    public void StartWithWindows_RevertsAndDoesNotPersistWhenRegistrationFails()
    {
        var settings = new AppSettings();
        var startup = new FakeStartup { Fail = true };
        var persist = CountingPersist(out var flushes);
        var vm = new GeneralSettingsViewModel(settings, startup, persist);
        vm.Load();

        vm.StartWithWindows = true;

        Assert.False(vm.StartWithWindows);
        Assert.Equal(1, startup.SetCalls);
        Assert.Equal(GeneralSettingsViewModel.StartupFailedMessage, vm.StatusMessage);
        Assert.False(persist.HasPending);
        Assert.Equal(0, flushes.Value);
    }

    [Fact]
    public void OpenSettingsFullScreen_RaisesEventOnUserChangeOnly()
    {
        var settings = new AppSettings { OpenSettingsFullScreen = true };
        var persist = CountingPersist(out _);
        var seen = new List<bool>();
        var vm = new GeneralSettingsViewModel(settings, new FakeStartup(), persist);
        vm.OpenFullScreenChanged += seen.Add;

        vm.Load();
        Assert.Empty(seen);

        vm.OpenSettingsFullScreen = false;
        vm.OpenSettingsFullScreen = true;
        Assert.Equal([false, true], seen);
    }

    [Fact]
    public void RoundTrip_FlushThenFreshAppSettingsMatch()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Read(profile);
        GeneralSettingsViewModel vm = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, vm));
        vm = new GeneralSettingsViewModel(settings, new FakeStartup(), persist);
        vm.Load();

        vm.MinimizeToTray = false;
        vm.CheckForUpdates = false;
        vm.OpenSettingsFullScreen = true;
        vm.QuickPauseIndex = 5;
        persist.Flush();

        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.False(loaded.MinimizeToTray);
        Assert.False(loaded.EnableAutoUpdates);
        Assert.True(loaded.OpenSettingsFullScreen);
        Assert.Equal(60, loaded.QuickPauseMinutes);
        Assert.True(profile.HasSetting(AppSettings.MinimizeToTrayKey));
        Assert.True(profile.HasSetting(AppSettings.EnableAutoUpdatesKey));
        Assert.True(profile.HasSetting(AppSettings.OpenSettingsFullScreenKey));
        Assert.True(profile.HasSetting(AppSettings.QuickPauseMinutesKey));
    }

    [Fact]
    public void Load_SecondTimePicksUpProfileChanges()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Read(profile);
        var persist = new PersistScheduler(() => settings.Write(profile));
        var vm = new GeneralSettingsViewModel(settings, new FakeStartup(), persist);
        vm.Load();
        Assert.True(vm.MinimizeToTray);
        Assert.Equal(3, vm.QuickPauseIndex);

        profile.SetSetting(AppSettings.MinimizeToTrayKey, false);
        profile.SetSetting(AppSettings.QuickPauseMinutesKey, 5);
        profile.SetSetting(AppSettings.EnableAutoUpdatesKey, false);
        profile.SetSetting(AppSettings.OpenSettingsFullScreenKey, true);
        settings.Read(profile);
        vm.Load();

        Assert.False(vm.MinimizeToTray);
        Assert.False(vm.CheckForUpdates);
        Assert.True(vm.OpenSettingsFullScreen);
        Assert.Equal(2, vm.QuickPauseIndex);
        Assert.Equal(string.Empty, vm.StatusMessage);
        Assert.False(persist.HasPending);
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

    private sealed class FakeStartup : IStartupRegistration
    {
        public bool Enabled;
        public bool Fail;
        public int GetCalls;
        public int SetCalls;

        public bool IsEnabled()
        {
            GetCalls++;
            return Enabled;
        }

        public bool TrySetEnabled(bool enabled)
        {
            SetCalls++;
            if (Fail)
            {
                return false;
            }

            Enabled = enabled;
            return true;
        }
    }
}
