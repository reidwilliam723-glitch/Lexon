using Lexon.Core;

namespace Lexon.SettingsModel;

public interface IEditHistory
{
    bool Enabled { get; set; }

    IReadOnlyList<EditHistoryEntry> Entries { get; }

    event EventHandler? Changed;

    void Clear();

    bool CanUndoLatest { get; }

    void UndoLatest();
}
