using Lexon.Core.Grammar;
using Lexon.Core.Models;
using Xunit;

namespace Lexon.Tests;

public class SpacingNormalizerTests
{
    [Theory]
    [InlineData("Hello,world", "Hello, world")]
    [InlineData("Yes!Ok", "Yes! Ok")]
    [InlineData("Wait;please", "Wait; please")]
    [InlineData("done.Next", "done. Next")]
    public void InsertsSpaceAfterPunctuation(string input, string expected)
    {
        Assert.True(SpacingNormalizer.TryGetTrailingEdit(input, out var start, out var length, out var replacement));
        Assert.Equal(expected, input.Remove(start, length).Insert(start, replacement));
    }

    [Theory]
    [InlineData("3.14")]
    [InlineData("10:30")]
    [InlineData("https://x.com")]
    [InlineData("a@b.c")]
    [InlineData("v1.2.3")]
    [InlineData("file.txt")]
    [InlineData("1,000")]
    [InlineData("Wait...")]
    [InlineData("Hello!!")]
    public void LeavesTechnicalTextAlone(string input)
    {
        Assert.False(SpacingNormalizer.TryGetTrailingEdit(input, out _, out _, out _));
    }

    [Fact]
    public void StripsSpaceBeforePunctuation()
    {
        Assert.True(SpacingNormalizer.TryGetTrailingEdit("Hello ,", out var start, out var length, out var replacement));
        Assert.Equal("Hello,", "Hello ,".Remove(start, length).Insert(start, replacement));
    }

    [Fact]
    public void CollapsesThreeSpacesButKeepsTwoAfterSentence()
    {
        Assert.True(SpacingNormalizer.TryGetTrailingEdit("Hello   ", out var start, out var length, out var replacement));
        Assert.Equal("Hello ", "Hello   ".Remove(start, length).Insert(start, replacement));

        Assert.True(SpacingNormalizer.TryGetTrailingEdit("Done.   ", out start, out length, out replacement));
        Assert.Equal("Done.  ", "Done.   ".Remove(start, length).Insert(start, replacement));
    }

    [Fact]
    public void DoesNotTouchLeadingIndent()
    {
        Assert.False(SpacingNormalizer.TryGetTrailingEdit("   ", out _, out _, out _));
    }

    [Fact]
    public void CodeAppsAreDetected()
    {
        Assert.True(SpacingNormalizer.IsCodeApp("code.exe"));
        Assert.True(SpacingNormalizer.IsCodeApp("cursor.exe"));
        Assert.True(SpacingNormalizer.IsCodeApp("pwsh.exe"));
        Assert.False(SpacingNormalizer.IsCodeApp("notepad.exe"));
        Assert.True(SpacingNormalizer.IsCodeApp(
            "chat.exe",
            new Dictionary<string, AppWritingCategory> { ["chat.exe"] = AppWritingCategory.Code }));
    }

    [Fact]
    public void GetEditToEnd_ReplacesFromMatchToCaret()
    {
        const string text = "Hello,world";
        Assert.True(SpacingNormalizer.TryGetTrailingEdit(text, out var start, out var length, out var replacement));
        var (deleteCount, insertText) = SpacingNormalizer.GetEditToEnd(text, start, length, replacement);
        Assert.Equal(text.Length - start, deleteCount);
        Assert.Equal("Hello, world", text[..start] + insertText);
    }
}

public class GluedWordSplitterTests
{
    [Fact]
    public void SplitsDictionaryPair()
    {
        Assert.True(GluedWordSplitter.TrySplit("thecat", out var left, out var right));
        Assert.Equal("the", left);
        Assert.Equal("cat", right);
    }

    [Fact]
    public void SplitsShortFunctionWord()
    {
        Assert.True(GluedWordSplitter.TrySplit("Iwent", out var left, out var right));
        Assert.Equal("I", left);
        Assert.Equal("went", right);
    }

    [Fact]
    public void DoesNotSplitCamelCase()
    {
        Assert.False(GluedWordSplitter.TrySplit("GitHub", out _, out _));
    }

    [Fact]
    public void DoesNotSplitWholeDictionaryWord()
    {
        Assert.False(GluedWordSplitter.TrySplit("together", out _, out _));
    }

    [Fact]
    public void Find_SuggestsGluedToken()
    {
        var matches = GluedWordSplitter.Find("thecat sat");
        Assert.Contains(matches, m => m.Replacement == "the cat");
    }
}
