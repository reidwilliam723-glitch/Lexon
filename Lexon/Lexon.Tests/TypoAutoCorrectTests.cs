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
}
