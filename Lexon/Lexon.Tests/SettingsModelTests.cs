using Lexon.AI;
using Lexon.AI.Interfaces;
using Lexon.Core;
using Lexon.Core.Models;
using Lexon.Profiles;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class AppSettingsRoundTripTests
{
    [Fact]
    public void RoundTripsEverySettingsKey()
    {
        var profile = new Profile();
        var source = new AppSettings
        {
            MinimizeToTray = false,
            EnableAutoUpdates = false,
            OpenSettingsFullScreen = true,
            QuickPauseMinutes = 30,
            AIProvider = "OpenAI",
            APIKey = "sk-test",
            AIKeyValidated = true,
            AIModel = "gpt-4o-mini",
            LocalMode = true,
            AiSuggestionsWhileTyping = true,
            AiRewriteOnRequest = false,
            AiPrefetchOnSelection = true,
            BlockedApplications = ["putty", "mstsc"],
            Theme = "Dark",
            SuggestionSortMode = "Used",
            SuggestionPlacement = "Above",
            RequireConfirmationForEdits = false,
            GrammarSensitivity = "High",
            MuteGrammarForCasualApps = true,
            EnableRewriteHotkey = false,
            GrammarChecking = false,
            AutoCorrectTypos = false,
            AutoInsertSpaces = false,
            AutoCorrectContractions = true,
            EnableGrammarHotkey = false,
            GrammarMutedApps = ["discord.exe"],
            AppCategoryOverrides = ["slack.exe|Casual"],
            CustomTerminology = ["Lexon", "Kube"],
            AppTerminologyOverrides = ["code.exe|Dotnet"],
            DefaultWritingMode = "More concise"
        };
        source.Write(profile);

        var loaded = new AppSettings();
        loaded.Read(profile);

        Assert.Equal(source.MinimizeToTray, loaded.MinimizeToTray);
        Assert.Equal(source.EnableAutoUpdates, loaded.EnableAutoUpdates);
        Assert.Equal(source.OpenSettingsFullScreen, loaded.OpenSettingsFullScreen);
        Assert.Equal(source.QuickPauseMinutes, loaded.QuickPauseMinutes);
        Assert.Equal(source.AIProvider, loaded.AIProvider);
        Assert.Equal(source.APIKey, loaded.APIKey);
        Assert.Equal(source.AIKeyValidated, loaded.AIKeyValidated);
        Assert.Equal(source.AIModel, loaded.AIModel);
        Assert.Equal(source.LocalMode, loaded.LocalMode);
        Assert.Equal(source.AiSuggestionsWhileTyping, loaded.AiSuggestionsWhileTyping);
        Assert.Equal(source.AiRewriteOnRequest, loaded.AiRewriteOnRequest);
        Assert.Equal(source.AiPrefetchOnSelection, loaded.AiPrefetchOnSelection);
        Assert.Equal(source.BlockedApplications, loaded.BlockedApplications);
        Assert.Equal(source.Theme, loaded.Theme);
        Assert.Equal(source.SuggestionSortMode, loaded.SuggestionSortMode);
        Assert.Equal(source.SuggestionPlacement, loaded.SuggestionPlacement);
        Assert.Equal(source.RequireConfirmationForEdits, loaded.RequireConfirmationForEdits);
        Assert.Equal(source.GrammarSensitivity, loaded.GrammarSensitivity);
        Assert.Equal(source.MuteGrammarForCasualApps, loaded.MuteGrammarForCasualApps);
        Assert.Equal(source.EnableRewriteHotkey, loaded.EnableRewriteHotkey);
        Assert.Equal(source.GrammarChecking, loaded.GrammarChecking);
        Assert.Equal(source.AutoCorrectTypos, loaded.AutoCorrectTypos);
        Assert.Equal(source.AutoInsertSpaces, loaded.AutoInsertSpaces);
        Assert.Equal(source.AutoCorrectContractions, loaded.AutoCorrectContractions);
        Assert.Equal(source.EnableGrammarHotkey, loaded.EnableGrammarHotkey);
        Assert.Equal(source.GrammarMutedApps, loaded.GrammarMutedApps);
        Assert.Equal(source.AppCategoryOverrides, loaded.AppCategoryOverrides);
        Assert.Equal(source.CustomTerminology, loaded.CustomTerminology);
        Assert.Equal(source.AppTerminologyOverrides, loaded.AppTerminologyOverrides);
        Assert.Equal(source.DefaultWritingMode, loaded.DefaultWritingMode);

        Assert.True(profile.HasSetting(AppSettings.MinimizeToTrayKey));
        Assert.True(profile.HasSetting(AppSettings.EnableAutoUpdatesKey));
        Assert.True(profile.HasSetting(AppSettings.OpenSettingsFullScreenKey));
        Assert.True(profile.HasSetting(AppSettings.QuickPauseMinutesKey));
        Assert.True(profile.HasSetting(AppSettings.AIProviderKey));
        Assert.True(profile.HasSetting(AppSettings.APIKeyKey));
        Assert.True(profile.HasSetting(AppSettings.AIKeyValidatedKey));
        Assert.True(profile.HasSetting(AppSettings.AIModelKey));
        Assert.True(profile.HasSetting(AppSettings.LocalModeKey));
        Assert.True(profile.HasSetting(AppSettings.AiSuggestionsWhileTypingKey));
        Assert.True(profile.HasSetting(AppSettings.AiRewriteOnRequestKey));
        Assert.True(profile.HasSetting(AppSettings.AiPrefetchOnSelectionKey));
        Assert.True(profile.HasSetting(AppSettings.BlockedApplicationsKey));
        Assert.True(profile.HasSetting(AppSettings.ThemeKey));
        Assert.True(profile.HasSetting(AppSettings.SuggestionSortModeKey));
        Assert.True(profile.HasSetting(AppSettings.SuggestionPlacementKey));
        Assert.True(profile.HasSetting(AppSettings.RequireConfirmationForEditsKey));
        Assert.True(profile.HasSetting(AppSettings.GrammarSensitivityKey));
        Assert.True(profile.HasSetting(AppSettings.MuteGrammarForCasualAppsKey));
        Assert.True(profile.HasSetting(AppSettings.EnableRewriteHotkeyKey));
        Assert.True(profile.HasSetting(AppSettings.GrammarCheckingKey));
        Assert.True(profile.HasSetting(AppSettings.AutoCorrectTyposKey));
        Assert.True(profile.HasSetting(AppSettings.AutoInsertSpacesKey));
        Assert.True(profile.HasSetting(AppSettings.AutoCorrectContractionsKey));
        Assert.True(profile.HasSetting(AppSettings.EnableGrammarHotkeyKey));
        Assert.True(profile.HasSetting(AppSettings.GrammarMutedAppsKey));
        Assert.True(profile.HasSetting(AppSettings.AppCategoryOverridesKey));
        Assert.True(profile.HasSetting(AppSettings.CustomTerminologyKey));
        Assert.True(profile.HasSetting(AppSettings.AppTerminologyOverridesKey));
        Assert.True(profile.HasSetting(AppSettings.DefaultWritingModeKey));
    }

    [Fact]
    public void DefaultsMatchOneOhEight()
    {
        var settings = new AppSettings();
        var profile = new Profile();
        settings.Write(profile);
        Assert.True(profile.GetSetting(AppSettings.MinimizeToTrayKey, false));
        Assert.Equal(15, profile.GetSetting(AppSettings.QuickPauseMinutesKey, 0));
        Assert.Equal("None", profile.GetSetting(AppSettings.AIProviderKey, ""));
        Assert.False(profile.GetSetting(AppSettings.AiSuggestionsWhileTypingKey, true));
        Assert.True(profile.GetSetting(AppSettings.AiRewriteOnRequestKey, false));
        Assert.Equal("Relevant", profile.GetSetting(AppSettings.SuggestionSortModeKey, ""));
        Assert.Equal("Below", profile.GetSetting(AppSettings.SuggestionPlacementKey, ""));
        Assert.Equal("Medium", profile.GetSetting(AppSettings.GrammarSensitivityKey, ""));
    }
}

