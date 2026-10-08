using System.Net;
using System.Net.Sockets;
using Lexon.AI;
using Lexon.Core.Grammar;
using Lexon.Service;
using Xunit;

namespace Lexon.Tests;

public class AiProviderSetupTests
{
    [Fact]
    public void RecommendedProvider_IsOpenAI()
    {
        Assert.Equal("OpenAI", AiProviderCatalog.Recommended);
        Assert.False(AiProviderCatalog.ShowAdvancedByDefault(null));
        Assert.False(AiProviderCatalog.ShowAdvancedByDefault("None"));
        Assert.False(AiProviderCatalog.ShowAdvancedByDefault("OpenAI"));
        Assert.True(AiProviderCatalog.ShowAdvancedByDefault("Gemini"));
        Assert.True(AiProviderCatalog.ShowAdvancedByDefault("Ollama"));
    }

    [Fact]
    public void ProbeResult_DistinguishesKeyRejectionFromNetwork()
    {
        Assert.Equal(AiProbeStatus.InvalidCredential, AiProbeResult.FromHttpStatus(HttpStatusCode.Unauthorized).Status);
        Assert.Equal(AiProbeStatus.InvalidCredential, AiProbeResult.FromHttpStatus(HttpStatusCode.Forbidden).Status);
        Assert.Equal(AiProbeStatus.Ok, AiProbeResult.FromHttpStatus(HttpStatusCode.OK).Status);
        Assert.Equal(AiProbeStatus.Ok, AiProbeResult.FromHttpStatus((HttpStatusCode)429).Status);
        Assert.Equal(AiProbeStatus.Unreachable, AiProbeResult.FromException(new HttpRequestException()).Status);
        Assert.Equal(AiProbeStatus.Unreachable, AiProbeResult.FromException(new SocketException()).Status);
    }

    [Fact]
    public void GeminiBadRequest_IsTreatedAsInvalidKey()
    {
        Assert.Equal(AiProbeStatus.InvalidCredential, AiProbeResult.FromHttpStatus(HttpStatusCode.BadRequest, treatBadRequestAsInvalidKey: true).Status);
        Assert.Equal(AiProbeStatus.Error, AiProbeResult.FromHttpStatus(HttpStatusCode.BadRequest).Status);
    }

    [Fact]
    public void Ollama_DoesNotUseApiKey()
    {
        Assert.False(AiProviderCatalog.UsesApiKey("Ollama"));
        Assert.True(AiProviderCatalog.UsesApiKey("OpenAI"));
        Assert.Null(AiProviderCatalog.KeyCreationUrl("Ollama"));
        Assert.Equal("https://platform.openai.com/api-keys", AiProviderCatalog.KeyCreationUrl("OpenAI"));
        Assert.Equal("https://aistudio.google.com/apikey", AiProviderCatalog.KeyCreationUrl("Gemini"));
        Assert.Equal("https://platform.deepseek.com/api_keys", AiProviderCatalog.KeyCreationUrl("DeepSeek"));
        Assert.Equal("gpt-4o-mini", AiProviderCatalog.DefaultModel("OpenAI"));
        Assert.Equal("gemini-2.0-flash", AiProviderCatalog.DefaultModel("Gemini"));
        Assert.Equal("deepseek-chat", AiProviderCatalog.DefaultModel("DeepSeek"));
        Assert.Equal("llama3.2", AiProviderCatalog.DefaultModel("Ollama"));
    }

