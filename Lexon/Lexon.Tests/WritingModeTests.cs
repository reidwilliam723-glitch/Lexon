using Lexon.Service;
using Xunit;

namespace Lexon.Tests;

public class WritingModeTests
{
    [Fact]
    public void Options_IncludePlainFormalAndConcise()
    {
        Assert.Contains(SelectionRewriteService.PlainLanguageOption, SelectionRewriteService.Options);
        Assert.Contains(SelectionRewriteService.FormalOption, SelectionRewriteService.Options);
        Assert.Contains(SelectionRewriteService.ConciseOption, SelectionRewriteService.Options);
    }

    [Fact]
    public void InstructionFor_PlainLanguage_MentionsEverydayWords()
    {
        var instruction = SelectionRewriteService.InstructionFor(SelectionRewriteService.PlainLanguageOption);
        Assert.Contains("plain language", instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("everyday", instruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OrderOptions_DefaultIsFirstUntilAModeIsUsed()
    {
        var fresh = SelectionRewriteService.OrderOptions(lastUsed: null, defaultMode: "more concise");
        Assert.Equal(SelectionRewriteService.ConciseOption, fresh[0]);

        var used = SelectionRewriteService.OrderOptions(lastUsed: "More formal", defaultMode: "More concise");
        Assert.Equal(SelectionRewriteService.FormalOption, used[0]);

        var cased = SelectionRewriteService.OrderOptions(lastUsed: "  ", defaultMode: "PLAIN LANGUAGE");
        Assert.Equal(SelectionRewriteService.PlainLanguageOption, cased[0]);
    }

    [Theory]
    [InlineData(null, "Plain language")]
    [InlineData("", "Plain language")]
    [InlineData("more concise", "More concise")]
    [InlineData("More formal", "More formal")]
    [InlineData("unknown", "Plain language")]
    public void NormalizeDefaultMode_MapsKnownLabels(string? input, string expected)
    {
        Assert.Equal(expected, SelectionRewriteService.NormalizeDefaultMode(input));
    }
}
