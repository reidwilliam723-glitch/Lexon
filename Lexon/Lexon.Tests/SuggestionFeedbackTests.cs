using System.Text;
using Lexon.Core.Expansion;
using Lexon.Core.Interfaces;
using Lexon.Core.Learning;
using Lexon.Core.Models;
using Lexon.Core.Pipeline;
using Lexon.Input;
using Lexon.Input.Interfaces;
using Lexon.Overlay.Interfaces;
using Lexon.Service;
using Moq;
using Xunit;

namespace Lexon.Tests;

public class SuggestionFeedbackTests
{
    [Fact]
    public void Wrong_RecordsRejected_WithReason_NotIgnored()
    {
        var env = FeedbackHarness.Create();
        var suggestion = new Suggestion { Text = "teh", Source = "Local", Score = 0.5 };

        env.Overlay.Raise(
            o => o.SuggestionFeedbackRequested += null,
            new SuggestionFeedbackEventArgs
            {
                Suggestion = suggestion,
                Reason = SuggestionFeedbackEventArgs.ReasonWrong
            });

        Assert.Equal(1, env.Personalization.GetRejectedTotal());
        Assert.Equal(0, env.Personalization.GetIgnoredTotal());
        var recent = env.Feedback.GetRecentFeedback(5).First();
        Assert.Equal(FeedbackType.Rejected, recent.Type);
        Assert.Equal(SuggestionFeedbackEventArgs.ReasonWrong, recent.Reason);
        Assert.Empty(env.BlockedApps);
    }

    [Fact]
    public void NotForThisApp_RecordsRejected_AndRequestsBlock()
    {
        var env = FeedbackHarness.Create(app: "chrome");
        var suggestion = new Suggestion { Text = "http", Source = "Local", Score = 0.4 };

        env.Overlay.Raise(
            o => o.SuggestionFeedbackRequested += null,
            new SuggestionFeedbackEventArgs
            {
                Suggestion = suggestion,
                Reason = SuggestionFeedbackEventArgs.ReasonNotForApp
            });

        Assert.Equal(1, env.Personalization.GetRejectedTotal());
        Assert.Equal(["chrome"], env.BlockedApps);
        var recent = env.Feedback.GetRecentFeedback(5).First();
        Assert.Equal(SuggestionFeedbackEventArgs.ReasonNotForApp, recent.Reason);
    }

    [Fact]
    public void DismissWithoutFeedback_StillIgnored()
    {
        var env = FeedbackHarness.Create();
        var suggestion = new Suggestion { Text = "skip", Source = "Local", Score = 0.2 };

        env.Overlay.Raise(
            o => o.SuggestionDismissed += null,
            new SuggestionDismissedEventArgs
            {
                DismissedSuggestions = [suggestion]
            });

        Assert.Equal(0, env.Personalization.GetRejectedTotal());
        Assert.Equal(1, env.Personalization.GetIgnoredTotal());
    }

    private sealed class FeedbackHarness
    {
        public Mock<ISuggestionOverlay> Overlay { get; } = new();
        public FeedbackCollector Feedback { get; } = new();
        public PersonalizationManager Personalization { get; }
        public List<string> BlockedApps { get; } = [];

        private FeedbackHarness(string app)
        {
            var storage = new Mock<IStorage>();
            storage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);
            storage.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Personalization = new PersonalizationManager(storage.Object, Feedback);
            var privacy = new Mock<IPrivacyGuard>();
            privacy.Setup(p => p.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);
            var pipeline = new SuggestionPipeline(privacy.Object);
            pipeline.SetPersonalizationManager(Personalization);

            var buffer = new StringBuilder("hel");
            var context = new TextContext
            {
                FullText = buffer.ToString(),
                CursorPosition = buffer.Length,
                CurrentWord = "hel",
                ApplicationName = app,
                WindowTitle = app
            };
            var focus = new Mock<IFocusTracker>();
            focus.Setup(f => f.GetTypedBufferText()).Returns(() => buffer.ToString());
            focus.Setup(f => f.GetCurrentContext()).Returns(context);
            focus.Setup(f => f.GetWordAnchorScreenPosition(It.IsAny<string?>())).Returns((10, 10));

            Overlay.Setup(o => o.HasPredictions).Returns(false);
            var keyboard = new Mock<IKeyboardListener>();
            var injector = new Mock<ITextInjector>();

            var service = new LexonService(
                pipeline,
                keyboard.Object,
                focus.Object,
                Overlay.Object,
                privacy.Object,
                injector.Object,
                new TextExpansionManager(storage.Object),
                new KeyboardShortcutManager(),
                new UndoManager(injector.Object),
                personalization: Personalization);
            service.BlockApplicationRequested += (_, name) => BlockedApps.Add(name);
            focus.Raise(f => f.ContextChanged += null, null!, context);
        }

        public static FeedbackHarness Create(string app = "notepad") => new(app);
    }
}
