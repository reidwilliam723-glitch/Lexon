namespace Lexon.Core;

public sealed class EditHistoryEntry
{
    public DateTime Utc { get; init; } = DateTime.UtcNow;

    public string Original { get; init; } = string.Empty;

    public string Replacement { get; init; } = string.Empty;
}

/// <summary>
/// Recent Lexon edits kept on this PC. Stores only the replaced span and the
/// text Lexon inserted, never the surrounding document.
/// </summary>
public sealed class EditHistoryLog
{
    public const int MaxEntries = 20;

    public const int MaxTextLength = 200;

    public static string LimitStoredText(string? text)
    {
        text ??= string.Empty;
        if (text.Length <= MaxTextLength)
        {
            return text;
        }

        return text[..(MaxTextLength - 1)] + "…";
    }

    private readonly List<EditHistoryEntry> _entries = new();

    public bool Enabled { get; set; } = true;

    public IReadOnlyList<EditHistoryEntry> Entries => _entries;

    public void Record(string? original, string? replacement, DateTime? utc = null)
    {
        if (!Enabled)
        {
            return;
        }

        original ??= string.Empty;
        replacement ??= string.Empty;
        if (original.Length == 0 && replacement.Length == 0)
        {
            return;
        }

        _entries.Insert(0, new EditHistoryEntry
        {
            Utc = utc ?? DateTime.UtcNow,
            Original = LimitStoredText(original),
            Replacement = LimitStoredText(replacement)
        });
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        }
    }

    public void Clear() => _entries.Clear();

    public void ReplaceAll(IEnumerable<EditHistoryEntry>? entries)
    {
        _entries.Clear();
        if (entries == null)
        {
            return;
        }

        foreach (var entry in entries.Take(MaxEntries))
        {
            _entries.Add(entry);
        }
    }
}
