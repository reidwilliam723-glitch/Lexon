using Lexon.Core;
using Lexon.Settings;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class AppPackagingTests : IDisposable
{
    public AppPackagingTests() => AppPackaging.SetOverride(null);

    public void Dispose() => AppPackaging.SetOverride(null);

    [Fact]
    public void IsPackaged_OverrideTrueAndFalse()
    {
        AppPackaging.SetOverride(true);
        Assert.True(AppPackaging.IsPackaged);

        AppPackaging.SetOverride(false);
        Assert.False(AppPackaging.IsPackaged);
    }

    [Fact]
    public void StoreIdentity_StoreModeRejectsPlaceholderPublisher()
    {
        Assert.Throws<InvalidOperationException>(
            () => StorePackageIdentity.EnsureReadyForStore("CN=WGR", storeMode: true));
        Assert.Throws<InvalidOperationException>(
            () => StorePackageIdentity.EnsureReadyForStore("  CN=WGR  ", storeMode: true));
        Assert.Throws<InvalidOperationException>(
            () => StorePackageIdentity.EnsureReadyForStore(null, storeMode: true));
    }

    [Fact]
    public void StoreIdentity_StoreModeAcceptsRealPublisher_AndNonStoreAllowsPlaceholder()
    {
        StorePackageIdentity.EnsureReadyForStore("CN=ABCDEF12-3456-7890-ABCD-EF1234567890", storeMode: true);
        StorePackageIdentity.EnsureReadyForStore("CN=WGR", storeMode: false);
    }

    [Fact]
    public async Task UpdateManager_Packaged_NeverReportsAnUpdate()
    {
        AppPackaging.SetOverride(true);
        var manager = new UpdateManager(new HttpClient(), "1.0.0", "https://example.test/updates");
        manager.Initialize(enabled: true, "https://example.test/updates");

        var result = await manager.CheckForUpdatesAsync(forceCheck: true);
        Assert.True(result.Success);
        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public void GeneralSettings_Packaged_HidesUpdateControls()
    {
        AppPackaging.SetOverride(true);
        var vm = new GeneralSettingsViewModel(new AppSettings(), new FakeStartup(), new PersistScheduler(() => { }));
        Assert.False(vm.ShowUpdateSettings);

        AppPackaging.SetOverride(false);
        Assert.True(vm.ShowUpdateSettings);
    }

    [Fact]
    public void StartWithWindows_PackagedFake_UsesStartupTaskMessages()
    {
        AppPackaging.SetOverride(true);
        var startup = new FakeStartup
        {
            Fail = true,
            StatusHint = PackagedStartupRegistration.DisabledByUserMessage,
            Availability = StartupAvailability.DisabledByUser
        };
        var vm = new GeneralSettingsViewModel(new AppSettings(), startup, new PersistScheduler(() => { }));
        vm.Load();
        Assert.Equal(PackagedStartupRegistration.DisabledByUserMessage, vm.StatusMessage);

        vm.StartWithWindows = true;
        Assert.False(vm.StartWithWindows);
        Assert.Equal(PackagedStartupRegistration.DisabledByUserMessage, vm.StatusMessage);
    }

    [Fact]
    public void StartWithWindows_UnpackagedFake_UsesRunKeySuccessPath()
    {
        AppPackaging.SetOverride(false);
        var startup = new FakeStartup();
        var persist = new PersistScheduler(() => { });
        var vm = new GeneralSettingsViewModel(new AppSettings(), startup, persist);
        vm.Load();
        vm.StartWithWindows = true;
        Assert.True(vm.StartWithWindows);
        Assert.True(startup.Enabled);
        Assert.Equal(string.Empty, vm.StatusMessage);
    }

    private sealed class FakeStartup : IStartupRegistration
    {
        public bool Enabled;
        public bool Fail;
        public StartupAvailability Availability { get; set; } = StartupAvailability.Available;
        public string? StatusHint { get; set; }

        public bool IsEnabled() => Enabled;

        public bool TrySetEnabled(bool enabled)
        {
            if (Fail)
            {
                return false;
            }

            Enabled = enabled;
            return true;
        }
    }
}
