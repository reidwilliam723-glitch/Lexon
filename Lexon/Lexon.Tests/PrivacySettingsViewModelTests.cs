using Lexon.Profiles;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class PrivacySettingsViewModelTests
{
    [Fact]
    public void Load_CopiesValuesAndDoesNotPublish()
    {
        var settings = new AppSettings
        {
            LocalMode = true,
            BlockedApplications = ["outlook"]
        };
        var policy = new FakePolicy();
        var persist = CountingPersist(out var flushes);
        var vm = new PrivacySettingsViewModel(settings, persist, policy, new FakePicker(), new FakeLog());

        vm.Load();

        Assert.True(vm.LocalOnly);
        Assert.Equal("outlook", vm.BlockedAppsText);
        Assert.Empty(policy.Calls);
        Assert.False(persist.HasPending);
        Assert.Equal(0, flushes.Value);
    }

    [Fact]
    public void LocalOnlyOn_PublishesStoredAiFlagsAndSavesLocalMode()
    {
        var settings = new AppSettings
        {
            AiSuggestionsWhileTyping = true,
            AiRewriteOnRequest = false,
            AiPrefetchOnSelection = true
        };
        var policy = new FakePolicy();
        var persist = CountingPersist(out var flushes);
        var vm = new PrivacySettingsViewModel(settings, persist, policy, new FakePicker(), new FakeLog());
        vm.Load();

        vm.LocalOnly = true;

        Assert.Single(policy.Calls);
        Assert.Equal((true, true, false, true), policy.Calls[0]);
        Assert.True(settings.LocalMode);
        persist.Flush();
        Assert.Equal(1, flushes.Value);
    }

    [Fact]
    public void LocalOnlyOff_PublishesAgain()
    {
        var settings = new AppSettings { LocalMode = true };
        var policy = new FakePolicy();
        var vm = new PrivacySettingsViewModel(settings, CountingPersist(out _), policy, new FakePicker(), new FakeLog());
        vm.Load();
        vm.LocalOnly = false;
        Assert.Single(policy.Calls);
        Assert.Equal((false, false, true, false), policy.Calls[0]);
        Assert.False(settings.LocalMode);
    }

    [Fact]
    public void PrivacyFlush_DoesNotWriteAiFlags()
    {
        var profile = new Profile();
        var settings = new AppSettings
        {
            AiSuggestionsWhileTyping = true,
            AiRewriteOnRequest = false,
            AiPrefetchOnSelection = true,
            AIModel = "keep"
        };
        settings.Write(profile);
        settings.AiSuggestionsWhileTyping = false;
        settings.AiRewriteOnRequest = true;
        settings.AIModel = "stale";

        PrivacySettingsViewModel vm = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, vm));
        vm = new PrivacySettingsViewModel(settings, persist, new FakePolicy(), new FakePicker(), new FakeLog());
        vm.Load();
        vm.LocalOnly = true;
        persist.Flush();

        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.True(loaded.LocalMode);
        Assert.True(loaded.AiSuggestionsWhileTyping);
        Assert.False(loaded.AiRewriteOnRequest);
        Assert.True(loaded.AiPrefetchOnSelection);
        Assert.Equal("keep", loaded.AIModel);
    }

    [Fact]
    public void BlockedAppsText_ParsesOnChangeWithoutRewriting()
    {
        var settings = new AppSettings();
        var persist = CountingPersist(out _);
        var vm = new PrivacySettingsViewModel(settings, persist, new FakePolicy(), new FakePicker(), new FakeLog());
        vm.Load();

        const string typed = "Outlook.exe, outlook.exe, ";
        vm.BlockedAppsText = typed;

        Assert.Equal(typed, vm.BlockedAppsText);
        Assert.Equal(BlockedAppList.Parse(typed), settings.BlockedApplications);
    }

    [Fact]
    public void AddRunningApp_AddsOnceAndIgnoresDuplicateOrCancel()
    {
        var settings = new AppSettings();
        var picker = new FakePicker();
        var persist = CountingPersist(out var flushes);
        var vm = new PrivacySettingsViewModel(settings, persist, new FakePolicy(), picker, new FakeLog());
        vm.Load();

        picker.Result = null;
        vm.AddRunningApp();
        Assert.Equal(string.Empty, vm.BlockedAppsText);

        picker.Result = "Outlook.EXE";
        vm.AddRunningApp();
        var expected = BlockedAppList.FormatCsv(BlockedAppList.Parse("Outlook.EXE"));
        Assert.Equal(expected, vm.BlockedAppsText);

        picker.Result = "outlook.exe";
        vm.AddRunningApp();
        Assert.Equal(expected, vm.BlockedAppsText);

        persist.Flush();
        Assert.Equal(1, flushes.Value);
    }

    [Fact]
    public void RoundTrip_AndSecondLoad()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Read(profile);
        PrivacySettingsViewModel vm = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, vm));
        vm = new PrivacySettingsViewModel(settings, persist, new FakePolicy(), new FakePicker(), new FakeLog());
        vm.Load();
        vm.LocalOnly = true;
        vm.BlockedAppsText = "Chrome.exe, chrome";
        persist.Flush();

        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.True(loaded.LocalMode);
        Assert.Equal(BlockedAppList.Parse("Chrome.exe, chrome"), loaded.BlockedApplications);

        profile.SetSetting(AppSettings.LocalModeKey, false);
        profile.SetSetting(AppSettings.BlockedApplicationsKey, new List<string> { "slack" });
        settings.Read(profile);
        vm.Load();
        Assert.False(vm.LocalOnly);
        Assert.Equal("slack", vm.BlockedAppsText);
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

    private sealed class FakePolicy : IAiPolicyPublisher
    {
        public List<(bool Local, bool Typing, bool Rewrite, bool Prefetch)> Calls { get; } = [];

        public void Publish(bool localOnly, bool typing, bool rewrite, bool prefetch)
            => Calls.Add((localOnly, typing, rewrite, prefetch));
    }

    private sealed class FakePicker : IProcessPicker
    {
        public string? Result { get; set; }

        public string? Pick(string prompt) => Result;
    }

    private sealed class FakeLog : ICloudAiActivityViewer
    {
        public int Shows { get; private set; }

        public void Show() => Shows++;
    }
}
