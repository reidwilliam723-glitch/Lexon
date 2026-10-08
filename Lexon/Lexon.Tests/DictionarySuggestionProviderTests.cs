using Lexon.Core.Interfaces;
using Lexon.Core.Models;
using Lexon.Storage;
using Moq;
using Xunit;

namespace Lexon.Tests;

public class DictionarySuggestionProviderTests
{
    private readonly Mock<IStorage> _mockStorage;

    public DictionarySuggestionProviderTests()
    {
        _mockStorage = new Mock<IStorage>();
        _mockStorage.Setup(s => s.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockStorage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GetSuggestionsAsync_PrefixMatch_ReturnsMatchingWords()
    {
        // Arrange
        var dictionaryJson = "{\"h\":[\"hello\",\"help\"],\"w\":[\"world\",\"word\"]}";
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dictionaryJson);
        
        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        var context = new TextContext
        {
            CurrentWord = "hel",
            PreviousWords = "",
            CursorPosition = 3
        };

        // Act
        var suggestions = await provider.GetSuggestionsAsync(context, default);

        // Assert
        Assert.NotNull(suggestions);
        Assert.Contains(suggestions, s => s.Text.Contains("hello", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(suggestions, s => s.Text.Contains("help", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetSuggestionsAsync_MultiCharacterPrefix_ReturnsMatches()
    {
        // Arrange
        var dictionaryJson = "{\"h\":[\"hello\",\"help\"],\"w\":[\"world\",\"word\"]}";
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dictionaryJson);
        
        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        var context = new TextContext
        {
            CurrentWord = "hel",
            PreviousWords = "",
            CursorPosition = 3
        };

        // Act
        var suggestions = await provider.GetSuggestionsAsync(context, default);

        // Assert
        Assert.NotNull(suggestions);
        Assert.True(suggestions.Count() >= 2);
    }

    [Fact]
    public async Task GetSuggestionsAsync_FirstLetter_ReturnsCommonWordsWithoutFullScan()
    {
        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        var suggestions = (await provider.GetSuggestionsAsync(
            new TextContext { CurrentWord = "t", PreviousWords = "", CursorPosition = 1 },
            default)).ToList();

        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Text.Equals("the", StringComparison.OrdinalIgnoreCase)
            || s.Text.Equals("to", StringComparison.OrdinalIgnoreCase));
        Assert.True(suggestions.Count <= 40);
    }

    [Fact]
    public async Task GetSuggestionsAsync_NoMatch_ReturnsEmpty()
    {
        // Arrange
        var dictionaryJson = "{\"h\":[\"hello\",\"help\"],\"w\":[\"world\"]}";
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dictionaryJson);
        
        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        var context = new TextContext
        {
            CurrentWord = "zzqxxqjkb",
            PreviousWords = "",
            CursorPosition = 3
        };

        // Act
        var suggestions = await provider.GetSuggestionsAsync(context, default);

        // Assert
        Assert.NotNull(suggestions);
        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task GetSuggestionsAsync_EmptyPrefix_ReturnsEmpty()
    {
        // Arrange
        var dictionaryJson = "{\"h\":[\"hello\",\"help\"],\"w\":[\"world\"]}";
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dictionaryJson);
        
        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        var context = new TextContext
        {
            CurrentWord = "",
            PreviousWords = "",
            CursorPosition = 0
        };

        // Act
        var suggestions = await provider.GetSuggestionsAsync(context, default);

        // Assert
        Assert.NotNull(suggestions);
        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task GetSuggestionsAsync_CaseInsensitive_ReturnsMatches()
    {
        // Arrange
        var dictionaryJson = "{\"h\":[\"Hello\",\"Help\"],\"w\":[\"World\"]}";
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dictionaryJson);
        
        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        var context = new TextContext
        {
            CurrentWord = "hel",
            PreviousWords = "",
            CursorPosition = 3
        };

        // Act
        var suggestions = await provider.GetSuggestionsAsync(context, default);

        // Assert
        Assert.NotNull(suggestions);
        Assert.True(suggestions.Count() >= 2);
    }

    [Fact]
    public async Task GetSuggestionsAsync_LimitsToTop40()
    {
        // Arrange
        var words = Enumerable.Range(0, 50).Select(i => $"word{i:D2}").ToList();
        var dictionaryJson = "{\"w\":[" + string.Join(",", words.Select(w => $"\"{w}\"")) + "]}";
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dictionaryJson);
        
        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        var context = new TextContext
        {
            CurrentWord = "word",
            PreviousWords = "",
            CursorPosition = 4
        };

        // Act
        var suggestions = await provider.GetSuggestionsAsync(context, default);

        // Assert
        Assert.NotNull(suggestions);
        Assert.True(suggestions.Count() <= 40);
        Assert.True(suggestions.Count() >= 10);
    }

    [Fact]
    public async Task GetSuggestionsAsync_LongPrefix_IncludesLongerCompletions()
    {
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var provider = new DictionarySuggestionProvider(_mockStorage.Object);

        var suggestions = (await provider.GetSuggestionsAsync(new TextContext
        {
            CurrentWord = "communi",
            CursorPosition = 7
        }, default)).Select(s => s.Text.ToLowerInvariant()).ToList();

        Assert.Contains(suggestions, t => t.StartsWith("communi"));
        Assert.Contains(suggestions, t => t.Length >= 10);
    }

    [Fact]
    public void AddWords_SkipsInProgressPrefixesOfLexiconWords()
    {
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        var before = provider.WordCount;

        provider.AddWords(new[] { "hel", "hello" });
        Assert.Equal(before, provider.WordCount);

        for (var i = 0; i < 3; i++)
        {
            provider.AddWords(new[] { "zzqxxcustomword" });
        }

        Assert.Equal(before + 1, provider.WordCount);
    }

    [Fact]
    public void LearnWords_RequiresThreeSightingsBeforeSuggesting()
    {
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        var before = provider.WordCount;

        provider.LearnWords(new[] { "zzqxxcustomword" });
        provider.LearnWords(new[] { "zzqxxcustomword" });
        Assert.Equal(before, provider.WordCount);
        Assert.Empty(provider.GetLearnedWords());

        provider.LearnWords(new[] { "zzqxxcustomword" });
        Assert.Equal(before + 1, provider.WordCount);
        Assert.Contains("zzqxxcustomword", provider.GetLearnedWords());
    }

    [Fact]
    public void LearnWords_SkipsPrefixOfOfferedCompletion()
    {
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        var before = provider.WordCount;

        for (var i = 0; i < 3; i++)
        {
            provider.LearnWords(new[] { "zzqxxhel" }, new[] { "zzqxxhello" });
        }

        Assert.Equal(before, provider.WordCount);
    }

    [Fact]
    public void NeverLearnWord_RemovesAndBlocks()
    {
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        provider.AddExplicitWord("zzqxxcustomword");
        Assert.Contains("zzqxxcustomword", provider.GetLearnedWords());

        provider.NeverLearnWord("zzqxxcustomword");
        Assert.DoesNotContain("zzqxxcustomword", provider.GetLearnedWords());

        provider.AddExplicitWord("zzqxxcustomword");
        Assert.DoesNotContain("zzqxxcustomword", provider.GetLearnedWords());
    }

    [Fact]
    public void UndoLastLearn_RevertsRecentSighting()
    {
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        provider.AddExplicitWord("zzqxxcustomword");
        Assert.True(provider.UndoLastLearn());
        Assert.DoesNotContain("zzqxxcustomword", provider.GetLearnedWords());
    }

    [Fact]
    public void Load_WipesPreviousLearnedWordsOnce()
    {
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"zzqxxoldjunk\",\"hel\"]");

        var provider = new DictionarySuggestionProvider(_mockStorage.Object);

        Assert.Empty(provider.GetLearnedWords());
    }

    [Fact]
    public void ClearLearnedWords_RemovesPromotedWords()
    {
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var provider = new DictionarySuggestionProvider(_mockStorage.Object);
        provider.AddExplicitWord("zzqxxcustomword");
        Assert.Contains("zzqxxcustomword", provider.GetLearnedWords());

        provider.ClearLearnedWords();
        Assert.Empty(provider.GetLearnedWords());
    }

    [Fact]
    public void WordCount_IncludesFullEnglishLexicon()
    {
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var provider = new DictionarySuggestionProvider(_mockStorage.Object);

        Assert.True(provider.WordCount > 300_000, $"Expected a full lexicon, got {provider.WordCount}");
    }

    [Fact]
    public async Task LearnedOnlyWord_IsOmittedWhenThatAppDisablesLearnedWords()
    {
        _mockStorage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var provider = new DictionarySuggestionProvider(_mockStorage.Object)
        {
            AllowsLearnedWords = app => !string.Equals(app, "winword", StringComparison.OrdinalIgnoreCase)
        };
        provider.AddExplicitWord("zzqxxcustomword");

        var allowed = await provider.GetSuggestionsAsync(new TextContext
        {
            CurrentWord = "zzq",
            ApplicationName = "notepad"
        });
        var muted = await provider.GetSuggestionsAsync(new TextContext
        {
            CurrentWord = "zzq",
            ApplicationName = "winword"
        });

        Assert.Contains(allowed, s => s.Text == "zzqxxcustomword");
        Assert.DoesNotContain(muted, s => s.Text == "zzqxxcustomword");
        Assert.True(provider.IsLearnedOnly("zzqxxcustomword"));
    }
}
