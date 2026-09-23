using Lexon.Input.Interfaces;
using System.Linq;

namespace Lexon.Input;

/// <summary>
/// Manages undo operations for text injections
/// </summary>
public class UndoManager
{
    private readonly ITextInjector _textInjector;
    private readonly Stack<TextOperation> _undoStack;
    private readonly Stack<TextOperation> _redoStack;
    private const int MaxStackSize = 50;

    public UndoManager(ITextInjector textInjector)
    {
        _textInjector = textInjector ?? throw new ArgumentNullException(nameof(textInjector));
        _undoStack = new Stack<TextOperation>(MaxStackSize);
        _redoStack = new Stack<TextOperation>(MaxStackSize);
    }

    public event EventHandler? Changed;

    public string? LastUndoLabel
    {
        get
        {
            var operation = PeekUndo();
            if (operation == null)
            {
                return null;
            }

            var inserted = operation.InsertedText.Trim();
            if (inserted.Length > 24)
            {
                inserted = inserted[..23] + "…";
            }

            return string.IsNullOrEmpty(inserted) ? "Undo last change" : $"Undo “{inserted}”";
        }
    }

    public void RecordOperation(string deletedText, string insertedText)
    {
        if (string.IsNullOrEmpty(deletedText) && string.IsNullOrEmpty(insertedText))
        {
            return;
        }

        var operation = new TextOperation
        {
            DeletedText = deletedText ?? string.Empty,
            InsertedText = insertedText ?? string.Empty,
            Timestamp = DateTime.UtcNow
        };

        _undoStack.Push(operation);
        _redoStack.Clear(); // Clear redo stack on new operation

        // Limit stack size - remove oldest entries if over limit
        if (_undoStack.Count > MaxStackSize)
        {
            var recentOperations = _undoStack.Take(MaxStackSize).ToList();
            _undoStack.Clear();
            for (int i = recentOperations.Count - 1; i >= 0; i--)
            {
                _undoStack.Push(recentOperations[i]);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;

        var operation = _undoStack.Pop();

        // To undo, we delete the inserted text and re-insert the deleted text
        if (!string.IsNullOrEmpty(operation.InsertedText))
        {
            _textInjector.DeleteBackward(operation.InsertedText.Length);
        }

        if (!string.IsNullOrEmpty(operation.DeletedText))
        {
            _textInjector.InjectText(operation.DeletedText);
        }

        _redoStack.Push(operation);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;

        var operation = _redoStack.Pop();

        // To redo, we delete the deleted text and re-insert the inserted text
        if (!string.IsNullOrEmpty(operation.DeletedText))
        {
            _textInjector.DeleteBackward(operation.DeletedText.Length);
        }

        if (!string.IsNullOrEmpty(operation.InsertedText))
        {
            _textInjector.InjectText(operation.InsertedText);
        }

        _undoStack.Push(operation);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public TextOperation? PeekUndo()
    {
        return _undoStack.Count > 0 ? _undoStack.Peek() : null;
    }

    public TextOperation? PeekRedo()
    {
        return _redoStack.Count > 0 ? _redoStack.Peek() : null;
    }
}

/// <summary>
/// Represents a text operation that can be undone/redone
/// </summary>
public class TextOperation
{
    public string DeletedText { get; set; } = string.Empty;
    public string InsertedText { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
