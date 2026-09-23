using Lexon.Core.Expansion;
using Lexon.Core.Grammar;
using Lexon.Core.Interfaces;
using Lexon.Core.Models;
using Lexon.Input;
using Lexon.Input.Interfaces;
using Lexon.Overlay.Interfaces;
using Lexon.Service;
using Moq;
using Xunit;

namespace Lexon.Tests;

/// <summary>
/// Accepting a grammar fix selects backward by word count from the caret, which only
/// lands on the flagged phrase while that phrase is still the trailing text. These
/// cover what happens when typing has moved on since the suggestion appeared.
/// </summary>
public class GrammarAcceptStalenessTests
{
    [Fact]
    public void AcceptedImmediately_ReplacesTheFlaggedPhrase()
    {
        // The caret is still right after "he are", so selecting the last two words
        // hits exactly the flagged phrase.
        var harness = new AcceptHarness("he are");

        harness.Accept(GrammarFix("he are", "he is"));

        harness.Injector.Verify(i => i.SelectBackwardWords(2), Times.Once);
        harness.Injector.Verify(i => i.InjectText("he is "), Times.Once);
    }

    [Fact]
    public void AcceptedAfterTypingMovedOn_LeavesTheNewlyTypedTextAlone()
    {
        // Typing continued past the flagged phrase before the suggestion was clicked.
        // The last two words are now "the store", so selecting backward would eat
        // those instead of "he are".
        var harness = new AcceptHarness("he are going to the store");

        harness.Accept(GrammarFix("he are", "he is"));

        harness.Injector.Verify(i => i.SelectBackwardWords(It.IsAny<int>()), Times.Never);
        harness.Injector.Verify(i => i.InjectText(It.IsAny<string>()), Times.Never);
        harness.Injector.Verify(i => i.DeleteBackward(It.IsAny<int>()), Times.Never);
    }

    private static Suggestion GrammarFix(string original, string replacement) => new()
    {
        Text = replacement,
        Source = GrammarSuggestionMapper.Source,
        Metadata =
        {
            [GrammarSuggestionMapper.OriginalKey] = original,
            [GrammarSuggestionMapper.ReplacementKey] = replacement
        }
    };

    /// <summary>
    /// A <see cref="LexonService"/> wired to mocks, with the caret at the end of
    /// <c>typed</c>, so a suggestion can be accepted through the overlay event the
    /// real overlay raises.
    /// </summary>
    private sealed class AcceptHarness
    {
        public Mock<ITextInjector> Injector { get; } = new();

        private readonly Mock<ISuggestionOverlay> _overlay = new();
        private readonly LexonService _service;

        public AcceptHarness(string typed)
        {
            var focusTracker = new Mock<IFocusTracker>();
            focusTracker.Setup(f => f.GetTypedBufferText()).Returns(typed);
            focusTracker.Setup(f => f.GetCurrentContext()).Returns(new TextContext
            {
                FullText = typed,
                CursorPosition = typed.Length,

                // Deliberately not a browser: this routes through the grammar branch
                // on its own, so the fix is not just being tested for web editors.
                ApplicationName = "notepad",
                WindowTitle = "Untitled - Notepad"
            });

            // Constructed for its side effect: the constructor subscribes to the
            // overlay's SuggestionSelected, which is what Accept below raises.
            _service = new LexonService(
                new Mock<ISuggestionPipeline>().Object,
                new Mock<IKeyboardListener>().Object,
                focusTracker.Object,
                _overlay.Object,
                new Mock<IPrivacyGuard>().Object,
                Injector.Object,
                new TextExpansionManager(new Mock<IStorage>().Object),
                new KeyboardShortcutManager(),
                new UndoManager(Injector.Object));
        }

        public void Accept(Suggestion suggestion)
            => _overlay.Raise(
                o => o.SuggestionSelected += null,
                new SuggestionSelectedEventArgs { SelectedSuggestion = suggestion });
    }
}
