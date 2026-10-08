using Lexon.Core.Models;

namespace Lexon.Overlay.Interfaces;

/// <summary>
/// Interface for the non-activating suggestion overlay window
/// </summary>
public interface ISuggestionOverlay : IDisposable
{
    void ShowSuggestions(IEnumerable<Suggestion> suggestions, int x, int y, int lineHeight = 20);
    void ReplaceSuggestions(IEnumerable<Suggestion> suggestions);
    void SetPlacement(string placement);
    void Hide();
    bool IsVisible { get; }
    bool HasPredictions { get; }

    /// <summary>True when the word or grammar list has rows, not only next-word chips.</summary>
    bool HasSuggestionList { get; }

    bool CanConfirmVisible(int visibleIndex);

    void ConfirmVisible(int visibleIndex);
    void MoveTo(int x, int y, int lineHeight = 20);
    void SelectNext();
    void SelectPrevious();
    void ConfirmSelection();
    void ShowPredictions(PredictedFollowers predictions, int x, int y, int lineHeight = 20);
    void ConfirmPrediction(int index);
    void FlashCorrection(string text, int x, int y, int lineHeight = 20);
    void ShowStatus(string message, int x, int y, int lineHeight = 20) { }
    event EventHandler<SuggestionSelectedEventArgs>? SuggestionSelected;

    /// <summary>
    /// Raised when the overlay is hidden while suggestions were visible and
    /// none of them was selected — i.e. the user typed past them, pressed
    /// Escape, or moved focus away. Not raised after a selection, since that
    /// case is already covered by <see cref="SuggestionSelected"/>.
    /// </summary>
    event EventHandler<SuggestionDismissedEventArgs>? SuggestionDismissed;

    /// <summary>
    /// Raised when the user clicks the brief Undo chip shown after an
    /// automatic typo correction.
    /// </summary>
    event EventHandler? CorrectionUndoRequested;

    /// <summary>
    /// Raised when the user marks a suggestion Wrong or Not for this app
    /// (right-click menu on a suggestion row).
    /// </summary>
    event EventHandler<SuggestionFeedbackEventArgs>? SuggestionFeedbackRequested;
}

public class SuggestionSelectedEventArgs : EventArgs
{
    public Suggestion SelectedSuggestion { get; set; } = null!;
}

public class SuggestionDismissedEventArgs : EventArgs
{
    public IReadOnlyList<Suggestion> DismissedSuggestions { get; set; } = Array.Empty<Suggestion>();
}

public class SuggestionFeedbackEventArgs : EventArgs
{
    public const string ReasonWrong = "wrong";
    public const string ReasonNotForApp = "not_for_app";

    public Suggestion Suggestion { get; set; } = null!;

    public string Reason { get; set; } = ReasonWrong;
}
