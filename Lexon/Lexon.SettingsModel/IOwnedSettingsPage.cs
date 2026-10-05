using Lexon.Profiles;

namespace Lexon.SettingsModel;

public interface IOwnedSettingsPage
{
    IReadOnlyList<string> OwnedKeys { get; }

    void CopyOwnedTo(AppSettings target);
}

public static class OwnedSettingsWriter
{
    /// <summary>
    /// Re-reads the profile, copies only each page's owned keys, then writes
    /// those keys. Other profile values stay byte-for-byte the same.
    /// </summary>
    public static void Flush(Profile profile, params IOwnedSettingsPage[] pages)
    {
        var fresh = new AppSettings();
        fresh.Read(profile);
        var keys = new List<string>();
        foreach (var page in pages)
        {
            page.CopyOwnedTo(fresh);
            keys.AddRange(page.OwnedKeys);
        }

        fresh.WriteKeys(profile, keys.Distinct(StringComparer.Ordinal).ToArray());
    }
}
