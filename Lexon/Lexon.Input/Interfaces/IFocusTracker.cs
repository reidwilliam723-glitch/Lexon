using Lexon.Core.Models;

namespace Lexon.Input.Interfaces;

/// <summary>
/// Interface for tracking window focus and text context
/// </summary>
public interface IFocusTracker
{
    event EventHandler<TextContext>? ContextChanged;
    TextContext GetCurrentContext();
    string GetTypedBufferText();
    bool TryGetSelectedText(out string selected);
    (int X, int Y) GetCaretScreenPosition();
    (int X, int Y) GetWordAnchorScreenPosition(string? currentWord);
    void NotePointerScreenPosition(int x, int y);
    int LastAnchorLineHeight { get; }
    void Start();
    void Stop();
    void AddTypedCharacter(char character);
    bool IsCurrentFieldSecure();
    string GetForegroundApplicationName();
    void ClearTypedBuffers();
}
