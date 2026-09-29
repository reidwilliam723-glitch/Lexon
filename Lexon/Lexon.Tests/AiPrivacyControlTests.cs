using Lexon.AI.Interfaces;
using Lexon.Core;
using Lexon.Core.Interfaces;
using Lexon.Core.Learning;
using Lexon.Core.Models;
using Lexon.Core.Pipeline;
using Lexon.Input;
using Lexon.Input.Interfaces;
using Lexon.Overlay.Interfaces;
using Lexon.Privacy;
using Lexon.Profiles;
using Lexon.Service;
using Moq;
using Xunit;

namespace Lexon.Tests;

public class AiProbeGateTests
{
    [Fact]
    public void Invalidate_MakesTicketStale()
    {
        using var gate = new AiProbeGate();
        var ticket = gate.Begin();
        Assert.True(gate.IsCurrent(ticket));

        gate.Invalidate();
        Assert.False(gate.IsCurrent(ticket));
    }

    [Fact]
    public void SecondBegin_MakesFirstTicketStale()
    {
        using var gate = new AiProbeGate();
        var first = gate.Begin();
        var second = gate.Begin();
        Assert.False(gate.IsCurrent(first));
        Assert.True(gate.IsCurrent(second));
    }

    [Fact]
    public void Invalidate_CancelsToken()
    {
        using var gate = new AiProbeGate();
        _ = gate.Begin();
        var token = gate.Token;
        gate.Invalidate();
        Assert.True(token.IsCancellationRequested);
        Assert.False(gate.Token.IsCancellationRequested);
    }
}

public class AiProviderGuardTests
{
    [Fact]
    public void LocalMode_BlocksInstall()
    {
        Assert.False(AiProviderGuard.ShouldInstall(localMode: true, providerRequested: true));
        Assert.True(AiProviderGuard.ShouldInstall(localMode: false, providerRequested: true));
        Assert.False(AiProviderGuard.ShouldInstall(localMode: false, providerRequested: false));
    }

    [Fact]
    public async Task ApplyAiProvider_LocalMode_RemovesAlreadyRegisteredProvider()
    {
        var guard = new Mock<IPrivacyGuard>();
        guard.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);
        guard.Setup(g => g.IsApplicationBlocked(It.IsAny<string>())).Returns(false);
        guard.Setup(g => g.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);

        var pipeline = new SuggestionPipeline(guard.Object);
        var policy = new AiAccessPolicy();
        policy.Update(true, suggestionsWhileTyping: true, rewriteOnRequest: true, prefetchOnSelection: false);

        var profile = new Profile { Id = "t" };
        profile.SetSetting("LocalMode", true);
        var composition = new LexonServiceComposer.CompositionResult
        {
            SuggestionPipeline = pipeline,
            Profile = profile,
            AiAccessPolicy = policy
        };

        var provider = new Mock<IAIProvider>();
        provider.Setup(p => p.Name).Returns("OpenAI");
        provider.Setup(p => p.IsFastPath).Returns(false);
        provider.Setup(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Suggestion { Text = "from-cloud", Score = 1 }]);
        pipeline.AddProvider(provider.Object);
        pipeline.SetAccessPolicy(policy);

        LexonServiceComposer.ApplyAiProvider(composition, provider.Object);

        Assert.Null(composition.AIProvider);
        Assert.True(composition.AiAccessPolicy.LocalOnly);
        var result = await pipeline.GetSupplementalSuggestionsAsync(new TextContext { ApplicationName = "notepad" });
        Assert.DoesNotContain(result, s => s.Text == "from-cloud");
        provider.Verify(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ApplyAiProvider_DoesNotResetLocalOnlyFromStaleProfile()
    {
        var pipeline = new SuggestionPipeline(new Mock<IPrivacyGuard>().Object);
        var policy = new AiAccessPolicy();
        policy.Update(true, false, true, false);
        var profile = new Profile { Id = "t" };
        profile.SetSetting("LocalMode", false);
        var composition = new LexonServiceComposer.CompositionResult
        {
            SuggestionPipeline = pipeline,
            Profile = profile,
            AiAccessPolicy = policy
        };

        LexonServiceComposer.ApplyAiProvider(composition, null);

        Assert.True(composition.AiAccessPolicy.LocalOnly);
    }
}

public class SuggestionPipelineAiEpochTests
{
    [Fact]
    public async Task LateSlowResults_AreDroppedAfterEpochBump()
    {
        var privacy = new Mock<IPrivacyGuard>();
        privacy.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);
        privacy.Setup(g => g.IsApplicationBlocked(It.IsAny<string>())).Returns(false);
        privacy.Setup(g => g.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);