public class QuickPauseOptionsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 15)]
    [InlineData(5, 60)]
    [InlineData(-1, 15)]
    public void MinutesFromIndex(int index, int minutes)
        => Assert.Equal(minutes, QuickPauseOptions.MinutesFromIndex(index));

    [Theory]
    [InlineData(15, 3)]
    [InlineData(99, 3)]
    [InlineData(0, 0)]
    public void IndexFromMinutes(int minutes, int index)
        => Assert.Equal(index, QuickPauseOptions.IndexFromMinutes(minutes));
}

public class BlockedAppListTests
{
    [Fact]
    public void ParseNormalizesAndDedupes()
    {
        var list = BlockedAppList.Parse("putty.exe, PUTTY, notepad.exe, notepad");
        Assert.Equal(["putty", "notepad"], list);
    }

    [Fact]
    public void TryAddRejectsDuplicate()
    {
        var current = new List<string> { "chrome" };
        Assert.False(BlockedAppList.TryAdd(current, "chrome.exe", out _));
        Assert.True(BlockedAppList.TryAdd(current, "firefox.exe", out var name));
        Assert.Equal("firefox", name);
        Assert.Equal(2, current.Count);
    }
}

public class AppToneListTests
{
    [Fact]
    public void AddOrReplaceNormalizesAndDedupes()
    {
        var rows = AppToneList.AddOrReplace([], "Slack.EXE", AppWritingCategory.Casual);
        rows = AppToneList.AddOrReplace(rows, "slack", AppWritingCategory.Formal);
        Assert.Single(rows);
        Assert.Contains("slack.exe", rows[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Formal", rows[0]);
    }
}

public class PersistSchedulerTests
{
    [Fact]
    public void DebounceRestartsAndFlushOnClose()
    {
        var flushes = 0;
        var clock = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        var persist = new PersistScheduler(() => flushes++, TimeSpan.FromMilliseconds(400));
        persist.Schedule(clock);
        Assert.False(persist.TryFlushDue(clock.AddMilliseconds(200)));
        persist.Schedule(clock.AddMilliseconds(200));
        Assert.False(persist.TryFlushDue(clock.AddMilliseconds(500)));
        Assert.True(persist.TryFlushDue(clock.AddMilliseconds(650)));
        Assert.Equal(1, flushes);

        persist.Schedule(clock.AddSeconds(2));
        persist.Flush();
        Assert.Equal(2, flushes);
        Assert.False(persist.HasPending);
    }

    [Fact]
    public void LoadingSuppressesFlush()
    {
        var flushes = 0;
        var persist = new PersistScheduler(() => flushes++) { IsLoading = true };
        persist.Schedule(DateTime.UtcNow);
        persist.Flush();
        Assert.Equal(0, flushes);
    }
}

public class AiProbeSessionTests
{
    [Fact]
    public async Task StaleProbeResultIsIgnored()
    {
        var applied = new List<string?>();
        var session = new AiProbeSession
        {
            ApplyProvider = p => applied.Add(p?.Name),
            ProbeAsync = async (provider, key, token, model) =>
            {
                await Task.Delay(80, token);
                return AiProbeResult.Ok("late");
            }
        };

        var first = session.ProbeAsyncCore(false, "OpenAI", "sk-a", "gpt-4o-mini", false);
        await session.ProbeAsyncCore(false, "None", "", "", false);
        await first;

        Assert.Equal("None", session.ActiveProvider);
        Assert.DoesNotContain("OpenAI", applied);
        Assert.Contains(applied, name => name == null);
    }

    [Fact]
    public void LocalOnlyTurnsAiOffImmediately()
    {
        IAIProvider? last = new FakeProvider();
        var session = new AiProbeSession
        {
            ActiveProvider = "OpenAI",
            ApplyProvider = p => last = p
        };
        session.EnterLocalOnly();
        Assert.Null(last);
        Assert.Equal(AiConnectionState.LocalOnlyOff, session.Connection.State);
        Assert.Null(session.Connection.ConnectedProvider);
    }

    [Fact]
    public void ResolveUiProviderUsesRecommendedUntilAdvanced()
    {
        Assert.Equal("OpenAI", AiModelChoices.ResolveUiProvider(false, "Gemini"));
        Assert.Equal("Gemini", AiModelChoices.ResolveUiProvider(true, "Gemini"));
    }

    [Fact]
    public void ForProvider_SelectsDefaultWhenEmpty()
    {
        var provider = "OpenAI";
        var models = AiModelChoices.ForProvider(provider, "");
        var expected = AiProviderCatalog.DefaultModel(provider);
        Assert.Contains(expected, models);
        Assert.Equal(expected, AiModelChoices.ResolveSelected(provider, null));
    }

    [Fact]
    public async Task OnStatusChanged_ReportsCheckingBeforeProbeCompletesThenConnected()
    {
        var gate = new TaskCompletionSource<AiProbeResult>();
        var states = new List<AiConnectionState>();
        AiProbeSession session = null!;
        session = new AiProbeSession
        {
            ProbeAsync = async (_, _, _, _) => await gate.Task,
            OnStatusChanged = () => states.Add(session.Connection.State),
            ApplyProvider = _ => { },
            CreateProvider = (_, _, _) => new FakeProvider()
        };

        var probe = session.ProbeAsyncCore(false, "OpenAI", "sk-test", "gpt-4o-mini", false);
        Assert.Equal([AiConnectionState.Checking], states);
        Assert.False(probe.IsCompleted);

        gate.SetResult(AiProbeResult.Ok("ok"));
        await probe;
        Assert.Equal([AiConnectionState.Checking, AiConnectionState.Connected], states);
    }

    [Fact]
    public async Task StaleProbeResult_DoesNotRaiseStatusCallback()
    {
        var gate = new TaskCompletionSource<AiProbeResult>();
        var states = new List<AiConnectionState>();
        AiProbeSession session = null!;
        session = new AiProbeSession
        {
            ProbeAsync = async (_, _, _, _) => await gate.Task,
            OnStatusChanged = () => states.Add(session.Connection.State),
            ApplyProvider = _ => { }
        };

        var first = session.ProbeAsyncCore(false, "OpenAI", "sk-a", "gpt-4o-mini", false);
        Assert.Equal(AiConnectionState.Checking, Assert.Single(states));

        await session.ProbeAsyncCore(false, "None", "", "", false);
        var countAfterNone = states.Count;
        Assert.Equal(AiConnectionState.NotConfigured, states[^1]);

        gate.SetResult(AiProbeResult.Ok("late"));
        await first;
        Assert.Equal(countAfterNone, states.Count);
    }

    [Fact]
    public async Task LocalOnly_ReportsImmediately()
    {
        var states = new List<AiConnectionState>();
        AiProbeSession session = null!;
        session = new AiProbeSession
        {
            OnStatusChanged = () => states.Add(session.Connection.State),
            ApplyProvider = _ => { }
        };

        session.EnterLocalOnly();
        Assert.Equal(AiConnectionState.LocalOnlyOff, Assert.Single(states));

        states.Clear();
        await session.ProbeAsyncCore(true, "OpenAI", "sk-test", "gpt-4o-mini", false);
        Assert.Equal(AiConnectionState.LocalOnlyOff, Assert.Single(states));
    }

    [Fact]
    public async Task ResolveModelAfterSuccess_IsNotCalled_ForNoneEmptyKeyLocalOnlyOrFailedProbe()
    {
        var calls = 0;
        Func<string, string> resolve = model =>
        {
            calls++;
            return model;
        };

        await new AiProbeSession { ResolveModelAfterSuccess = resolve }.ProbeAsyncCore(false, "None", "", "gpt-4o-mini", false);
        await new AiProbeSession { ResolveModelAfterSuccess = resolve }.ProbeAsyncCore(false, "OpenAI", "", "gpt-4o-mini", false);
        await new AiProbeSession { ResolveModelAfterSuccess = resolve }.ProbeAsyncCore(true, "OpenAI", "sk-test", "gpt-4o-mini", false);
        await new AiProbeSession
        {
            ResolveModelAfterSuccess = resolve,
            ProbeAsync = (_, _, _, _) => Task.FromResult(AiProbeResult.Error("no"))
        }.ProbeAsyncCore(false, "OpenAI", "sk-test", "gpt-4o-mini", false);

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ResolveModelAfterSuccess_IsCalledOnce_BeforeEnterConnected()
    {
        var order = new List<string>();
        AiProbeSession session = null!;
        session = new AiProbeSession
        {
            ProbeAsync = (_, _, _, _) => Task.FromResult(AiProbeResult.Ok("ok")),
            ResolveModelAfterSuccess = model =>
            {
                order.Add("resolve");
                Assert.Equal(AiConnectionState.Checking, session.Connection.State);
                return model;
            },
            OnStatusChanged = () => order.Add(session.Connection.State.ToString()),
            ApplyProvider = _ => { },
            CreateProvider = (_, _, _) => new FakeProvider()
        };

        await session.ProbeAsyncCore(false, "OpenAI", "sk-test", "gpt-4o-mini", false);

        Assert.Equal(["Checking", "resolve", "Connected"], order);
        Assert.Equal(AiConnectionState.Connected, session.Connection.State);
        Assert.Equal("gpt-4o-mini", session.Connection.ConnectedModel);
    }

    private sealed class FakeProvider : IAIProvider
    {
        public string Name => "OpenAI";
        public Task<IEnumerable<Lexon.Core.Models.Suggestion>> GetSuggestionsAsync(Lexon.Core.Models.TextContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<Lexon.Core.Models.Suggestion>());
        public Task<string> RewriteTextAsync(string text, string instruction, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
            => Task.FromResult(text);
        public Task<string> ImproveGrammarAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(text);
        public Task<string> ChangeToneAsync(string text, string tone, CancellationToken cancellationToken = default) => Task.FromResult(text);
        public Task<AiProbeResult> ProbeAsync(CancellationToken cancellationToken = default) => Task.FromResult(AiProbeResult.Ok("ok"));
        public Task WarmupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

public class HexColorTests
{
    [Theory]
    [InlineData("#0A3BA6", 255, 10, 59, 166)]
    [InlineData("0A3BA6", 255, 10, 59, 166)]
    [InlineData("#0a3ba6", 255, 10, 59, 166)]
    [InlineData("0a3ba6", 255, 10, 59, 166)]
    [InlineData("#FF0A3BA6", 255, 10, 59, 166)]
    [InlineData("ff0a3ba6", 255, 10, 59, 166)]
    [InlineData("#800A3BA6", 128, 10, 59, 166)]
    [InlineData("800A3BA6", 128, 10, 59, 166)]
    public void Parse_AcceptsRgbAndArgb(string hex, byte a, byte r, byte g, byte b)
    {
        var color = HexColor.Parse(hex);
        Assert.Equal(a, color.A);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
        Assert.True(HexColor.TryParse(hex, out var parsed));
        Assert.Equal(color, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("123")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    [InlineData("not-a-color")]
    public void InvalidInput_DoesNotBecomeBlackSilentlyInDebug(string hex)
    {
        Assert.False(HexColor.TryParse(hex, out _));
#if DEBUG
        Assert.Throws<FormatException>(() => HexColor.Parse(hex));
#else
        var fallback = HexColor.Parse(hex);
        Assert.Equal((byte)255, fallback.A);
        Assert.Equal((byte)0, fallback.R);
        Assert.Equal((byte)0, fallback.G);
        Assert.Equal((byte)0, fallback.B);
#endif
    }
}
