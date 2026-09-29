using Lexon.Core.Interfaces;

namespace Lexon.Core;

/// <summary>
/// Local-only record of when text was sent to a cloud AI provider.
/// Does not store the text itself.
/// </summary>
public sealed class CloudAiActivityEntry
{
    public DateTime Utc { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

public sealed class CloudAiActivityLog
{
    public const string StorageKey = "cloud_ai_activity_log";
    public const int MaxEntries = 200;

    private readonly IStorage? _storage;
    private readonly List<CloudAiActivityEntry> _entries = [];
    private readonly object _lock = new();

    private readonly Dictionary<string, DateTime> _lastSuggestUtc = new(StringComparer.OrdinalIgnoreCase);

    public CloudAiActivityLog(IStorage? storage = null)
    {
        _storage = storage;
    }

    public IReadOnlyList<CloudAiActivityEntry> Snapshot()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    public void Record(string? provider, string? applicationName, string action)
    {
        var entry = new CloudAiActivityEntry
        {
            Utc = DateTime.UtcNow,
            Provider = string.IsNullOrWhiteSpace(provider) ? "unknown" : provider.Trim(),
            ApplicationName = string.IsNullOrWhiteSpace(applicationName) ? "(unknown app)" : applicationName.Trim(),
            Action = string.IsNullOrWhiteSpace(action) ? "rewrite" : action.Trim()
        };

        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
            }
        }

        _ = PersistAsync();
    }

    /// <summary>
    /// Records a typing-suggestion send. Cloud providers only. At most one
    /// entry per app per minute so the log is not flushed by keystrokes.
    /// </summary>
    public bool TryRecordSuggest(string? provider, string? applicationName)
    {
        if (!CloudAiNames.IsCloud(provider))
        {
            return false;
        }

        var app = string.IsNullOrWhiteSpace(applicationName) ? "(unknown app)" : applicationName.Trim();
        lock (_lock)
        {
            if (_lastSuggestUtc.TryGetValue(app, out var last)
                && DateTime.UtcNow - last < TimeSpan.FromMinutes(1))
            {
                return false;
            }

            _lastSuggestUtc[app] = DateTime.UtcNow;
        }

        Record(provider, app, "suggest");
        return true;
    }

    public async Task LoadAsync()
    {
        if (_storage == null)
        {
            return;
        }

        var loaded = await _storage.LoadAsync<List<CloudAiActivityEntry>>(StorageKey);
        if (loaded is { Count: > 0 })
        {
            lock (_lock)
            {
                _entries.Clear();
                _entries.AddRange(loaded.TakeLast(MaxEntries));
            }
        }
    }

    private async Task PersistAsync()
    {
        if (_storage == null)
        {
            return;
        }

        List<CloudAiActivityEntry> copy;
        lock (_lock)
        {
            copy = _entries.ToList();
        }

        try
        {
            await _storage.SaveAsync(StorageKey, copy);
        }
        catch
        {
            // Logging must never interrupt a rewrite.
        }
    }
}
