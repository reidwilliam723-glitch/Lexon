using Lexon.Core.Grammar;
using Lexon.Core.Models;
using Xunit;

namespace Lexon.Tests;

public class SpellingContextCallbackTests
{
    [Fact]
    public async Task TypoSuggestion_PassesTheProvidedContext_NotAFreshLookup()
    {
        TextContext? seen = null;
        var provider = new TypoSuggestionProvider
        {
            IsProtectedWord = (word, ctx) =>
            {
                seen = ctx;
                return word.Equals("recieve", StringComparison.OrdinalIgnoreCase);
            }
        };
        var context = new TextContext
        {
            CurrentWord = "recieve",
            ApplicationName = "notepad",
            FullText = "please recieve"
        };

        var suggestions = await provider.GetSuggestionsAsync(context);

        Assert.Empty(suggestions);
        Assert.Same(context, seen);
        Assert.Equal("notepad", seen!.ApplicationName);
    }

    [Fact]
    public async Task GrammarSuggestion_PassesTheProvidedContextToSkip()
    {
        TextContext? seen = null;
        var provider = new GrammarSuggestionProvider
        {
            SkipSpellingWord = (word, ctx) =>
            {
                seen = ctx;
                return word.Equals("recieve", StringComparison.OrdinalIgnoreCase);
            }
        };
        var context = new TextContext
        {
            CurrentWord = "recieve",
            FullText = "please recieve",
            CursorPosition = "please recieve".Length,
            ApplicationName = "code"
        };

        var suggestions = await provider.GetSuggestionsAsync(context);

        Assert.DoesNotContain(suggestions, s => s.Text.Contains("recieve", StringComparison.OrdinalIgnoreCase));
        Assert.Same(context, seen);
    }
}