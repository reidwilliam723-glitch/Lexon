using System.Text.Json;
using Lexon.Core;
using Lexon.SettingsModel;

namespace Lexon.Settings;

public sealed class LocalEditHistory : IEditHistory
{
    private readonly EditHistoryLog _log = new();
    private readonly string _path;

    public LocalEditHistory(string path)
    {
        _path = path;
        Load();
    }

    public event EventHandler? Changed;

    public Func<bool>? UndoAvailable { get; set; }

    public Action? PerformUndo { get; set; }

    public bool Enabled
    {
        get => _log.Enabled;
        set => _log.Enabled = value;
    }

    public IReadOnlyList<EditHistoryEntry> Entries => _log.Entries;

    public bool CanUndoLatest => UndoAvailable?.Invoke() == true && _log.Entries.Count > 0;

    public void Record(string? original, string? replacement, DateTime utc)
    {
        var before = _log.Entries.Count;
        _log.Record(original, replacement, utc);
        if (_log.Entries.Count == before && (before == 0 || !_log.Enabled))
        {
            return;
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _log.Clear();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UndoLatest()
    {
        if (!CanUndoLatest)
        {
            return;
        }

        PerformUndo?.Invoke();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var json = File.ReadAllText(_path);
            var stored = JsonSerializer.Deserialize<List<StoredEdit>>(json);
            _log.ReplaceAll(stored?.Select(item => new EditHistoryEntry
            {
                Utc = item.Utc,
                Original = item.Original ?? string.Empty,
                Replacement = item.Replacement ?? string.Empty
            }));
        }
        catch
        {
            _log.Clear();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var stored = _log.Entries.Select(entry => new StoredEdit
            {
                Utc = entry.Utc,
                Original = entry.Original,
                Replacement = entry.Replacement
            }).ToList();
            File.WriteAllText(_path, JsonSerializer.Serialize(stored));
        }
        catch
        {
            // A full disk should not stop suggestions.
        }
    }

    private sealed class StoredEdit
    {
        public DateTime Utc { get; set; }

        public string? Original { get; set; }

        public string? Replacement { get; set; }
    }
}
