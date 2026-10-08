using Lexon.Core.Expansion;
using Lexon.Core.Interfaces;
using Lexon.Core.Models;
using Lexon.Input;
using Lexon.Input.Interfaces;
using Lexon.Overlay.Interfaces;
using Lexon.Privacy;
using Lexon.Service;
using Moq;
using Xunit;

namespace Lexon.Tests;

public class PasswordSuggestionGuardTests
{
    [Fact]
    public void ContextChange_IntoPasswordField_HidesAnOpenSuggestion()
    {
        var harness = new Harness(password: false);
        harness.Focus.Setup(f => f.GetTypedBufferText()).Returns("hel");
        harness.Focus.Setup(f => f.GetCurrentContext()).Returns(new TextContext
        {
            IsPasswordField = true,
            ApplicationName = "notepad",
            WindowTitle = "Login"
        });

        harness.Focus.Raise(
            f => f.ContextChanged += null,
            null!,
            new TextContext
            {
                IsPasswordField = true,
                ApplicationName = "notepad",
                WindowTitle = "Login"
            });

        harness.Overlay.Verify(o => o.Hide(), Times.AtLeastOnce);
    }

    [Fact]
    public void AcceptingSuggestion_InPasswordField_DoesNotInsertText()
    {
        var harness = new Harness(password: true);

        harness.Overlay.Raise(
            o => o.SuggestionSelected += null,
            new SuggestionSelectedEventArgs
            {
                SelectedSuggestion = new Suggestion { Text = "hello", Source = "Dictionary", Score = 1 }
            });

        harness.Injector.Verify(i => i.InjectText(It.IsAny<string>()), Times.Never);
        harness.Injector.Verify(i => i.DeleteBackward(It.IsAny<int>()), Times.Never);
        harness.Injector.Verify(i => i.SelectBackwardWords(It.IsAny<int>()), Times.Never);
        harness.Focus.Verify(f => f.AddTypedCharacter(It.IsAny<char>()), Times.Never);
        harness.Overlay.Verify(o => o.Hide(), Times.AtLeastOnce);
    }

    [Fact]
    public void AcceptingSuggestion_InNormalField_StillInsertsText()
    {
        var harness = new Harness(password: false);

        harness.Overlay.Raise(
            o => o.SuggestionSelected += null,
            new SuggestionSelectedEventArgs
            {
                SelectedSuggestion = new Suggestion { Text = "hello", Source = "Dictionary", Score = 1 }
            });

        harness.Injector.Verify(i => i.InjectText("lo "), Times.Once);
    }

    private sealed class Harness
    {
        public Mock<IFocusTracker> Focus { get; } = new();
        public Mock<ISuggestionOverlay> Overlay { get; } = new();
        public Mock<ITextInjector> Injector { get; } = new();

        public Harness(bool password)
        {
            var context = new TextContext
            {
                FullText = password ? string.Empty : "hel",
                CursorPosition = password ? 0 : 3,
                CurrentWord = password ? string.Empty : "hel",
                IsPasswordField = password,
                ApplicationName = "notepad",
                WindowTitle = password ? "Login" : "Untitled - Notepad"
            };
            Focus.Setup(f => f.GetTypedBufferText()).Returns(password ? string.Empty : "hel");
            Focus.Setup(f => f.GetCurrentContext()).Returns(context);
            Focus.Setup(f => f.IsCurrentFieldSecure()).Returns(password);

            _ = new LexonService(
                new Mock<ISuggestionPipeline>().Object,
                new Mock<IKeyboardListener>().Object,
                Focus.Object,
                Overlay.Object,
                new PrivacyGuard(),
                Injector.Object,
                new TextExpansionManager(new Mock<IStorage>().Object),
                new KeyboardShortcutManager(),
                new UndoManager(Injector.Object));
        }
    }
}
