using Lexon.Core.Grammar;
using Xunit;

namespace Lexon.Tests;

public class TypoAutoCorrectTests
{
    [Fact]
    public void KnownTypo_ReturnsShapedCorrection()
    {
        Assert.True(TypoAutoCorrect.TryGetCorrection("teh", enabled: true, learnedWords: [], out var correction));
        Assert.Equal("the", correction);
        Assert.True(TypoAutoCorrect.TryGetCorrection("Teh", enabled: true, learnedWords: [], out correction));
        Assert.Equal("The", correction);
    }

    [Fact]
    public void Disabled_DoesNotCorrect()
    {
        Assert.False(TypoAutoCorrect.TryGetCorrection("teh", enabled: false, learnedWords: [], out _));
    }

    [Fact]
    public void LearnedVocabulary_IsNotAutoCorrected()
    {
        Assert.False(TypoAutoCorrect.TryGetCorrection("teh", enabled: true, learnedWords: ["teh"], out _));
        Assert.False(TypoAutoCorrect.TryGetCorrection("Teh", enabled: true, learnedWords: ["TEH"], out _));
    }

    [Fact]
    public void ProtectedTerminology_IsNotAutoCorrected()
    {
        Assert.False(TypoAutoCorrect.TryGetCorrection(
            "teh",
            enabled: true,
            learnedWords: [],
            out _,
            protectedTerms: ["teh"]));
        Assert.False(TypoAutoCorrect.TryGetCorrection(
            "Teh",
            enabled: true,
            learnedWords: [],
            out _,
            protectedTerms: ["TEH"]));
        Assert.True(TypoAutoCorrect.TryGetCorrection(
            "teh",
            enabled: true,
            learnedWords: [],
            out var correction,
            protectedTerms: ["Lexon"]));
        Assert.Equal("the", correction);
    }

    [Fact]
    public void UnknownWord_IsNotCorrected()
    {
        Assert.False(TypoAutoCorrect.TryGetCorrection("hello", enabled: true, learnedWords: [], out _));
    }

    [Fact]
    public void CodeSwitching_SkipsNonAsciiWord()
    {
        Assert.False(TypoAutoCorrect.TryGetCorrection("café", enabled: true, learnedWords: [], out _, allowCodeSwitching: true));
        Assert.True(TypoAutoCorrect.TryGetCorrection("teh", enabled: true, learnedWords: [], out var correction, allowCodeSwitching: true));
        Assert.Equal("the", correction);
    }

    [Fact]
    public void GetEdit_ReplacesWordAndKeepsSeparator()
    {
        var (deleteCount, insertText) = TypoAutoCorrect.GetEdit("teh", "the", ' ');

        Assert.Equal(4, deleteCount);
        Assert.Equal("the ", insertText);
    }

    [Theory]
    [InlineData("im")]
    [InlineData("ive")]
    [InlineData("whats")]
    [InlineData("thats")]
    [InlineData("ther")]
    [InlineData("wether")]
    [InlineData("lightening")]
    [InlineData("loosing")]
    public void ContextDependent_IsNotAutoCorrected(string word)
    {
        Assert.False(TypoAutoCorrect.TryGetCorrection(word, enabled: true, learnedWords: [], out _));
        Assert.True(CommonMisspellings.TryCorrect(word, out _));
    }

    [Fact]
    public void Contractions_AutoCorrectOnlyWhenEnabled()
    {
        Assert.False(TypoAutoCorrect.TryGetCorrection("im", enabled: true, learnedWords: [], out _));
        Assert.True(TypoAutoCorrect.TryGetCorrection(
            "im", enabled: true, learnedWords: [], out var correction, includeContractions: true));
        Assert.Equal("I'm", correction);
        Assert.False(TypoAutoCorrect.TryGetCorrection(
            "wether", enabled: true, learnedWords: [], out _, includeContractions: true));
    }

    [Fact]
    public void GrammarStillSuggestsContractions()
    {
        var matches = RuleBasedGrammarChecker.Find("im sure whats next");
        Assert.Contains(matches, m => m.Replacement.Equals("I'm", StringComparison.OrdinalIgnoreCase)
            || m.Replacement.Equals("I'm sure", StringComparison.OrdinalIgnoreCase)
            || m.Original.Equals("im", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(matches, m => m.Original.Equals("whats", StringComparison.OrdinalIgnoreCase));
    }
}
