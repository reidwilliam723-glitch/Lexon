using Lexon.Profiles;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class OwnedKeyWriteTests
{
    [Fact]
    public void FullWrite_OfStaleAppSettings_OverwritesUnownedKeys()
    {
        var profile = new Profile();
        var stale = new AppSettings
        {
            AIModel = "old",
            MinimizeToTray = true
        };
        stale.Write(profile);
        profile.SetSetting(AppSettings.AIModelKey, "gpt-4o");

        stale.MinimizeToTray = false;
        stale.Write(profile);

        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.Equal("old", loaded.AIModel);
        Assert.False(loaded.MinimizeToTray);
    }

    [Fact]
    public void OwnedKeyFlush_DoesNotOverwriteUnownedKeys()
    {
        var profile = new Profile();
        var settings = new AppSettings
        {
            AIModel = "old",
            MinimizeToTray = true
        };
        settings.Write(profile);
        profile.SetSetting(AppSettings.AIModelKey, "gpt-4o");
        settings.AIModel = "old";

        var persist = new PersistScheduler(() => { });
        var vm = new GeneralSettingsViewModel(settings, new SilentStartup(), persist);
        vm.Load();
        vm.MinimizeToTray = false;
        OwnedSettingsWriter.Flush(profile, vm);

        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.Equal("gpt-4o", loaded.AIModel);
        Assert.False(loaded.MinimizeToTray);
        Assert.True(loaded.EnableAutoUpdates);
    }

    [Fact]
    public void WriteKeys_WritesOnlyNamedKeys()
    {
        var profile = new Profile();
        profile.SetSetting(AppSettings.AIModelKey, "keep-me");
        var settings = new AppSettings { MinimizeToTray = false, AIModel = "stale" };
        settings.WriteKeys(profile, AppSettings.MinimizeToTrayKey);

        Assert.False(profile.GetSetting(AppSettings.MinimizeToTrayKey, true));
        Assert.Equal("keep-me", profile.GetSetting(AppSettings.AIModelKey, ""));
    }

    private sealed class SilentStartup : IStartupRegistration
    {
        public bool IsEnabled() => false;

        public bool TrySetEnabled(bool enabled) => true;
    }
}
