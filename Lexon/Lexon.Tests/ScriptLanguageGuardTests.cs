using Lexon.Core.Grammar;
using Xunit;

namespace Lexon.Tests;

public class ScriptLanguageGuardTests
{
    [Theory]
    [InlineData("café")]
    [InlineData("naïve")]
    [InlineData("привет")]
    [InlineData("שלום")]
    [InlineData("日本語")]
    public void SkipsWordWithNonAsciiOrNonLatinLetters(string word)
    {
        Assert.True(ScriptLanguageGuard.ShouldSkipSpelling(word));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("teh")]
    [InlineData("don't")]
    [InlineData("")]
    public void DoesNotSkipAsciiLatinWords(string word)
    {
        Assert.False(ScriptLanguageGuard.ShouldSkipSpelling(word));
    }

    [Fact]
    public void SkipsWhenSurroundingTextIsPredominantlyNonLatin()
    {
        const string surrounding = "שלום עולם זה טקסט בעברית עם teh בתוכו";
        Assert.True(ScriptLanguageGuard.ShouldSkipSpelling("teh", surrounding));
    }

    [Fact]
    public void DoesNotSkipAsciiWordInMostlyLatinSurrounding()
    {
        Assert.False(ScriptLanguageGuard.ShouldSkipSpelling("teh", "I typed teh by mistake today."));
    }

    [Fact]
    public void SurroundingThresholdRequiresOverFortyPercentNonLatin()
    {
        // 1 non-Latin letter out of 5 letters = 20% — below threshold
        Assert.False(ScriptLanguageGuard.ShouldSkipSpelling("teh", "ab cд"));
        // 3 non-Latin out of 5 letters = 60% — above threshold
        Assert.True(ScriptLanguageGuard.ShouldSkipSpelling("teh", "a бвгд"));
    }

    [Fact]
    public void SurroundingScan_UsesOnlyTheLast600Characters()
    {
        var early = new string('я', 1000);
        var recent = new string('a', 700);
        Assert.False(ScriptLanguageGuard.ShouldSkipSpelling("teh", early + recent));
    }
}
