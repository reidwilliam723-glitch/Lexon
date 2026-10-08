using Lexon.Core.Grammar;
using Xunit;

namespace Lexon.Tests;

public class ConsistencyCheckerTests
{
    [Fact]
    public void FlagsCaseVariantsOfSameName()
    {
        var matches = ConsistencyChecker.Find("We use GitHub today. Later we opened Github again.");
        Assert.Contains(matches, m =>
            m.Category == GrammarRuleCategory.Consistency
            && m.Original.Equals("Github", StringComparison.Ordinal)
            && m.Replacement.Equals("GitHub", StringComparison.Ordinal));
    }

    [Fact]
    public void FlagsHyphenVariants()
    {
        var matches = ConsistencyChecker.Find("Enable Wi-Fi now. Then disable WiFi later.");
        Assert.Contains(matches, m =>
            m.Category == GrammarRuleCategory.Consistency
            && (m.Original.Contains("WiFi", StringComparison.OrdinalIgnoreCase)
                || m.Original.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void FlagsRegionalSpellingVariants()
    {
        var matches = ConsistencyChecker.Find("Pick a color. Then change the colour again.");
        Assert.Contains(matches, m =>
            m.Category == GrammarRuleCategory.Consistency
            && m.Message.Contains("Inconsistent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LeavesConsistentSpellingAlone()
    {
        var matches = ConsistencyChecker.Find("GitHub and GitHub again look fine.");
        Assert.DoesNotContain(matches, m => m.Category == GrammarRuleCategory.Consistency);
    }

    [Theory]
    [InlineData("Meeting notes are ready. The meeting starts at 3.")]
    [InlineData("The report is ready. Report shows growth this quarter.")]
    [InlineData("He wrote: \"Meeting notes are ready.\" Later the meeting started.")]
    [InlineData("She said (Thanks for the update) and then thanks again.")]
    [InlineData("- Report due friday\n- the report is late")]
    [InlineData("\u201CInvoice sent,\u201D she said. Please check the invoice.")]
    public void DoesNotFlagSentenceOrQuoteOpeners(string text)
    {
        Assert.Empty(ConsistencyChecker.Find(text));
    }

    [Theory]
    [InlineData("We use the e-mail system. Please email me tomorrow.", "email", "e-mail")]
    [InlineData("Email is fast. We use e-mail daily and e-mail again.", "Email", "E-mail")]
    [InlineData("I love the color of the sky. The colour is great.", "colour", "color")]
    [InlineData("We love LexFlow. Try Lexflow today.", "Lexflow", "LexFlow")]
    [InlineData("I bought an iPhone. The Iphone is great.", "Iphone", "iPhone")]
    public void FlagsRealSpellingVariants(string text, string original, string replacement)
    {
        var matches = ConsistencyChecker.Find(text);
        Assert.Contains(matches, m =>
            m.Original.Equals(original, StringComparison.Ordinal)
            && m.Replacement.Equals(replacement, StringComparison.Ordinal));
    }
}
