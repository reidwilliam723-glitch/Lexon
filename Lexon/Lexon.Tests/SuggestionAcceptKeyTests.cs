using Lexon.Core;
using Xunit;

namespace Lexon.Tests;

public class SuggestionAcceptKeyTests
{
    [Theory]
    [InlineData(null, 9, true)]
    [InlineData("Tab", 9, true)]
    [InlineData("tab", 9, true)]
    [InlineData("Enter", 13, true)]
    [InlineData("Right", 39, true)]
    [InlineData("Numbers", 9, false)]
    [InlineData("Numbers", 49, false)]
    [InlineData("Nope", 9, true)]
    public void MatchesTheChosenKey(string? stored, int virtualKey, bool expected)
        => Assert.Equal(expected, SuggestionAcceptKey.Matches(stored, virtualKey, false, false, false));

    [Fact]
    public void ModifiedKeysNeverAccept()
        => Assert.False(SuggestionAcceptKey.Matches("Tab", 9, shift: true, control: false, alt: false));

    [Fact]
    public void SpaceNeverMatches()
    {
        Assert.False(SuggestionAcceptKey.Matches("Tab", 32, false, false, false));
        Assert.False(SuggestionAcceptKey.Matches("Enter", 32, false, false, false));
        Assert.False(SuggestionAcceptKey.Matches("Right", 32, false, false, false));
        Assert.False(SuggestionAcceptKey.Matches("Numbers", 32, false, false, false));
    }
}
