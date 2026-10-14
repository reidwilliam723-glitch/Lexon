using Lexon.Core.Grammar;
using Lexon.Core.Interfaces;
using Lexon.Onboarding.Models;
using Lexon.Profiles;
using Lexon.Settings;
using Xunit;

namespace Lexon.Tests;

public class QuietHoursAndTutorialTests
{
    private static readonly DateTime Night = new(2026, 10, 12, 22, 30, 0);
    private static readonly DateTime WindowEnd = new(2026, 10, 13, 7, 0, 0);
    private static readonly DateTime AfterWindow = new(2026, 10, 13, 7, 1, 0);
    private static readonly DateTime NextNight = new(2026, 10, 13, 22, 30, 0);

    [Fact]
    public void QuietStart_PausesLexon()
    {
        var quiet = new QuietHoursController();

        var action = quiet.Tick(Night, quiet: true, WindowEnd);

        Assert.Equal(QuietHoursAction.Pause, action);
        Assert.False(quiet.Enabled);
        Assert.True(quiet.PausedByQuietHours);
    }

    [Fact]
    public void TrayEnable_DuringQuietHours_StaysEnabled_OnNextTick()
    {
        var quiet = new QuietHoursController();
        quiet.Tick(Night, quiet: true, WindowEnd);

        quiet.UserEnabled(Night.AddMinutes(1), quiet: true, WindowEnd);
        quiet.Tick(Night.AddMinutes(2), quiet: true, WindowEnd);

        Assert.True(quiet.Enabled);
    }

    [Fact]
    public void HotkeyEnable_DuringQuietHours_StaysEnabled_OnNextTick()
    {
        var quiet = new QuietHoursController();
        quiet.Tick(Night, quiet: true, WindowEnd);

        quiet.UserEnabled(Night.AddMinutes(1), quiet: true, WindowEnd);
        quiet.Tick(Night.AddMinutes(2), quiet: true, WindowEnd);

        Assert.True(quiet.Enabled);
    }

    [Fact]
    public void EnableOverride_ExpiresAfterWindow_AndNextNightPausesAgain()
    {
        var quiet = new QuietHoursController();
        quiet.Tick(Night, quiet: true, WindowEnd);
        quiet.UserEnabled(Night.AddMinutes(1), quiet: true, WindowEnd);

        quiet.Tick(Night.AddMinutes(2), quiet: true, WindowEnd);
        Assert.True(quiet.Enabled);

        quiet.Tick(AfterWindow, quiet: false, WindowEnd);
        Assert.True(quiet.Enabled);
        Assert.False(quiet.SuppressesQuiet(NextNight));

        var next = quiet.Tick(NextNight, quiet: true, NextNight.Date.AddDays(1).AddHours(7));
        Assert.Equal(QuietHoursAction.Pause, next);
        Assert.False(quiet.Enabled);
    }

    [Fact]
    public void AutoResume_WhenUserDidNothing()
    {
        var quiet = new QuietHoursController();
        quiet.Tick(Night, quiet: true, WindowEnd);

        var action = quiet.Tick(AfterWindow, quiet: false, WindowEnd);

        Assert.Equal(QuietHoursAction.Resume, action);
        Assert.True(quiet.Enabled);
        Assert.False(quiet.PausedByQuietHours);
    }

    [Fact]
    public void PauseUntilResume_DuringQuietHours_StaysPaused_AfterWindowEnds()
    {
        var quiet = new QuietHoursController();
        quiet.Tick(Night, quiet: true, WindowEnd);

        quiet.ManualPause();
        var action = quiet.Tick(AfterWindow, quiet: false, WindowEnd);

        Assert.Equal(QuietHoursAction.None, action);
        Assert.False(quiet.Enabled);
    }