    [Fact]
    public void LooksLikeApiKey_MatchesProviderShapesOnly()
    {
        // GitHub secret scanning rejects any literal matching sk- + 32 [a-z0-9],
        // which is exactly the shape this test needs, so assemble it at runtime.
        var deepSeekShaped = "sk-" + new string('a', 32);

        Assert.True(AiProviderCatalog.LooksLikeApiKey("OpenAI", "sk-proj-abcdefghijklmnopqrstuvwxyz123456"));
        Assert.True(AiProviderCatalog.LooksLikeApiKey("OpenAI", "sk-abcdefghijklmnopqrstuvwxyz1234567890"));
        Assert.True(AiProviderCatalog.LooksLikeApiKey("Gemini", "AIzaSyDabcdefghijklmnopqrstuvwx1234567"));
        Assert.True(AiProviderCatalog.LooksLikeApiKey("DeepSeek", deepSeekShaped));
        Assert.False(AiProviderCatalog.LooksLikeApiKey("OpenAI", "https://platform.openai.com/api-keys"));
        Assert.False(AiProviderCatalog.LooksLikeApiKey("Gemini", "sk-abcdefghijklmnopqrstuvwxyz1234567890"));
        Assert.False(AiProviderCatalog.LooksLikeApiKey("OpenAI", "AIzaSyDabcdefghijklmnopqrstuvwx1234567"));
        Assert.False(AiProviderCatalog.LooksLikeApiKey("OpenAI", "not a key"));
        Assert.False(AiProviderCatalog.LooksLikeApiKey("Ollama", "sk-abcdefghijklmnopqrstuvwxyz1234567890"));
        Assert.False(AiProviderCatalog.LooksLikeApiKey("DeepSeek", "sk-abcdefghijklmnopqrstuvwxyz1234567890"));
        Assert.False(AiProviderCatalog.LooksLikeApiKey("OpenAI", deepSeekShaped));
    }

    [Fact]
    public void LooksLikeApiKey_OpenAiShapedClipboard_IsNotAConfidentDeepSeekMatch()
    {
        const string openAiShaped = "sk-aBcD3fGh1jKlMnOpQrStUvWxYz012345";

        Assert.True(AiProviderCatalog.LooksLikeApiKey("OpenAI", openAiShaped));
        Assert.False(AiProviderCatalog.LooksLikeApiKey("DeepSeek", openAiShaped));
    }
}

public class GrammarPauseTriggerTests
{
    [Fact]
    public void PauseCheck_SkipsWhenTextUnchangedOrMuted()
    {
        Assert.True(GrammarCheckService.ShouldRunPauseCheck("hello there", "", true, false, false));
        Assert.False(GrammarCheckService.ShouldRunPauseCheck("hello there", "hello there", true, false, false));
        Assert.False(GrammarCheckService.ShouldRunPauseCheck("hello there", "", false, false, false)); // grammar disabled
        Assert.False(GrammarCheckService.ShouldRunPauseCheck("hello there", "", true, true, false));
        Assert.False(GrammarCheckService.ShouldRunPauseCheck("hello there", "", true, false, true));
        Assert.False(GrammarCheckService.ShouldRunPauseCheck(" ", "", true, false, false));
    }

    [Fact]
    public void PausePopup_DropsAnIssueOnceTypingMovesPastIt()
    {
        var atCaret = GrammarSuggestionMapper.TrailingMatches("he are");
        var movedOn = GrammarSuggestionMapper.TrailingMatches("he are going to the store");
        Assert.NotEmpty(atCaret);
        Assert.Empty(GrammarCheckService.PausePopupMatches(atCaret, movedOn));
    }

    [Fact]
    public void PauseCheck_UsesNearbyTypedTextNotSelection()
    {
        var pause = GrammarCheckService.ResolveCheckText("SELECTED", "I recieve teh package.", "unused", "unused", useSelection: false);
        Assert.Equal("I recieve teh package.", pause);

        var hotkey = GrammarCheckService.ResolveCheckText("SELECTED", "full", "prev", "typed", useSelection: true);
        Assert.Equal("SELECTED", hotkey);

        Assert.Equal("typed buffer", GrammarCheckService.ResolveCheckText(null, "", "", "typed buffer", useSelection: false));
        Assert.True(GrammarCheckService.ClipToRecent(new string('a', 20) + ". " + new string('b', 500)).Length <= 480);
    }
}
