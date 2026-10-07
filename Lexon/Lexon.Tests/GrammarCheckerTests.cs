using Lexon.Core.Grammar;
using Lexon.Core.Models;
using Xunit;

namespace Lexon.Tests;

public class GrammarCheckerTests
{
    [Fact]
    public void FindsCommonTypos()
    {
        var matches = RuleBasedGrammarChecker.Find("I recieve teh package tommorrow.");
        Assert.Contains(matches, m => m.Original.Equals("recieve", StringComparison.OrdinalIgnoreCase) && m.Replacement.Equals("receive", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(matches, m => m.Original.Equals("teh", StringComparison.OrdinalIgnoreCase) && m.Replacement.Equals("the", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(matches, m => m.Original.Equals("tommorrow", StringComparison.OrdinalIgnoreCase) && m.Replacement.Equals("tomorrow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FixesSubjectVerbAgreement()
    {
        var matches = RuleBasedGrammarChecker.Find("he are ready");
        var match = Assert.Single(matches);
        Assert.Equal("he is", match.Replacement);
        Assert.Equal(GrammarRuleCategory.Agreement, match.Category);
    }

    [Fact]
    public void FixesABeforeVowel()
    {
        var matches = RuleBasedGrammarChecker.Find("a apple");
        Assert.Contains(matches, m => m.Replacement.Equals("an apple", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LeavesAUniversityAlone()
    {
        var matches = RuleBasedGrammarChecker.Find("a university");
        Assert.DoesNotContain(matches, m => m.Original.Contains("university", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RemovesRepeatedWord()
    {
        var matches = RuleBasedGrammarChecker.Find("the the cat");
        Assert.Contains(matches, m => m.Replacement.Equals("the", StringComparison.OrdinalIgnoreCase) && m.Message.Contains("Repeated"));
    }

    [Fact]
    public void CouldOfBecomesCouldHave()
    {
        var matches = RuleBasedGrammarChecker.Find("I could of gone");
        Assert.Contains(matches, m => m.Replacement.Equals("could have", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DistinguishesItsAndIts()
    {
        var possessive = RuleBasedGrammarChecker.Find("the dog licked it's paws");
        Assert.Contains(possessive, m => m.Replacement.Equals("its paws", StringComparison.OrdinalIgnoreCase));

        var contraction = RuleBasedGrammarChecker.Find("its a nice day");
        Assert.Contains(contraction, m => m.Replacement.Equals("it's a", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyReplacesSpan()
    {
        var text = "he are here";
        var match = Assert.Single(RuleBasedGrammarChecker.Find(text));
        Assert.Equal("he is here", RuleBasedGrammarChecker.Apply(text, match));
    }

    [Fact]
    public void LowSensitivitySkipsConfusedWords()
    {
        var matches = RuleBasedGrammarChecker.Find("your a star", "Low");
        Assert.DoesNotContain(matches, m => m.Category == GrammarRuleCategory.ConfusedWord);
    }

    [Fact]
    public async Task TypoSuggestionProviderCorrectsTeh()
    {
        var provider = new TypoSuggestionProvider();
        var suggestions = (await provider.GetSuggestionsAsync(new TextContext { CurrentWord = "teh" })).ToList();
        var suggestion = Assert.Single(suggestions);
        Assert.Equal("the", suggestion.Text);
        Assert.Equal("Spelling", suggestion.Source);
        Assert.True(provider.IsFastPath);

        var titled = (await provider.GetSuggestionsAsync(new TextContext { CurrentWord = "Teh" })).ToList();
        Assert.Equal("The", Assert.Single(titled).Text);

        var caps = (await provider.GetSuggestionsAsync(new TextContext { CurrentWord = "TEH" })).ToList();
        Assert.Equal("THE", Assert.Single(caps).Text);
    }

    [Fact]
    public async Task GrammarSuggestionProviderOffersTrailingFix()
    {
        var provider = new GrammarSuggestionProvider();
        var suggestions = (await provider.GetSuggestionsAsync(new TextContext
        {
            PreviousWords = "I think he",
            CurrentWord = "are",
            FullText = "I think he are",
            CursorPosition = "I think he are".Length
        })).ToList();

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("Grammar", suggestion.Source);
        Assert.Contains("→", suggestion.Text);
        Assert.True(GrammarSuggestionMapper.TryGetSpanReplacement(suggestion, out var original, out var replacement));
        Assert.Equal("he are", original, ignoreCase: true);
        Assert.Equal("he is", replacement, ignoreCase: true);
    }

    [Fact]
    public void SplitFrom_PutsGrammarInItsOwnList()
    {
        var grammar = GrammarSuggestionMapper.Suggest(new TextContext
        {
            FullText = "he are",
            CursorPosition = 6,
            CurrentWord = "are"
        });
        GrammarSuggestionMapper.SplitFrom(
            grammar.Append(new Suggestion { Text = "hello", Source = "Dictionary" }),
            out var grammarList,
            out var completions);
        Assert.NotEmpty(grammarList);
        Assert.All(grammarList, item => Assert.True(GrammarSuggestionMapper.IsGrammarFix(item)));
        Assert.Equal("hello", Assert.Single(completions).Text);
    }

    [Fact]
    public void TrailingMatches_AllowsClosingPunctuation()
    {
        var trailing = GrammarSuggestionMapper.TrailingMatches("he are.");
        Assert.Contains(trailing, m => m.Replacement.Equals("he is", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WindowFrom_UsesTextBeforeCaret()
    {
        var window = GrammarSuggestionMapper.WindowFrom(new TextContext
        {
            CurrentWord = "are",
            FullText = "prefix he are extra",
            CursorPosition = "prefix he are".Length
        });
        Assert.Equal("prefix he are", window);
        Assert.Contains(GrammarSuggestionMapper.Suggest(new TextContext
        {
            FullText = "prefix he are",
            CursorPosition = "prefix he are".Length,
            CurrentWord = "are"
        }), s => s.Text.Contains("→"));
    }

    [Fact]
    public void MediumSensitivity_SuggestsSpacingAndSkipsNumbers()
    {
        var spaced = RuleBasedGrammarChecker.Find("Hello,world");
        Assert.Contains(spaced, m => m.Replacement.Contains("Hello, world", StringComparison.OrdinalIgnoreCase)
            || m.Message.Contains("space after punctuation", StringComparison.OrdinalIgnoreCase));

        var number = RuleBasedGrammarChecker.Find("Use 3.14 here");
        Assert.DoesNotContain(number, m => m.Message.Contains("space after punctuation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LowSensitivity_SkipsPunctuationSpacing()
    {
        var matches = RuleBasedGrammarChecker.Find("Hello,world", "Low");
        Assert.DoesNotContain(matches, m => m.Category == GrammarRuleCategory.Punctuation);
    }

    [Fact]
    public void SuggestsGluedWords_NotCamelCase()
    {
        var glued = RuleBasedGrammarChecker.Find("thecat sat down");
        Assert.Contains(glued, m => m.Replacement.Equals("the cat", StringComparison.OrdinalIgnoreCase));

        var camel = RuleBasedGrammarChecker.Find("GitHub is ready");
        Assert.DoesNotContain(camel, m => m.Original.Equals("GitHub", StringComparison.Ordinal));
    }

    [Fact]
    public void Homophones_NeedContext()
    {
        Assert.Contains(
            RuleBasedGrammarChecker.Find("a side affect"),
            m => m.Replacement.Equals("side effect", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            RuleBasedGrammarChecker.Find("it will effect the plan"),
            m => m.Replacement.Equals("will affect the", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            RuleBasedGrammarChecker.Find("they will effect change"),
            m => m.Replacement.Contains("affect", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            RuleBasedGrammarChecker.Find("this will affect you"),
            m => m.Original.Contains("affect", StringComparison.OrdinalIgnoreCase)
                && m.Replacement.Contains("effect", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            RuleBasedGrammarChecker.Find("loose weight now"),
            m => m.Replacement.StartsWith("lose ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            RuleBasedGrammarChecker.Find("everyone accept Sam"),
            m => m.Replacement.Equals("everyone except", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IntroComma_Possessive_Ellipsis_Question_Hyphen()
    {
        Assert.Contains(
            RuleBasedGrammarChecker.Find("However I think so"),
            m => m.Replacement.StartsWith("However,", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            RuleBasedGrammarChecker.Find("the cats food"),
            m => m.Replacement.Equals("cat's food", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            RuleBasedGrammarChecker.Find("Wait.."),
            m => m.Replacement == "...");
        Assert.Contains(
            RuleBasedGrammarChecker.Find("Who is there."),
            m => m.Replacement.EndsWith('?'));
        Assert.Contains(
            RuleBasedGrammarChecker.Find("a well known author"),
            m => m.Replacement.Contains("well-known", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            RuleBasedGrammarChecker.Find("send an email later"),
            m => m.Original.Contains("email", StringComparison.OrdinalIgnoreCase) && m.Replacement.Contains('-'));
    }

    [Fact]
    public void IllAndId_OnlyWithContext()
    {
        Assert.Contains(
            RuleBasedGrammarChecker.Find("ill be there"),
            m => m.Replacement.StartsWith("I'll ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            RuleBasedGrammarChecker.Find("id like that"),
            m => m.Replacement.StartsWith("I'd ", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            RuleBasedGrammarChecker.Find("I feel ill today"),
            m => m.Original.Equals("ill", StringComparison.OrdinalIgnoreCase) && m.Replacement.Contains("I'll"));
        Assert.DoesNotContain(
            RuleBasedGrammarChecker.Find("user id number"),
            m => m.Original.Equals("id", StringComparison.OrdinalIgnoreCase) && m.Replacement.Contains("I'd"));
    }

    [Fact]
    public void SentenceCapAndCapitalI_AvailableAtMedium()
    {
        Assert.Contains(
            RuleBasedGrammarChecker.Find("Hello. there"),
            m => m.Replacement == "T" || m.Message.Contains("Capitalize the start", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            RuleBasedGrammarChecker.Find("this is what i want"),
            m => m.Replacement == "I" && m.Original.Equals("i", StringComparison.Ordinal));
    }
}