    [Fact]
    public void PauseFifteen_DuringQuietHours_IsNotResumedByQuietHours()
    {
        var quiet = new QuietHoursController();
        quiet.Tick(Night, quiet: true, WindowEnd);

        quiet.ManualPause();
        var action = quiet.Tick(AfterWindow, quiet: false, WindowEnd);

        Assert.Equal(QuietHoursAction.None, action);
        Assert.False(quiet.Enabled);

        quiet.Resume(AfterWindow.AddMinutes(10), quiet: false, WindowEnd);
        Assert.True(quiet.Enabled);
    }

    [Fact]
    public void ManualPause_OutsideQuietHours_IsNotResumed_WhenWindowEnds()
    {
        var quiet = new QuietHoursController();
        quiet.ManualPause();

        quiet.Tick(Night, quiet: true, WindowEnd);
        var action = quiet.Tick(AfterWindow, quiet: false, WindowEnd);

        Assert.Equal(QuietHoursAction.None, action);
        Assert.False(quiet.Enabled);
    }

    [Fact]
    public void LaterNight_PausesAgain_AfterManualPauseWasClearedByEnabling()
    {
        var quiet = new QuietHoursController();
        quiet.ManualPause();
        quiet.UserEnabled(Night.AddHours(-2), quiet: false, WindowEnd);

        var action = quiet.Tick(Night, quiet: true, WindowEnd);

        Assert.Equal(QuietHoursAction.Pause, action);
        Assert.False(quiet.Enabled);
    }

    [Fact]
    public async Task KilledBeforeProfileSave_StillShowsTutorial()
    {
        var storage = new DropUnflushedStorage();
        var profile = new Profile(storage) { Id = Profile.DefaultProfileId };
        await profile.SaveAsync();
        var completed = DateTime.UtcNow;
        await storage.SaveAsync("onboarding_state", new OnboardingState
        {
            HasCompletedOnboarding = true,
            OnboardingCompleted = completed
        });
        profile.SetSetting("CoachCompleted", true);
        profile.SetSetting("TutorialCompleted", false);
        storage.HoldWrites = true;
        await profile.SaveAsync();

        var relaunch = new Profile(storage) { Id = Profile.DefaultProfileId };
        await relaunch.LoadAsync();
        var onboarding = await storage.LoadAsync<OnboardingState>("onboarding_state");
        var profileExists = await storage.ExistsAsync(Profile.GetStorageKey(Profile.DefaultProfileId));
        var onboardingExists = await storage.ExistsAsync("onboarding_state");
        var isFirstRun = !profileExists || !onboardingExists;

        Assert.False(isFirstRun);
        Assert.False(relaunch.HasSetting("TutorialCompleted"));
        var show = TutorialLaunch.ShouldShow(
            relaunch.HasSetting("TutorialCompleted"),
            relaunch.GetSetting("TutorialCompleted", !isFirstRun),
            isFirstRun,
            onboarding?.OnboardingCompleted,
            DateTime.UtcNow);

        Assert.True(show);
    }

    [Fact]
    public void Upgrade_WithoutTutorialKey_DoesNotShowTutorial()
    {
        var show = TutorialLaunch.ShouldShow(
            tutorialKeyPresent: false,
            tutorialCompleted: false,
            isFirstRun: false,
            onboardingCompletedUtc: DateTime.UtcNow.AddDays(-30),
            utcNow: DateTime.UtcNow);

        Assert.False(show);
    }

    private sealed class DropUnflushedStorage : IStorage
    {
        private readonly Dictionary<string, string> _kept = new(StringComparer.Ordinal);

        public bool HoldWrites { get; set; }

        public Task SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
        {
            if (!HoldWrites)
            {
                _kept[key] = System.Text.Json.JsonSerializer.Serialize(data);
            }

            return Task.CompletedTask;
        }

        public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (!_kept.TryGetValue(key, out var json))
            {
                return Task.FromResult<T?>(default);
            }

            return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(json));
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _kept.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_kept.ContainsKey(key));
    }
}
