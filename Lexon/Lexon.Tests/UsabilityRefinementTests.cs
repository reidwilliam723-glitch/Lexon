using Lexon.AI;
using Lexon.Core;
using Lexon.Core.Models;
using Lexon.Service;
using Xunit;

namespace Lexon.Tests;

public class TextDiffTests
{
    [Fact]
    public void Compare_MarksReplacement()
    {
        var spans = TextDiff.Compare("the quick fox", "the slow fox");
        Assert.Contains(spans, s => s.Kind == DiffKind.Removed && s.Text.Contains("quick"));
        Assert.Contains(spans, s => s.Kind == DiffKind.Added && s.Text.Contains("slow"));
        Assert.Contains(spans, s => s.Kind == DiffKind.Equal && s.Text.Contains("fox"));
    }
}

public class EditRiskTests
{
    [Fact]
    public void SingleWord_IsLowRisk()
    {
        Assert.True(EditRisk.IsLowRisk("hello", "hi"));
    }

    [Fact]
    public void LongParagraph_IsNotLowRisk()
    {
        var text = string.Join(" ", Enumerable.Repeat("word", 40));
        Assert.False(EditRisk.IsLowRisk(text, text + " extra"));
    }
}

public class SessionEditTrustTests
{
    [Fact]
    public void TrustsAfterThreeAccepts_ResetsOnReject()
    {
        var trust = new SessionEditTrust();
        Assert.False(trust.IsTrusted("app|concise"));
        trust.RecordAccepted("app|concise");
        trust.RecordAccepted("app|concise");
        Assert.False(trust.IsTrusted("app|concise"));
        trust.RecordAccepted("app|concise");
        Assert.True(trust.IsTrusted("app|concise"));
        trust.RecordRejected("app|concise");
        Assert.False(trust.IsTrusted("app|concise"));
    }
}

public class SuggestionContextTests
{
    [Fact]
    public void SuggestionPrompt_IncludesTextAroundCaret()
    {
        var prompt = AiPromptContext.BuildSuggestionPrompt(new TextContext
        {
            PreviousWords = "please send the",
            CurrentWord = "inv",
            FollowingWords = "today thanks"
        });

        Assert.Contains("please send the", prompt);
        Assert.Contains("inv", prompt);
        Assert.Contains("today thanks", prompt);
    }
}

public class SelectionRewriteSpeedTests
{
    [Fact]
    public void TryResolveSelection_PrefersCacheOnRightClick()
    {
        Assert.True(SelectionRewriteService.TryResolveSelection("live", "cached", preferCache: true, out var fromCache));
        Assert.Equal("cached", fromCache);

        Assert.True(SelectionRewriteService.TryResolveSelection("live", "cached", preferCache: false, out var fromLive));
        Assert.Equal("live", fromLive);

        Assert.True(SelectionRewriteService.TryResolveSelection(null, "cached", preferCache: true, out var fallback));
        Assert.Equal("cached", fallback);

        Assert.False(SelectionRewriteService.TryResolveSelection("  ", "", preferCache: false, out _));
    }

    [Fact]
    public void IsUsableRewrite_RejectsEmptyAndUnchanged()
    {
        Assert.False(SelectionRewriteService.IsUsableRewrite(null, "hello"));
        Assert.False(SelectionRewriteService.IsUsableRewrite("  ", "hello"));
        Assert.False(SelectionRewriteService.IsUsableRewrite("hello", "hello"));
        Assert.True(SelectionRewriteService.IsUsableRewrite("Hello.", "hello"));
    }
}

public class AiStreamParserTests
{
    [Fact]
    public void TryOpenAiDelta_ReadsContent()
    {
        var json = """{"choices":[{"delta":{"content":"Hello"}}]}""";
        Assert.Equal("Hello", AiStreamParser.TryOpenAiDelta(json));
    }

    [Fact]
    public async Task ReadSseAsync_AccumulatesDeltas()
    {
        var body = "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\n" +
                   "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}\n\n" +
                   "data: [DONE]\n\n";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
        var reports = new List<string>();
        var progress = new Progress<string>(reports.Add);
        var text = await AiStreamParser.ReadSseAsync(stream, AiStreamParser.TryOpenAiDelta, progress, CancellationToken.None);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void TryGeminiText_SkipsThoughtParts()
    {
        var json = """{"candidates":[{"content":{"parts":[{"thought":true,"text":"thinking"},{"text":"More formal."}]}}]}""";
        Assert.Equal("More formal.", AiStreamParser.TryGeminiText(json));
    }
}
