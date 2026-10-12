using Lexon.Input;
using Xunit;

namespace Lexon.Tests;

public class SuggestionInsertionTests
{
    [Fact]
    public void GetReplacement_MatchingPrefix_InsertsOnlySuffix()
    {
        var (deleteCount, insertText) = SuggestionInsertion.GetReplacement("hel", "hello");

        Assert.Equal(0, deleteCount);
        Assert.Equal("lo", insertText);
    }

    [Fact]
    public void GetReplacement_MatchingPrefixIgnoresCase_InsertsSuffix()
    {
        var (deleteCount, insertText) = SuggestionInsertion.GetReplacement("Hel", "hello");

        Assert.Equal(0, deleteCount);
        Assert.Equal("lo", insertText);
    }

    [Fact]
    public void GetReplacement_UnrelatedSuggestion_ReplacesCurrentWord()
    {
        var (deleteCount, insertText) = SuggestionInsertion.GetReplacement("hel", "world");

        Assert.Equal(3, deleteCount);
        Assert.Equal("world", insertText);
    }

    [Fact]
    public void GetReplacement_Misspelling_DeletesWrongTailAndInsertsCorrection()
    {
        var (deleteCount, insertText) = SuggestionInsertion.GetReplacement("helo", "hello");

        Assert.Equal(1, deleteCount);
        Assert.Equal("lo", insertText);
    }

    [Fact]
    public void GetReplacement_TransposedMisspelling_ReplacesFromDivergence()
    {
        var (deleteCount, insertText) = SuggestionInsertion.GetReplacement("teh", "the");

        Assert.Equal(2, deleteCount);
        Assert.Equal("he", insertText);
    }

    [Fact]
    public void GetReplacement_ReceiveMisspelling_ReplacesFromDivergence()
    {
        var (deleteCount, insertText) = SuggestionInsertion.GetReplacement("recieve", "receive");

        Assert.Equal(4, deleteCount);
        Assert.Equal("eive", insertText);
    }

    [Fact]
    public void GetReplacement_EmptyCurrentWord_InsertsFullSuggestion()
    {
        var (deleteCount, insertText) = SuggestionInsertion.GetReplacement("", "hello");

        Assert.Equal(0, deleteCount);
        Assert.Equal("hello", insertText);
    }

    [Fact]
    public void GetReplacement_EmptySuggestion_InsertsNothing()
    {
        var (deleteCount, insertText) = SuggestionInsertion.GetReplacement("hel", "");

        Assert.Equal(0, deleteCount);
        Assert.Equal("", insertText);
    }

    [Fact]
    public void ResolvePrefix_UsesTypedWordWhenLiveContextIsEmpty()
    {
        var prefix = SuggestionInsertion.ResolvePrefix("hello", "", "hel", "ignored title");

        Assert.Equal("hel", prefix);
    }

    [Fact]
    public void ResolvePrefix_PicksLongestMatchingCandidate()
    {
        var prefix = SuggestionInsertion.ResolvePrefix("hello", "he", "hel");

        Assert.Equal("hel", prefix);
    }

    [Fact]
    public void ResolvePrefix_IgnoresUnrelatedWindowText()
    {
        var prefix = SuggestionInsertion.ResolvePrefix("hello", "Notepad", "hel");

        Assert.Equal("hel", prefix);
    }

    [Fact]
    public void ResolvePrefix_RecoversTokenFromTextBeforeCaret()
    {
        var prefix = SuggestionInsertion.ResolvePrefix("hello", "", "please hel");

        Assert.Equal("hel", prefix);
    }

    [Fact]
    public void CurrentToken_EmptyWhenTextEndsWithSpace()
    {
        Assert.Equal(string.Empty, SuggestionInsertion.CurrentToken("hello "));
        Assert.Equal("hel", SuggestionInsertion.CurrentToken("please hel"));
    }

    [Fact]
    public void TryReplaceTrailingPhrase_HeAre_OnlyDeletesAre()
    {
        Assert.True(SuggestionInsertion.TryReplaceTrailingPhrase(
            "he are", "he is", "I think he are", out var deleteCount, out var insertText));
        Assert.Equal(3, deleteCount);
        Assert.Equal("is ", insertText);
    }

    [Fact]
    public void TryReplaceTrailingPhrase_TrailingSpace_DeletesSpaceWithTail()
    {
        Assert.True(SuggestionInsertion.TryReplaceTrailingPhrase(
            "he are", "he is", "he are ", out var deleteCount, out var insertText));
        Assert.Equal(4, deleteCount);
        Assert.Equal("is ", insertText);
    }

    [Fact]
    public void ResolveInProgressWord_Misspelling_UsesTypedToken()
    {
        var word = SuggestionInsertion.ResolveInProgressWord(
            "hello",
            "please helo",
            "please helo",
            "helo");

        Assert.Equal("helo", word);
    }

    [Fact]
    public void ResolveInProgressWord_MatchingPrefix_StillPrefersLongestMatch()
    {
        var word = SuggestionInsertion.ResolveInProgressWord(
            "hello",
            "please hel",
            "please hel",
            "he");

        Assert.Equal("hel", word);
    }

    [Theory]
    [InlineData("teh ", "teh")]
    [InlineData("hello.", "hello")]
    [InlineData("please send ", "send")]
    [InlineData("midword", "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    public void LastCompletedWord_OnlyAfterASeparator(string text, string expected)
    {
        Assert.Equal(expected, SuggestionInsertion.LastCompletedWord(text));
    }

    [Fact]
    public void SuggestionPrefix_UsesLettersBeforeTheCaret_NotTheLastTypedLetter()
    {
        var prefix = SuggestionInsertion.SuggestionPrefix("o", "hello", 2);

        Assert.Equal("he", prefix);
    }

    [Fact]
    public void SuggestionPrefix_WithoutEditorText_UsesTheTypedBuffer()
    {
        Assert.Equal("hel", SuggestionInsertion.SuggestionPrefix("hel", string.Empty, 0));
    }

    [Fact]
    public void GetWordReplacement_ReplacesTheLettersAfterTheCaretAsWell()
    {
        var (backward, forward, insert) = SuggestionInsertion.GetWordReplacement("he", "llo", "hello");

        Assert.Equal(0, backward);
        Assert.Equal(3, forward);
        Assert.Equal("llo", insert);
    }
}