        var policy = new AiAccessPolicy();
        policy.Update(false, suggestionsWhileTyping: true, rewriteOnRequest: true, prefetchOnSelection: false);

        var pipeline = new SuggestionPipeline(privacy.Object);
        pipeline.SetAccessPolicy(policy);

        var tcs = new TaskCompletionSource<IEnumerable<Suggestion>>();
        var slow = new Mock<ISuggestionProvider>();
        slow.Setup(p => p.Name).Returns("OpenAI");
        slow.Setup(p => p.IsFastPath).Returns(false);
        slow.Setup(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        pipeline.AddProvider(slow.Object);

        var collect = pipeline.GetSupplementalSuggestionsAsync(new TextContext { ApplicationName = "notepad", CurrentWord = "hel" });
        pipeline.BumpAiEpoch();
        pipeline.RemoveProvider("OpenAI");
        tcs.SetResult([new Suggestion { Text = "hello-from-cloud", Score = 1 }]);

        var result = await collect;
        Assert.DoesNotContain(result, s => s.Text == "hello-from-cloud");
    }

    [Fact]
    public async Task TypingSettingOff_DoesNotCallCloudProvider()
    {
        var privacy = new Mock<IPrivacyGuard>();
        privacy.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);
        privacy.Setup(g => g.IsApplicationBlocked(It.IsAny<string>())).Returns(false);
        privacy.Setup(g => g.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);
        var policy = new AiAccessPolicy();
        policy.Update(false, suggestionsWhileTyping: false, rewriteOnRequest: true, prefetchOnSelection: false);

        var pipeline = new SuggestionPipeline(privacy.Object);
        pipeline.SetAccessPolicy(policy);
        var slow = new Mock<ISuggestionProvider>();
        slow.Setup(p => p.Name).Returns("OpenAI");
        slow.Setup(p => p.IsFastPath).Returns(false);
        pipeline.AddProvider(slow.Object);

        await pipeline.GetSupplementalSuggestionsAsync(new TextContext { ApplicationName = "notepad" });
        slow.Verify(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TypingSettingOff_StillCallsLocalOllamaAndPlugins()
    {
        var privacy = new Mock<IPrivacyGuard>();
        privacy.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);
        privacy.Setup(g => g.IsApplicationBlocked(It.IsAny<string>())).Returns(false);
        privacy.Setup(g => g.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);
        var policy = new AiAccessPolicy();
        policy.Update(false, suggestionsWhileTyping: false, rewriteOnRequest: true, prefetchOnSelection: false);

        var pipeline = new SuggestionPipeline(privacy.Object);
        pipeline.SetAccessPolicy(policy);

        var ollama = new Mock<ISuggestionProvider>();
        ollama.Setup(p => p.Name).Returns("Ollama");
        ollama.Setup(p => p.IsFastPath).Returns(false);
        ollama.Setup(p => p.NetworkEndpoint).Returns("http://127.0.0.1:11434");
        ollama.Setup(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Suggestion { Text = "local-ollama", Score = 1 }]);

