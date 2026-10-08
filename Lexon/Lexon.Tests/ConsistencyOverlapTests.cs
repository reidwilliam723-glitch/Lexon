using Lexon.Core.Grammar;
using Xunit;

namespace Lexon.Tests;

public class ConsistencyOverlapTests
{
    [Theory]
    [InlineData("Please recieve it. We did not Recieve it.", "recieve", "receive")]
    [InlineData("It occured once. Then it Occured again.", "occured", "occurred")]
    public void MergedChecks_KeepOneCorrectedSpellingPerSpan(string text, string typo, string fix)
    {
        var rules = RuleBasedGrammarChecker.Find(text).ToList();
        var merged = GrammarMatch.PreferRuleBased(rules, ConsistencyChecker.Find(text));

        Assert.All(merged.GroupBy(m => m.Start), group => Assert.Single(group));
        Assert.DoesNotContain(merged, m => m.Category == GrammarRuleCategory.Consistency);
        Assert.Contains(merged, m =>
            m.Original.Equals(typo, StringComparison.OrdinalIgnoreCase)
            && m.Replacement.Equals(fix, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(merged, m => m.Replacement.Equals(typo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TrailingMatches_DropsConsistencyThatOverlapsATypo()
    {
        const string text = "Please recieve it. We did not Recieve";
        var trailing = GrammarSuggestionMapper.TrailingMatches(text, includeConsistency: true);

        Assert.Contains(trailing, m =>
            m.Original.Equals("Recieve", StringComparison.Ordinal)
            && m.Replacement.Equals("Receive", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(trailing, m =>
            m.Category == GrammarRuleCategory.Consistency
            && m.Original.Contains("ecieve", StringComparison.OrdinalIgnoreCase));
    }
}
