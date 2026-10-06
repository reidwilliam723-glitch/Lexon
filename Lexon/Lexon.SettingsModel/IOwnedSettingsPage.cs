using Lexon.Profiles;

namespace Lexon.SettingsModel;

public interface IOwnedSettingsPage
{
    IReadOnlyList<string> OwnedKeys { get; }

    /// <summary>
    /// True after a real user edit. Cleared by <see cref="Load"/> and after a
    /// successful dirty flush. Clean pages must never write.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// Raised after a user edit marks the page dirty and schedules persist.
    /// Never raised during <see cref="Load"/> or for non-user changes.
    /// </summary>
    event Action? UserEdited;

    void CopyOwnedTo(AppSettings target);

    void MarkClean();
}

public static class OwnedSettingsWriter
{
    /// <summary>
    /// Re-reads the profile, copies only each dirty page's owned keys, writes
    /// those keys, then clears those dirty flags. Clean pages write nothing.
    /// </summary>
    public static void Flush(Profile profile, params IOwnedSettingsPage[] pages)
    {
        var dirty = pages.Where(static p => p.IsDirty).ToArray();
        if (dirty.Length == 0)
        {
            return;
        }

        var fresh = new AppSettings();
        fresh.Read(profile);
        var keys = new List<string>();
        foreach (var page in dirty)
        {
            page.CopyOwnedTo(fresh);
            keys.AddRange(page.OwnedKeys);
        }

        fresh.WriteKeys(profile, keys.Distinct(StringComparer.Ordinal).ToArray());
        foreach (var page in dirty)
        {
            page.MarkClean();
        }
    }

    /// <summary>
    /// Fresh profile values with dirty pages' current UI values overlaid.
    /// Does not write. Used for immediate live effects before a debounced save.
    /// </summary>
    public static AppSettings BuildLiveSnapshot(Profile profile, IEnumerable<IOwnedSettingsPage> pages)
    {
        var snapshot = new AppSettings();
        snapshot.Read(profile);
        foreach (var page in pages)
        {
            if (page.IsDirty)
            {
                page.CopyOwnedTo(snapshot);
            }
        }

        return snapshot;
    }
}