        var plugin = new Mock<ISuggestionProvider>();
        plugin.Setup(p => p.Name).Returns("AcmePlugin");
        plugin.Setup(p => p.IsFastPath).Returns(false);
        plugin.Setup(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Suggestion { Text = "from-plugin", Score = 1 }]);

        pipeline.AddProvider(ollama.Object);
        pipeline.AddProvider(plugin.Object);

        var result = (await pipeline.GetSupplementalSuggestionsAsync(new TextContext { ApplicationName = "notepad" })).ToList();
        ollama.Verify(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()), Times.Once);
        plugin.Verify(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(result, s => s.Text == "local-ollama");
        Assert.Contains(result, s => s.Text == "from-plugin");
    }

    [Fact]
    public async Task TypingSettingOff_BlocksRemoteOllama()
    {
        var privacy = new Mock<IPrivacyGuard>();
        privacy.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);
        privacy.Setup(g => g.IsApplicationBlocked(It.IsAny<string>())).Returns(false);
        privacy.Setup(g => g.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);
        var policy = new AiAccessPolicy();
        policy.Update(false, suggestionsWhileTyping: false, rewriteOnRequest: true, prefetchOnSelection: false);

        var pipeline = new SuggestionPipeline(privacy.Object);
        pipeline.SetAccessPolicy(policy);
        var ollama = new Mock<ISuggestionProvider>();
        ollama.Setup(p => p.Name).Returns("Ollama");
        ollama.Setup(p => p.IsFastPath).Returns(false);
        ollama.Setup(p => p.NetworkEndpoint).Returns("http://10.0.0.8:11434");
        pipeline.AddProvider(ollama.Object);

        await pipeline.GetSupplementalSuggestionsAsync(new TextContext { ApplicationName = "notepad" });
        ollama.Verify(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FastPath_DoesNotCallSlowProviderWhenNoFastProvidersExist()
    {
        var privacy = new Mock<IPrivacyGuard>();
        privacy.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);
        var pipeline = new SuggestionPipeline(privacy.Object);
        var slow = new Mock<ISuggestionProvider>();
        slow.Setup(p => p.Name).Returns("OpenAI");
        slow.Setup(p => p.IsFastPath).Returns(false);
        pipeline.AddProvider(slow.Object);

        var result = await pipeline.GetSuggestionsAsync(new TextContext { ApplicationName = "notepad", CurrentWord = "hel" });
        Assert.Empty(result);
        slow.Verify(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BlockedApp_NeverReachesSlowProvider()
    {
        var privacy = new PrivacyGuard();
        privacy.AddBlockedApplication("secretapp");
        var policy = new AiAccessPolicy();
        policy.Update(false, suggestionsWhileTyping: true, rewriteOnRequest: true, prefetchOnSelection: false);

        var pipeline = new SuggestionPipeline(privacy);
        pipeline.SetAccessPolicy(policy);
        var slow = new Mock<ISuggestionProvider>();
        slow.Setup(p => p.Name).Returns("OpenAI");
        slow.Setup(p => p.IsFastPath).Returns(false);
        pipeline.AddProvider(slow.Object);

        await pipeline.GetSupplementalSuggestionsAsync(new TextContext { ApplicationName = "secretapp", FullText = "hello" });
        slow.Verify(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void UnchangedPolicy_DoesNotBumpEpoch()
    {
        var privacy = new Mock<IPrivacyGuard>();
        var pipeline = new SuggestionPipeline(privacy.Object);
        var policy = new AiAccessPolicy();
        Assert.True(policy.Update(false, true, true, false));
        pipeline.SetAccessPolicy(policy);
        var epoch = pipeline.AiEpoch;

        var changed = policy.Update(false, true, true, false);
        if (changed)
        {
            pipeline.BumpAiEpoch();
        }

        Assert.False(changed);
        Assert.Equal(epoch, pipeline.AiEpoch);
    }

    [Fact]
    public async Task ConcurrentAddRemove_DoesNotThrow()
    {
        var privacy = new Mock<IPrivacyGuard>();
        privacy.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);
        var pipeline = new SuggestionPipeline(privacy.Object);
        var fast = new Mock<ISuggestionProvider>();
        fast.Setup(p => p.Name).Returns("Dict");
        fast.Setup(p => p.IsFastPath).Returns(true);
        fast.Setup(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Suggestion { Text = "a", Score = 1 }]);

        var errors = 0;
        var addRemove = Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                try
                {
                    pipeline.AddProvider(fast.Object);
                    pipeline.RemoveProvider("Dict");
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }
        });

        var read = Task.Run(async () =>
        {
            for (var i = 0; i < 200; i++)
            {
                try
                {
                    await pipeline.GetSuggestionsAsync(new TextContext { CurrentWord = "a" });
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }
        });

        await Task.WhenAll(addRemove, read);
        Assert.Equal(0, errors);
    }
}

public class CloudSuggestLogTests
{
    [Fact]
    public async Task RecordsSuggest_AndCoalescesRepeats()
    {
        var privacy = new Mock<IPrivacyGuard>();
        privacy.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);
        privacy.Setup(g => g.IsApplicationBlocked(It.IsAny<string>())).Returns(false);
        privacy.Setup(g => g.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);
        var policy = new AiAccessPolicy();
        policy.Update(false, true, true, false);
        var log = new CloudAiActivityLog();
        var pipeline = new SuggestionPipeline(privacy.Object);
        pipeline.SetAccessPolicy(policy);
        pipeline.SetActivityLog(log);

        var slow = new Mock<ISuggestionProvider>();
        slow.Setup(p => p.Name).Returns("OpenAI");
        slow.Setup(p => p.IsFastPath).Returns(false);
        slow.Setup(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Suggestion { Text = "hello", Score = 1 }]);
        pipeline.AddProvider(slow.Object);

        var context = new TextContext { ApplicationName = "notepad", CurrentWord = "hel" };
        await pipeline.GetSupplementalSuggestionsAsync(context);
        await pipeline.GetSupplementalSuggestionsAsync(context);

        var suggests = log.Snapshot().Where(e => e.Action == "suggest").ToList();
        Assert.Single(suggests);
        Assert.Equal("OpenAI", suggests[0].Provider);
        Assert.Equal("notepad", suggests[0].ApplicationName);
    }

    [Fact]
    public void TryRecordSuggest_IgnoresOllama()
    {
        var log = new CloudAiActivityLog();
        Assert.False(log.TryRecordSuggest("Ollama", "notepad"));
        Assert.Empty(log.Snapshot());
    }
}

