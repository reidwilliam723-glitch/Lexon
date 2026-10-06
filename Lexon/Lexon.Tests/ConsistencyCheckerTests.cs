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
}
