using System.Text;
using Lexon.Core.Expansion;
using Lexon.Core.Interfaces;
using Lexon.Core.Learning;
using Lexon.Core.Models;
using Lexon.Input;
using Lexon.Input.Interfaces;
using Lexon.Overlay.Interfaces;
using Lexon.Service;
using Moq;
using Xunit;

namespace Lexon.Tests;

public class NextWordAndAutoCorrectTests
{
    [Fact]
    public void SpaceAfterKnownTypo_InjectsCorrection()
    {
        var harness = WordHarness.WithBuffer("teh", autoCorrect: true);
        harness.Privacy.Setup(p => p.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);
        harness.Pipeline.Setup(p => p.GetLearnedWords()).Returns(Array.Empty<string>());

        harness.TypeSpace();
        harness.WaitForIdle();

        harness.Injector.Verify(i => i.DeleteBackward(4), Times.Once);
        harness.Injector.Verify(i => i.InjectText("the "), Times.Once);
        harness.Overlay.Verify(
            o => o.FlashCorrection("the", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public void SpaceAfterKnownTypo_WhenDisabled_DoesNotInject()
    {
        var harness = WordHarness.WithBuffer("teh", autoCorrect: false);
        harness.Privacy.Setup(p => p.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);

        harness.TypeSpace();
        harness.WaitForIdle();

        harness.Injector.Verify(i => i.DeleteBackward(It.IsAny<int>()), Times.Never);
        harness.Injector.Verify(i => i.InjectText(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SpaceAfterThank_ShowsYouPrediction()
    {
        var harness = WordHarness.WithBuffer("thank", autoCorrect: false);
        harness.Privacy.Setup(p => p.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);

        harness.TypeSpace();
        harness.WaitForIdle();

        harness.Overlay.Verify(
            o => o.ShowPredictions(
                It.Is<PredictedFollowers>(p => p.PreviousWord == "thank" && p.Words.Contains("you")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public void BlockedField_DoesNotPredictOrCorrect()
    {
        var harness = WordHarness.WithBuffer("teh", autoCorrect: true);
        harness.Privacy.Setup(p => p.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(true);

        harness.TypeSpace();
        harness.WaitForIdle();

        harness.Injector.Verify(i => i.InjectText(It.IsAny<string>()), Times.Never);
        harness.Overlay.Verify(
            o => o.ShowPredictions(It.IsAny<PredictedFollowers>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public void LearnedVocabulary_IsNotAutoCorrected()
    {
        var harness = WordHarness.WithBuffer("teh", autoCorrect: true);
        harness.Privacy.Setup(p => p.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);
        harness.Pipeline.Setup(p => p.GetLearnedWords()).Returns(new[] { "teh" });

        harness.TypeSpace();
        harness.WaitForIdle();

        harness.Injector.Verify(i => i.DeleteBackward(It.IsAny<int>()), Times.Never);
        harness.Injector.Verify(i => i.InjectText(It.IsAny<string>()), Times.Never);
        harness.Overlay.Verify(
            o => o.FlashCorrection(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public void NumberKey_ConfirmsMatchingPredictionChip()
    {
        var harness = WordHarness.WithBuffer("thank", autoCorrect: false);
        harness.Privacy.Setup(p => p.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);
        harness.Overlay.Setup(o => o.HasPredictions).Returns(true);

        harness.TypeKey(0x31);
        harness.WaitForIdle();

        harness.Overlay.Verify(o => o.ConfirmPrediction(0), Times.Once);
        Assert.True(harness.LastKey.Handled);
    }

    [Fact]
    public void Tab_WithOnlyPredictionChips_IsNotStolen()
    {
        var harness = WordHarness.WithBuffer("thank ", autoCorrect: false);
        harness.Privacy.Setup(p => p.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);
        harness.Overlay.Setup(o => o.HasPredictions).Returns(true);
        harness.Overlay.Setup(o => o.IsVisible).Returns(true);

        harness.TypeKey(9);

        Assert.False(harness.LastKey.Handled);
        harness.Overlay.Verify(o => o.ConfirmSelection(), Times.Never);
    }

    [Fact]
    public void AcceptingPrediction_InsertsWordWithoutReplacingPrevious()
    {
        var harness = WordHarness.WithBuffer("thank ", autoCorrect: false);
        harness.Privacy.Setup(p => p.ShouldBlockAssistance(It.IsAny<TextContext>())).Returns(false);

        harness.Overlay.Raise(
            o => o.SuggestionSelected += null,
            new SuggestionSelectedEventArgs
            {
                SelectedSuggestion = new Suggestion { Text = "you", Source = "Prediction", Score = 1 }
            });

        harness.Injector.Verify(i => i.DeleteBackward(It.IsAny<int>()), Times.Never);
        harness.Injector.Verify(i => i.InjectText("you "), Times.Once);
    }

    private sealed class WordHarness
    {
        public Mock<ITextInjector> Injector { get; } = new();
        public Mock<ISuggestionOverlay> Overlay { get; } = new();
        public Mock<IPrivacyGuard> Privacy { get; } = new();
        public Mock<ISuggestionPipeline> Pipeline { get; } = new();
        public Mock<IKeyboardListener> Keyboard { get; } = new();
        public KeyboardEventArgs LastKey { get; private set; } = new();

        private readonly StringBuilder _buffer;

        private WordHarness(string typed, bool autoCorrect)
        {
            _buffer = new StringBuilder(typed);
            var focus = new Mock<IFocusTracker>();
            focus.Setup(f => f.GetTypedBufferText()).Returns(() => _buffer.ToString());
            focus.Setup(f => f.GetCurrentContext()).Returns(() => new TextContext
            {
                FullText = _buffer.ToString(),
                CursorPosition = _buffer.Length,
                ApplicationName = "notepad",
                WindowTitle = "Untitled - Notepad"
            });
            focus.Setup(f => f.GetWordAnchorScreenPosition(It.IsAny<string?>())).Returns((40, 80));
            focus.Setup(f => f.AddTypedCharacter(It.IsAny<char>()))
                .Callback<char>(ch =>
                {
                    if (ch == '\b')
                    {
                        if (_buffer.Length > 0)
                        {
                            _buffer.Length--;
                        }

                        return;
                    }

                    _buffer.Append(ch);
                });

            Overlay.Setup(o => o.HasPredictions).Returns(false);

            var storage = new Mock<IStorage>();
            storage.Setup(s => s.LoadAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);
            storage.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var personalization = new PersonalizationManager(storage.Object, new FeedbackCollector());

            _ = new LexonService(
                Pipeline.Object,
                Keyboard.Object,
                focus.Object,
                Overlay.Object,
                Privacy.Object,
                Injector.Object,
                new TextExpansionManager(storage.Object),
                new KeyboardShortcutManager(),
                new UndoManager(Injector.Object),
                autoCorrectEnabled: () => autoCorrect,
                personalization: personalization);
        }

        public static WordHarness WithBuffer(string typed, bool autoCorrect) => new(typed, autoCorrect);

        public void TypeSpace() => TypeKey(32);

        public void TypeKey(int virtualKey)
        {
            LastKey = new KeyboardEventArgs { VirtualKey = virtualKey };
            Keyboard.Raise(k => k.KeyPressed += null, LastKey);
        }

        public void WaitForIdle() => Thread.Sleep(250);
    }
}
