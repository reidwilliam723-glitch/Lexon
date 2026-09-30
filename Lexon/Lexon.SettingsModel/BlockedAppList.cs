using Lexon.Core;

namespace Lexon.SettingsModel;

public static class BlockedAppList
{
    public static List<string> Parse(string? csv)
        => NormalizeMany(
            (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static List<string> ParseMutedGrammar(string? csv)
        => (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    public static List<string> NormalizeMany(IEnumerable<string> names)
        => names
            .Select(ApplicationName.Normalize)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool TryAdd(IList<string> current, string? processName, out string normalized)
    {
        normalized = ApplicationName.Normalize(processName);
        if (normalized.Length == 0)
        {
            return false;
        }

        if (current.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        current.Add(normalized);
        return true;
    }

    public static string FormatCsv(IEnumerable<string> names) => string.Join(", ", names);
}
