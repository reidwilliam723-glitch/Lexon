using Lexon.Core.Interfaces;
using Lexon.Core.Models;
using Lexon.Core.Pipeline;
using Lexon.Core.Learning;
using Lexon.Privacy;
using Moq;
using Xunit;

namespace Lexon.Tests;

public class SuggestionPipelineTests
{
    private readonly Mock<IPrivacyGuard> _mockPrivacyGuard;
    private readonly SuggestionPipeline _pipeline;

    public SuggestionPipelineTests()
    {
        _mockPrivacyGuard = new Mock<IPrivacyGuard>();
        _pipeline = new SuggestionPipeline(_mockPrivacyGuard.Object);
    }

    [Fact]
    public void SetEnabled_WhenTrue_EnablesPipeline()
    {
        // Arrange
        _pipeline.SetEnabled(false);

        // Act
        _pipeline.SetEnabled(true);

        // Assert
        Assert.True(_pipeline.IsEnabled);
    }

    [Fact]
    public void SetEnabled_WhenFalse_DisablesPipeline()
    {
        // Arrange
        _pipeline.SetEnabled(true);

        // Act
        _pipeline.SetEnabled(false);

        // Assert
        Assert.False(_pipeline.IsEnabled);
    }

    [Fact]
    public void IsEnabled_Initially_ReturnsTrue()
    {
        // Assert
        Assert.True(_pipeline.IsEnabled);
    }

    [Fact]
    public async Task GetSuggestionsAsync_WhenDisabled_ReturnsEmpty()
    {
        // Arrange
        var mockProvider = new Mock<ISuggestionProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestProvider");
        mockProvider.Setup(p => p.IsFastPath).Returns(true);
        mockProvider.Setup(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Suggestion { Text = "test", Score = 1.0f } });
        
        _pipeline.AddProvider(mockProvider.Object);
        _pipeline.SetEnabled(false);

        var context = new TextContext { FullText = "test" };

        // Act
        var suggestions = await _pipeline.GetSuggestionsAsync(context);

        // Assert
        Assert.Empty(suggestions);
        mockProvider.Verify(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSuggestionsAsync_WhenEnabled_ReturnsSuggestions()
    {
        // Arrange
        var mockProvider = new Mock<ISuggestionProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestProvider");
        mockProvider.Setup(p => p.IsFastPath).Returns(true);
        mockProvider.Setup(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Suggestion { Text = "test", Score = 1.0f } });
        
        _mockPrivacyGuard.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);
        _pipeline.AddProvider(mockProvider.Object);
        _pipeline.SetEnabled(true);

        var context = new TextContext { FullText = "test" };

        // Act
        var suggestions = await _pipeline.GetSuggestionsAsync(context);

        // Assert
        Assert.NotEmpty(suggestions);
        mockProvider.Verify(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSuggestionsAsync_MostRelevant_OrdersByScore()
    {
        var pipeline = CreatePipelineWithPersonalization(out _);
        pipeline.SetSortMode("Relevant");
        AddTwoSuggestions(pipeline, 0.9, 0.4);

        var suggestions = (await pipeline.GetSuggestionsAsync(new TextContext { CurrentWord = "a" })).Select(s => s.Text).ToList();

        Assert.Equal(new[] { "alpha", "beta" }, suggestions);
    }

    [Fact]
    public async Task GetSuggestionsAsync_MostUsed_PromotesAcceptedSuggestion()
    {
        var pipeline = CreatePipelineWithPersonalization(out _);
        pipeline.SetSortMode("Used");
        AddTwoSuggestions(pipeline, 0.9, 0.4);

        var context = new TextContext { CurrentWord = "a" };
        pipeline.RecordInteraction(new Suggestion { Text = "beta" }, context, InteractionType.Accepted);
        pipeline.RecordInteraction(new Suggestion { Text = "beta" }, context, InteractionType.Accepted);
        pipeline.RecordInteraction(new Suggestion { Text = "beta" }, context, InteractionType.Accepted);

        var suggestions = (await pipeline.GetSuggestionsAsync(context)).Select(s => s.Text).ToList();

        Assert.Equal("beta", suggestions[0]);
        Assert.Equal("alpha", suggestions[1]);
    }

    [Fact]
    public async Task GetSuggestionsAsync_MostUsed_WithNoHistory_FallsBackToScore()
    {
        var pipeline = CreatePipelineWithPersonalization(out _);
        pipeline.SetSortMode("Used");
        AddTwoSuggestions(pipeline, 0.9, 0.4);

        var suggestions = (await pipeline.GetSuggestionsAsync(new TextContext { CurrentWord = "a" })).Select(s => s.Text).ToList();

        Assert.Equal(new[] { "alpha", "beta" }, suggestions);
    }

    private SuggestionPipeline CreatePipelineWithPersonalization(out PersonalizationManager personalization)
    {
        var storage = new Mock<IStorage>();
        storage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        storage.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        personalization = new PersonalizationManager(storage.Object, new FeedbackCollector());
        var pipeline = new SuggestionPipeline(_mockPrivacyGuard.Object);
        pipeline.SetPersonalizationManager(personalization);
        _mockPrivacyGuard.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);
        return pipeline;
    }

    private static void AddTwoSuggestions(SuggestionPipeline pipeline, double alphaScore, double betaScore)
    {
        var mockProvider = new Mock<ISuggestionProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestProvider");
        mockProvider.Setup(p => p.IsFastPath).Returns(true);
        mockProvider.Setup(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Suggestion { Text = "alpha", Score = alphaScore },
                new Suggestion { Text = "beta", Score = betaScore }
            });
        pipeline.AddProvider(mockProvider.Object);
    }
}
