using Lexon.AI;
using Lexon.AI.Interfaces;
using Lexon.Core;
using Lexon.Profiles;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class AiSettingsViewModelTests
{
    private const string FakeKey = "sk-test-key-value-not-for-logs";

    [Fact]
    public void Load_MapsSevenKeys_AndDoesNotProbeOrPublish()
    {
        var settings = new AppSettings
        {
            AIProvider = "Gemini",
            APIKey = FakeKey,
            AIKeyValidated = true,
            AIModel = "gemini-1.5-flash",
            AiSuggestionsWhileTyping = true,
            AiRewriteOnRequest = false,
            AiPrefetchOnSelection = true
        };
        var env = Create(settings);
        env.Vm.Load();

        Assert.True(env.Vm.AdvancedVisible);
        Assert.Equal(IndexOf("Gemini"), env.Vm.ProviderIndex);
        Assert.Equal(FakeKey, env.Vm.ApiKeyText);
        Assert.Equal("gemini-1.5-flash", env.Vm.SelectedModel);
        Assert.True(env.Vm.AiSuggestionsWhileTyping);
        Assert.False(env.Vm.AiRewriteOnRequest);
        Assert.True(env.Vm.AiPrefetchOnSelection);
        Assert.False(env.Vm.IsDirty);
        Assert.Empty(env.Policy.Calls);
        Assert.Equal(0, env.ProbeCalls);
        Assert.DoesNotContain(FakeKey, env.Vm.StatusDetail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("OpenAI", false, true, true, true, "OpenAI (recommended)")]
    [InlineData("Gemini", false, true, true, true, "Gemini")]
    [InlineData("Ollama", false, false, true, true, "Ollama (local)")]
    [InlineData("None", false, false, false, true, "No AI provider")]
    [InlineData("OpenAI", true, false, false, false, "OpenAI (recommended)")]
    [InlineData("Gemini", true, false, false, false, "Gemini")]
    public void EntryMode_Matrix(
        string provider,
        bool localOnly,
        bool showKey,
        bool showModel,
        bool flagsEnabled,
        string heading)
    {
        var settings = new AppSettings { AIProvider = provider, LocalMode = localOnly };
        var env = Create(settings, localOnly: localOnly);
        env.Vm.Load();
        if (!AiProviderCatalog.IsRecommended(provider) && !provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(env.Vm.AdvancedVisible);
        }
        else if (provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            // None is not ShowAdvancedByDefault
            env.Vm.ShowMoreProviders();
            env.Vm.ProviderIndex = IndexOf("None");
        }
        else if (localOnly)
        {
            // recommended stays collapsed heading
        }

        if (provider.Equals("None", StringComparison.OrdinalIgnoreCase) && !env.Vm.AdvancedVisible)
        {
            env.Vm.ShowMoreProviders();
            env.Vm.ProviderIndex = IndexOf("None");
        }

        if (!provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
            && !provider.Equals("None", StringComparison.OrdinalIgnoreCase)
            && !env.Vm.AdvancedVisible)
        {
            env.Vm.ShowMoreProviders();
            env.Vm.ProviderIndex = IndexOf(provider);
        }

        Assert.Equal(showKey, env.Vm.ShowKeyBox);
        Assert.Equal(showKey, env.Vm.ShowGetKeyButton);
        Assert.Equal(showModel, env.Vm.ShowModel);
        Assert.Equal(flagsEnabled, env.Vm.AiFlagsEnabled);
        Assert.Equal(heading, env.Vm.Heading);
    }

    [Fact]
    public async Task UnvalidatedKey_IsNeverWritten()
    {
        var profile = new Profile();
        var settings = new AppSettings { AIProvider = "None", AIKeyValidated = true };
        settings.Write(profile);
        var env = Create(settings, profile);
        env.Vm.Load();
        env.Vm.ShowMoreProviders();
        env.Vm.ProviderIndex = IndexOf("OpenAI");
        env.Vm.ApiKeyText = FakeKey;
        // Fail the probe
        env.ProbeResult = AiProbeResult.Error("bad key");
        await env.FlushProbeAsync();

        OwnedSettingsWriter.Flush(profile, env.Vm);
        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.Equal("None", loaded.AIProvider);
        Assert.Equal(string.Empty, loaded.APIKey);
        Assert.DoesNotContain(FakeKey, loaded.APIKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessfulProbe_WritesActiveValues_AndAppliesProvider()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Write(profile);
        var env = Create(settings, profile);
        env.Vm.OnTabSelected();
        env.Vm.ApiKeyText = FakeKey;
        env.ProbeResult = AiProbeResult.Ok("ok");
        await env.FlushProbeAsync();

        Assert.True(env.Vm.IsDirty);
        Assert.Equal("OpenAI", env.Session.ActiveProvider);
        Assert.Equal(FakeKey, env.Session.ActiveApiKey);
        Assert.True(env.Session.AiValidated);
        Assert.Single(env.Applied);
        OwnedSettingsWriter.Flush(profile, env.Vm);
        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.Equal("OpenAI", loaded.AIProvider);
        Assert.Equal(FakeKey, loaded.APIKey);
        Assert.True(loaded.AIKeyValidated);
    }

    [Fact]
    public void CopyOwnedTo_UsesActiveNotTextBox()
    {
        var env = Create(new AppSettings { AIProvider = "None", AIKeyValidated = true });
        env.Vm.Load();
        env.Session.ActiveProvider = "OpenAI";
        env.Session.ActiveApiKey = "sk-active";
        env.Session.AiValidated = true;
        env.Vm.ApiKeyText = "sk-typed-not-active";
        var target = new AppSettings();
        env.Vm.CopyOwnedTo(target);
        Assert.Equal("OpenAI", target.AIProvider);
        Assert.Equal("sk-active", target.APIKey);
        Assert.NotEqual("sk-typed-not-active", target.APIKey);
    }

    [Fact]
    public void KeyEdit_DebouncesProbe_LostFocusProbesImmediately()
    {
        var env = Create(new AppSettings());
        env.Vm.Load();
        env.ProbeCalls = 0;
        env.Vm.ApiKeyText = "sk-a";
        Assert.Equal(0, env.ProbeCalls);
        Assert.Single(env.Delays.Pending);
        Assert.Equal(AiSettingsViewModel.KeyProbeDelay, env.Delays.Pending[0].Delay);

        env.Vm.OnKeyLostFocus();
        Assert.Empty(env.Delays.Pending);
        Assert.Equal(1, env.ProbeCalls);
    }

    [Fact]
    public void ReloadUnchanged_DoesNotProbe_ChangedSeedsAndProbes()
    {
        var settings = new AppSettings
        {
            AIProvider = "OpenAI",
            APIKey = FakeKey,
            AIKeyValidated = true,
            AIModel = "gpt-4o-mini"
        };
        var env = Create(settings);
        env.Session.ActiveProvider = "OpenAI";
        env.Session.ActiveApiKey = FakeKey;
        env.Session.AiValidated = true;
        env.Vm.OnTabSelected();
        var probesAfterFirst = env.ProbeCalls;

        env.Vm.Load();
        Assert.Equal(probesAfterFirst, env.ProbeCalls);

        settings.APIKey = "sk-changed-by-classic";
        env.ReadLocalOnly = () => false;
        env.Vm.Load();
        Assert.True(env.ProbeCalls > probesAfterFirst);
        Assert.Equal("sk-changed-by-classic", env.Session.ActiveApiKey);
    }

    [Fact]
    public void GetApiKey_OpensUrl_AndWatches_MatchingKeyProbes()
    {
        var env = Create(new AppSettings());
        env.Vm.Load();
        env.Clipboard.StartResult = true;
        env.Vm.GetApiKey();
        Assert.Equal("https://platform.openai.com/api-keys", env.Urls.Opened);
        Assert.True(env.Vm.IsWaitingForClipboard);
        Assert.Equal(AiSettingsViewModel.WatchingClipboardMessage, env.Vm.StatusDetail);

        env.Clipboard.Text = "not-a-key";
        env.Clipboard.RaiseUpdated();
        Assert.True(env.Vm.IsWaitingForClipboard);
        Assert.Equal(string.Empty, env.Vm.ApiKeyText);

        env.Clipboard.Text = "sk-proj-" + new string('a', 24);
        env.ProbeCalls = 0;
        env.Clipboard.RaiseUpdated();
        Assert.False(env.Vm.IsWaitingForClipboard);
        Assert.Equal(1, env.ProbeCalls);
        Assert.DoesNotContain(FakeKey, env.Vm.StatusDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void GetApiKey_OpenFailure_DoesNotWatch()
    {
        var env = Create(new AppSettings());
        env.Vm.Load();
        env.Urls.Succeed = false;
        env.Vm.GetApiKey();
        Assert.False(env.Vm.IsWaitingForClipboard);
        Assert.Equal(AiSettingsViewModel.OpenKeyPageFailedMessage, env.Vm.StatusDetail);
        Assert.Equal(AiStatusKind.Error, env.Vm.StatusKind);
    }

    [Fact]
    public void Clipboard_ListenerFailure_ShowsWarning()
    {
        var env = Create(new AppSettings());
        env.Vm.Load();
        env.Clipboard.StartResult = false;
        env.Vm.GetApiKey();
        Assert.False(env.Vm.IsWaitingForClipboard);
        Assert.Equal(AiSettingsViewModel.ClipboardWatchFailedMessage, env.Vm.StatusDetail);
        Assert.Equal(AiStatusKind.Warning, env.Vm.StatusKind);
    }

    [Fact]
    public void CancelAndTimeout_ShowExactMessages()
    {
        var env = Create(new AppSettings());
        env.Vm.Load();
        env.Clipboard.StartResult = true;
        env.Vm.GetApiKey();
        env.Vm.CancelWait();
        Assert.Equal(AiSettingsViewModel.ClipboardCancelledMessage, env.Vm.StatusDetail);

        env.Vm.GetApiKey();
        env.Delays.RunMatching(AiSettingsViewModel.ClipboardWatchTimeout);
        Assert.Equal(AiSettingsViewModel.ClipboardTimedOutMessage, env.Vm.StatusDetail);
    }

    [Fact]
    public void LocalOnly_FromPrivacy_UnloadsAndDisables()
    {
        var settings = new AppSettings();
        var env = Create(settings);
        env.Vm.OnTabSelected();
        env.Applied.Clear();
        env.Vm.OnLocalOnlyChanged(true);
        Assert.Contains(env.Applied, p => p == null);
        Assert.False(env.Vm.AiFlagsEnabled);
        Assert.False(env.Vm.ShowKeyBox);
        Assert.Equal(AiConnectionState.LocalOnlyOff, env.Session.Connection.State);
        Assert.False(env.Vm.IsWaitingForClipboard);
    }

    [Fact]
    public void Policy_AiFlagThenLocalOnly_UsesLiveSnapshotValues()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Write(profile);
        PrivacySettingsViewModel privacy = null!;
        AiSettingsViewModel ai = null!;
        var pages = new List<IOwnedSettingsPage>();
        AppSettings Live() => OwnedSettingsWriter.BuildLiveSnapshot(profile, pages);
        var policy = new FakePolicy();
        var persist = new PersistScheduler(() => { });
        var session = new AiProbeSession();
        var delays = new FakeDelays();
        ai = new AiSettingsViewModel(
            settings, persist, session, policy, new FakeClipboard(), new FakeUrls(), delays,
            () => Live().LocalMode, Live);
        privacy = new PrivacySettingsViewModel(
            settings, persist, policy, new FakePicker(), new FakeLog(),
            () => settings.Read(profile), Live);
        pages.Add(ai);
        pages.Add(privacy);
        ai.Load();
        privacy.Load();

        ai.AiSuggestionsWhileTyping = true;
        Assert.True(ai.IsDirty);
        Assert.Equal((false, true, true, false), policy.Calls[^1]);

        privacy.LocalOnly = true;
        Assert.Equal((true, true, true, false), policy.Calls[^1]);
    }

    [Fact]
    public void DirtySafety_CleanAiPageWritesNothing()
    {
        var profile = new SpyProfile();
        var settings = new AppSettings { APIKey = "sk-classic", AIProvider = "OpenAI", AIKeyValidated = true };
        settings.Write(profile);
        profile.Written.Clear();
        var env = Create(settings, profile);
        env.Session.ActiveProvider = "OpenAI";
        env.Session.ActiveApiKey = "sk-classic";
        env.Session.AiValidated = true;
        env.Vm.Load();
        Assert.False(env.Vm.IsDirty);

        var general = new GeneralSettingsViewModel(settings, new SilentStartup(), new PersistScheduler(() => { }));
        general.Load();
        general.MinimizeToTray = false;
        OwnedSettingsWriter.Flush(profile, general, env.Vm);

        Assert.DoesNotContain(AppSettings.APIKeyKey, profile.Written);
        Assert.Equal("sk-classic", profile.GetSetting(AppSettings.APIKeyKey, ""));
    }

    [Fact]
    public void RoundTrip_OwnedKeys()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Read(profile);
        var env = Create(settings, profile);
        env.Vm.Load();
        env.Session.ActiveProvider = "DeepSeek";
        env.Session.ActiveApiKey = "sk-" + new string('b', 32);
        env.Session.AiValidated = true;
        env.Vm.ShowMoreProviders();
        env.Vm.ProviderIndex = IndexOf("DeepSeek");
        env.Vm.SelectedModel = "deepseek-chat";
        env.Vm.AiSuggestionsWhileTyping = true;
        env.Vm.AiRewriteOnRequest = false;
        env.Vm.AiPrefetchOnSelection = true;
        // Force dirty from session values
        env.Session.Persist();
        OwnedSettingsWriter.Flush(profile, env.Vm);

        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.Equal("DeepSeek", loaded.AIProvider);
        Assert.Equal(env.Session.ActiveApiKey, loaded.APIKey);
        Assert.True(loaded.AIKeyValidated);
        Assert.Equal("deepseek-chat", loaded.AIModel);
        Assert.True(loaded.AiSuggestionsWhileTyping);
        Assert.False(loaded.AiRewriteOnRequest);
        Assert.True(loaded.AiPrefetchOnSelection);
    }

    [Fact]
    public void Secrets_NeverAppearInStatus()
    {
        var env = Create(new AppSettings());
        env.Vm.Load();
        env.Vm.ApiKeyText = FakeKey;
        env.ProbeResult = AiProbeResult.Error("Invalid credential");
        env.Vm.OnKeyLostFocus();
        Assert.DoesNotContain(FakeKey, env.Vm.StatusActiveLine, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeKey, env.Vm.StatusDetail, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeKey, env.Session.Connection.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_Unchanged_KeepsUnsavedProviderAndKey()
    {
        var settings = new AppSettings { AIProvider = "OpenAI", APIKey = string.Empty, AIKeyValidated = false };
        var env = Create(settings);
        env.Vm.OnTabSelected();
        env.ProbeCalls = 0;
        env.Vm.ShowMoreProviders();
        env.Vm.ProviderIndex = IndexOf("Gemini");
        env.Vm.ApiKeyText = "AIza" + new string('x', 35);
        Assert.True(env.Vm.HasUnsavedUiState);
        Assert.True(env.Vm.AdvancedVisible);

        env.Vm.Load();
        env.Vm.OnTabSelected();

        Assert.Equal(IndexOf("Gemini"), env.Vm.ProviderIndex);
        Assert.Equal("AIza" + new string('x', 35), env.Vm.ApiKeyText);
        Assert.True(env.Vm.AdvancedVisible);
        Assert.True(env.Vm.HasUnsavedUiState);
    }

    [Fact]
    public async Task Load_Unchanged_KeepsProviderWhileProbeInFlight()
    {
        var settings = new AppSettings { AIProvider = "OpenAI", APIKey = string.Empty };
        var env = Create(settings);
        env.Vm.OnTabSelected();
        env.Vm.ShowMoreProviders();
        env.Vm.ProviderIndex = IndexOf("Gemini");
        env.ProbeResult = AiProbeResult.Ok("ok");
        env.Vm.ApiKeyText = "AIza" + new string('y', 35);
        // Start probe without completing yet.
        var tcs = new TaskCompletionSource<AiProbeResult>();
        env.Session.ProbeAsync = async (provider, key, token, model) =>
        {
            env.ProbeCalls++;
            Assert.Equal("Gemini", provider);
            return await tcs.Task;
        };
        env.Vm.OnKeyLostFocus();
        Assert.Equal(1, env.ProbeCalls);
        Assert.Equal(AiConnectionState.Checking, env.Session.Connection.State);

        env.Vm.Load();
        Assert.Equal(IndexOf("Gemini"), env.Vm.ProviderIndex);

        tcs.SetResult(AiProbeResult.Ok("ok"));
        await Task.Delay(30);
        Assert.Equal("Gemini", env.Session.ActiveProvider);
        Assert.Equal(IndexOf("Gemini"), env.Vm.ProviderIndex);
    }

    [Fact]
    public void Load_ProfileChanged_DiscardsUnsavedUi_AndReseeds()
    {
        var settings = new AppSettings
        {
            AIProvider = "OpenAI",
            APIKey = FakeKey,
            AIKeyValidated = true,
            AIModel = "gpt-4o-mini"
        };
        var env = Create(settings);
        env.Session.ActiveProvider = "OpenAI";
        env.Session.ActiveApiKey = FakeKey;
        env.Session.AiValidated = true;
        env.Vm.OnTabSelected();
        env.Vm.ShowMoreProviders();
        env.Vm.ProviderIndex = IndexOf("Gemini");
        env.Vm.ApiKeyText = "AIza-unsaved";
        env.Clipboard.StartResult = true;
        env.Vm.GetApiKey();
        Assert.True(env.Vm.IsWaitingForClipboard);

        settings.AIProvider = "DeepSeek";
        settings.APIKey = "sk-" + new string('d', 32);
        settings.AIKeyValidated = true;
        settings.AIModel = "deepseek-chat";
        var probesBefore = env.ProbeCalls;
        env.Vm.Load();

        Assert.Equal(IndexOf("DeepSeek"), env.Vm.ProviderIndex);
        Assert.Equal(settings.APIKey, env.Vm.ApiKeyText);
        Assert.Equal("DeepSeek", env.Session.ActiveProvider);
        Assert.False(env.Vm.IsWaitingForClipboard);
        Assert.False(env.Clipboard.Watching);
        Assert.True(env.ProbeCalls > probesBefore);
    }

    [Fact]
    public void OnTabLeft_DoesNotCancelPendingProbe_WindowHiddenDoes()
    {
        var env = Create(new AppSettings());
        env.Vm.OnTabSelected();
        env.Vm.ApiKeyText = FakeKey;
        Assert.Single(env.Delays.Pending.Where(p => p.Delay == AiSettingsViewModel.KeyProbeDelay));

        env.Vm.OnTabLeft();
        Assert.Single(env.Delays.Pending.Where(p => p.Delay == AiSettingsViewModel.KeyProbeDelay));

        env.ProbeCalls = 0;
        env.Delays.RunMatching(AiSettingsViewModel.KeyProbeDelay);
        Assert.Equal(1, env.ProbeCalls);

        env.Vm.ApiKeyText = FakeKey + "2";
        Assert.Single(env.Delays.Pending.Where(p => p.Delay == AiSettingsViewModel.KeyProbeDelay));
        env.Clipboard.StartResult = true;
        env.Vm.GetApiKey();
        Assert.True(env.Vm.IsWaitingForClipboard);
        env.Vm.OnWindowHidden();
        Assert.Empty(env.Delays.Pending.Where(p => p.Delay == AiSettingsViewModel.KeyProbeDelay));
        Assert.False(env.Vm.IsWaitingForClipboard);
        Assert.False(env.Clipboard.Watching);
    }

    [Fact]
    public void ClipboardWatch_SurvivesUnchangedLoad_StopsOnReseed()
    {
        var settings = new AppSettings { AIProvider = "OpenAI", APIKey = FakeKey, AIKeyValidated = true };
        var env = Create(settings);
        env.Session.ActiveProvider = "OpenAI";
        env.Session.ActiveApiKey = FakeKey;
        env.Session.AiValidated = true;
        env.Vm.OnTabSelected();
        env.Clipboard.StartResult = true;
        env.Vm.GetApiKey();
        Assert.True(env.Vm.IsWaitingForClipboard);

        env.Vm.Load();
        Assert.True(env.Vm.IsWaitingForClipboard);
        Assert.True(env.Clipboard.Watching);

        settings.APIKey = "sk-classic-changed";
        env.Vm.Load();
        Assert.False(env.Vm.IsWaitingForClipboard);
        Assert.False(env.Clipboard.Watching);
    }

    private static int IndexOf(string name)
        => Array.FindIndex(AiProviderCatalog.AllProviders, p => p.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static TestEnv Create(AppSettings settings, Profile? profile = null, bool localOnly = false)
    {
        profile ??= new Profile();
        settings.LocalMode = localOnly;
        var persist = new PersistScheduler(() => { });
        var policy = new FakePolicy();
        var delays = new FakeDelays();
        var clipboard = new FakeClipboard();
        var urls = new FakeUrls();
        var applied = new List<IAIProvider?>();
        var session = new AiProbeSession
        {
            ApplyProvider = p => applied.Add(p),
            InstalledProviderName = () => null
        };
        var env = new TestEnv
        {
            Settings = settings,
            Profile = profile,
            Persist = persist,
            Policy = policy,
            Delays = delays,
            Clipboard = clipboard,
            Urls = urls,
            Session = session,
            Applied = applied,
            ReadLocalOnly = () => localOnly
        };
        session.ProbeAsync = async (provider, key, token, model) =>
        {
            env.ProbeCalls++;
            await Task.Yield();
            return env.ProbeResult;
        };
        env.Vm = new AiSettingsViewModel(
            settings, persist, session, policy, clipboard, urls, delays,
            () => env.ReadLocalOnly(),
            () =>
            {
                var snap = new AppSettings();
                snap.Read(profile);
                if (env.Vm.IsDirty)
                {
                    env.Vm.CopyOwnedTo(snap);
                }

                return snap;
            });
        return env;
    }

    private sealed class TestEnv
    {
        public AppSettings Settings { get; init; } = null!;
        public Profile Profile { get; init; } = null!;
        public PersistScheduler Persist { get; init; } = null!;
        public FakePolicy Policy { get; init; } = null!;
        public FakeDelays Delays { get; init; } = null!;
        public FakeClipboard Clipboard { get; init; } = null!;
        public FakeUrls Urls { get; init; } = null!;
        public AiProbeSession Session { get; init; } = null!;
        public List<IAIProvider?> Applied { get; init; } = null!;
        public AiSettingsViewModel Vm { get; set; } = null!;
        public Func<bool> ReadLocalOnly { get; set; } = static () => false;
        public int ProbeCalls { get; set; }
        public AiProbeResult ProbeResult { get; set; } = AiProbeResult.Ok("ok");

        public async Task FlushProbeAsync()
        {
            Delays.RunMatching(AiSettingsViewModel.KeyProbeDelay);
            await Task.Delay(20);
        }
    }

    private sealed class FakePolicy : IAiPolicyPublisher
    {
        public List<(bool Local, bool Typing, bool Rewrite, bool Prefetch)> Calls { get; } = [];

        public void Publish(bool localOnly, bool typing, bool rewrite, bool prefetch)
            => Calls.Add((localOnly, typing, rewrite, prefetch));
    }

    private sealed class FakeDelays : IDelayScheduler
    {
        public List<(TimeSpan Delay, Action Action)> Pending { get; } = [];

        public IDisposable Schedule(TimeSpan delay, Action action)
        {
            Pending.Add((delay, action));
            return new Handle(this, action);
        }

        public void RunMatching(TimeSpan delay)
        {
            var hit = Pending.Where(p => p.Delay == delay).ToList();
            Pending.RemoveAll(p => p.Delay == delay);
            foreach (var item in hit)
            {
                item.Action();
            }
        }

        private sealed class Handle : IDisposable
        {
            private readonly FakeDelays _owner;
            private readonly Action _action;

            public Handle(FakeDelays owner, Action action)
            {
                _owner = owner;
                _action = action;
            }

            public void Dispose() => _owner.Pending.RemoveAll(p => p.Action == _action);
        }
    }

    private sealed class FakeClipboard : IClipboardWatch
    {
        public bool StartResult { get; set; } = true;
        public string? Text { get; set; }
        public bool Watching { get; private set; }

        public event EventHandler? Updated;

        public bool Start()
        {
            Watching = StartResult;
            return StartResult;
        }

        public void Stop() => Watching = false;

        public string? ReadText() => Text;

        public void RaiseUpdated() => Updated?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeUrls : IUrlLauncher
    {
        public bool Succeed { get; set; } = true;
        public string? Opened { get; private set; }

        public bool TryOpen(string url)
        {
            Opened = url;
            return Succeed;
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

    private sealed class SilentStartup : IStartupRegistration
    {
        public bool IsEnabled() => false;

        public bool TrySetEnabled(bool enabled) => true;
    }

    private sealed class SpyProfile : Profile
    {
        public List<string> Written { get; } = [];

        public override void SetSetting<T>(string key, T value)
        {
            Written.Add(key);
            base.SetSetting(key, value);
        }
    }
}