public class AiConnectionStatusTests
{
    [Fact]
    public void Success_IsConnected()
    {
        var status = new AiConnectionStatus();
        status.EnterChecking("OpenAI");
        status.EnterConnected("OpenAI", "gpt-4.1-mini");
        Assert.Equal(AiConnectionState.Connected, status.State);
        Assert.Equal("Active: OpenAI · gpt-4.1-mini", status.ActiveLine);
        Assert.Equal("Connected.", status.Detail);
    }

    [Fact]
    public void Failure_WithInstalledProvider_KeepsIt()
    {
        var status = new AiConnectionStatus();
        status.EnterConnected("OpenAI", "gpt-4.1-mini");
        status.EnterFailed("That key was refused.", installedProvider: "OpenAI");
        Assert.Equal(AiConnectionState.Failed, status.State);
        Assert.Contains("Still using OpenAI (previous key)", status.Detail);
        Assert.StartsWith("Active: OpenAI", status.ActiveLine);
    }

    [Fact]
    public void Failure_WithoutPrevious_TurnsAiOff()
    {
        var status = new AiConnectionStatus();
        status.EnterFailed("That key was refused.");
        Assert.Equal("AI is off", status.ActiveLine);
        Assert.Contains("AI is off until a key works", status.Detail);
    }

    [Fact]
    public void LocalOnlyThenFailedProbe_ReportsAiOffUntilKeyWorks()
    {
        var status = new AiConnectionStatus();
        status.EnterConnected("OpenAI", "gpt-4.1-mini");
        status.EnterLocalOnly();
        status.EnterFailed("That key was refused.");
        Assert.Equal(AiConnectionState.Failed, status.State);
        Assert.Equal("AI is off", status.ActiveLine);
        Assert.Contains("AI is off until a key works", status.Detail);
        Assert.DoesNotContain("Still using", status.Detail);
    }

    [Fact]
    public void LoadWithNoInstalledProvider_NeverReportsConnected()
    {
        var status = new AiConnectionStatus();
        status.SeedFromInstalled(null, "gpt-4.1-mini", keyPresent: true);
        Assert.NotEqual(AiConnectionState.Connected, status.State);
        Assert.Equal(AiConnectionState.KeySaved, status.State);
        Assert.Equal("AI is off", status.ActiveLine);
        Assert.DoesNotContain("Connected", status.Detail);
    }

    [Fact]
    public void LocalOnly_ShowsAiOff()
    {
        var status = new AiConnectionStatus();
        status.EnterConnected("OpenAI", "gpt-4.1-mini");
        status.EnterLocalOnly();
        Assert.Equal(AiConnectionState.LocalOnlyOff, status.State);
        Assert.Equal("AI is off", status.ActiveLine);
        Assert.Contains("No text is sent", status.Detail);
        Assert.Null(status.ConnectedProvider);
    }
}

public class SelectionRewriteProviderSnapshotTests
{
    [Fact]
    public async Task SetProviderNull_DuringAwaitedRewrite_ReturnsOriginalAndDoesNotThrow()
    {
        var tcs = new TaskCompletionSource<string>();
        var ai = new Mock<IAIProvider>();
        ai.Setup(p => p.Name).Returns("OpenAI");
        ai.Setup(p => p.RewriteTextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<string>?>()))
            .Returns(tcs.Task);

        var service = CreateRewriteService(ai.Object);
        var pending = service.AwaitRewriteForTests(tcs.Task, "hello");
        service.SetProvider(null);
        tcs.SetCanceled();

        var result = await pending;
        Assert.Equal("hello", result);
    }

    private static SelectionRewriteService CreateRewriteService(IAIProvider provider)
    {
        var focus = new Mock<IFocusTracker>();
        focus.Setup(f => f.GetCurrentContext()).Returns(new TextContext { ApplicationName = "notepad" });
        focus.Setup(f => f.GetCaretScreenPosition()).Returns((0, 0));
        var injector = new Mock<ITextInjector>();
        var undo = new UndoManager(injector.Object);
        var confirm = new Mock<IEditConfirmation>();
        var menu = new Mock<IActionMenuOverlay>();
        var storage = new Mock<IStorage>();
        storage.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var personalization = new PersonalizationManager(storage.Object, new FeedbackCollector());
        var privacy = new Mock<IPrivacyGuard>();
        privacy.Setup(g => g.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);
        return new SelectionRewriteService(
            provider,
            focus.Object,
            injector.Object,
            undo,
            confirm.Object,
            menu.Object,
            personalization,
            new Profile { Id = "t" },
            privacy.Object);
    }
}
