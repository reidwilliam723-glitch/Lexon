namespace Lexon.Input.Interfaces;

/// <summary>
/// Interface for injecting text into the focused application
/// </summary>
public interface ITextInjector
{
    /// <summary>
    /// Injects the specified text into the focused application
    /// </summary>
    void InjectText(string text);

    /// <summary>
    /// Deletes the specified number of characters backward
    /// </summary>
    void DeleteBackward(int count);

    /// <summary>
    /// Replaces old text with new text. Use
    /// <paramref name="selectionStillActive"/> when the target is still
    /// highlighted so a single Backspace deletes the selection instead of
    /// characters before it.
    /// </summary>
    void ReplaceText(string oldText, string newText, bool selectionStillActive = false);

    /// <summary>
    /// Replaces the current selection (one Backspace, then insert).
    /// </summary>
    void ReplaceSelection(string newText);

    /// <summary>
    /// Injects text via clipboard as a fallback method
    /// </summary>
    void InjectTextViaClipboard(string text);

    /// <summary>
    /// Extends the selection backward by whole words (Ctrl+Shift+Left).
    /// Used in browsers / Google Docs where batched Backspace is dropped.
    /// </summary>
    void SelectBackwardWords(int wordCount);

    void Flush();
}
