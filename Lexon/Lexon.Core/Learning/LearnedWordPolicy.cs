namespace Lexon.Core.Learning;

/// <summary>
/// Whether learned vocabulary may be offered in the current app.
/// </summary>
public static class LearnedWordPolicy
{
    public static bool Allows(bool enabled, IEnumerable<string>? mutedApps, string? applicationName)
    {
        if (!enabled)
        {
            return false;
        }

        if (mutedApps == null)
        {
            return true;
        }

        var app = ApplicationName.Normalize(applicationName);
        if (app.Length == 0)
        {
            return true;
        }

        foreach (var name in mutedApps)
        {
            if (string.Equals(ApplicationName.Normalize(name), app, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
