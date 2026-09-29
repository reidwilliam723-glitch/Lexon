using Lexon.Core.Learning;
using Lexon.Core.Models;
using Lexon.Core.Pipeline;
using Lexon.Core.Interfaces;
using Moq;
using Xunit;

namespace Lexon.Tests;

public class WordTransitionModelTests
{
    [Fact]
    public void Seed_RanksThankYouAboveRandomFollower()
    {
        var model = new WordTransitionModel();

        Assert.True(model.GetFollowScore("thank", "you") > model.GetFollowScore("thank", "yellow"));
        Assert.True(model.GetFollowScore("going", "to") > model.GetFollowScore("going", "the"));
    }

    [Fact]
    public void LearnFromCompletedText_BoostsObservedPair()
    {
        var model = new WordTransitionModel();
        model.LearnFromCompletedText("please send the invoice ");
        model.LearnFromCompletedText("please send the report ");

        Assert.True(model.GetFollowScore("please", "send") > 0);
        Assert.True(model.GetFollowScore("send", "the") > 0);
    }

    [Fact]
    public void GetFollowScore_UnknownPrevious_ReturnsZero()
    {
        var model = new WordTransitionModel();

        Assert.Equal(0, model.GetFollowScore("xyzzy", "the"));
        Assert.Equal(0, model.GetFollowScore("", "the"));
    }

    [Fact]
    public void GetTopFollowers_ReturnsHighestWeightedFollowers()
    {
        var model = new WordTransitionModel();
        var followers = model.GetTopFollowers("thank", 3);

        Assert.Contains("you", followers);
        Assert.Equal("you", followers[0]);
        Assert.True(followers.Count is >= 1 and <= 3);
    }

    [Fact]
    public void GetTopFollowers_UnknownWord_ReturnsEmpty()
    {
        var model = new WordTransitionModel();

        Assert.Empty(model.GetTopFollowers("xyzzy"));
        Assert.Empty(model.GetTopFollowers(""));
        Assert.Empty(model.GetTopFollowers("   "));
    }
}

public class SuggestionPipelineBigramTests
{
    [Fact]
    public async Task GetSuggestionsAsync_PreviousWord_PromotesCommonFollower()
    {
        var storage = new Mock<IStorage>();
        storage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        storage.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var privacy = new Mock<IPrivacyGuard>();
        privacy.Setup(g => g.IsSecureField(It.IsAny<TextContext>())).Returns(false);

        var personalization = new PersonalizationManager(storage.Object, new FeedbackCollector());
        var pipeline = new SuggestionPipeline(privacy.Object);
        pipeline.SetPersonalizationManager(personalization);

        var provider = new Mock<ISuggestionProvider>();
        provider.Setup(p => p.Name).Returns("Test");
        provider.Setup(p => p.IsFastPath).Returns(true);
        provider.Setup(p => p.GetSuggestionsAsync(It.IsAny<TextContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Suggestion { Text = "the", Score = 0.7 },
                new Suggestion { Text = "to", Score = 0.7 }
            });
        pipeline.AddProvider(provider.Object);

        var afterGoing = (await pipeline.GetSuggestionsAsync(new TextContext
        {
            CurrentWord = "t",
            PreviousWords = "going"
        })).Select(s => s.Text).ToList();

        Assert.Equal("to", afterGoing[0]);

        var afterOf = (await pipeline.GetSuggestionsAsync(new TextContext
        {
            CurrentWord = "t",
            PreviousWords = "of"
        })).Select(s => s.Text).ToList();

        Assert.Equal("the", afterOf[0]);
    }
}
