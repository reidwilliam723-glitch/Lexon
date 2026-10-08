using Lexon.Core.Models;

namespace Lexon.Core.Grammar;

/// <summary>
/// Custom terminology protected from autocorrect and spelling grammar.
/// Global terms apply everywhere; app rows are <c>process.exe|TermOne|TermTwo</c>.
/// </summary>
public static class TerminologyList
{
    public static string Normalize(string? word)
        => string.IsNullOrWhiteSpace(word) ? string.Empty : word.Trim();

    public static bool IsProtected(
        string? word,
        IEnumerable<string>? globalTerms,
        IEnumerable<string>? appTerms)
    {
        var key = Normalize(word);
        if (key.Length == 0)
        {
            return false;
        }

        return ContainsIgnoreCase(globalTerms, key) || ContainsIgnoreCase(appTerms, key);
    }

    public static List<string> ParseGlobal(IEnumerable<string>? fromSettings)
    {
        var result = new List<string>();
        if (fromSettings == null)
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in fromSettings)
        {
            var term = Normalize(item);
            if (term.Length == 0 || term.Contains('|') || !seen.Add(term))
            {
                continue;
            }

            result.Add(term);
        }

        return result;
    }

    public static Dictionary<string, List<string>> ParseAppRows(IEnumerable<string>? rows)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (rows == null)
        {
            return result;
        }

        foreach (var row in rows)
        {
            if (!TryParseAppRow(row, out var app, out var terms))
            {
                continue;
            }

            if (!result.TryGetValue(app, out var existing))
            {
                result[app] = terms.ToList();
                continue;
            }

            var seen = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            foreach (var term in terms)
            {
                if (seen.Add(term))
                {
                    existing.Add(term);
                }
            }
        }

        return result;
    }

    public static bool TryParseAppRow(string? row, out string app, out IReadOnlyList<string> terms)
    {
        app = string.Empty;
        terms = [];
        if (string.IsNullOrWhiteSpace(row))
        {
            return false;
        }

        var parts = row.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        app = AppCategoryMapper.EnsureExeExtension(parts[0]);
        if (string.IsNullOrEmpty(app))
        {
            return false;
        }

        var parsed = ParseGlobal(parts.Skip(1));
        if (parsed.Count == 0)
        {
            app = string.Empty;
            return false;
        }

        terms = parsed;
        return true;
    }

    public static string FormatAppRow(string app, IEnumerable<string> terms)
    {
        var exe = AppCategoryMapper.EnsureExeExtension(app);
        var termParts = ParseGlobal(terms);
        if (string.IsNullOrEmpty(exe) || termParts.Count == 0)
        {
            return string.Empty;
        }

        return exe + "|" + string.Join("|", termParts);
    }

    /// <summary>
    /// Combined protected set for the current process: global terms plus any matching app override.
    /// </summary>
    public static HashSet<string> ResolveTerms(
        string? processName,
        IEnumerable<string>? globalList,
        IEnumerable<string>? appOverrideRows)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in ParseGlobal(globalList))
        {
            set.Add(term);
        }

        var appKey = AppCategoryMapper.EnsureExeExtension(processName);
        if (string.IsNullOrEmpty(appKey))
        {
            return set;
        }

        var map = ParseAppRows(appOverrideRows);
        if (map.TryGetValue(appKey, out var appTerms))
        {
            foreach (var term in appTerms)
            {
                set.Add(term);
            }
        }

        return set;
    }

    private static bool ContainsIgnoreCase(IEnumerable<string>? terms, string key)
    {
        if (terms == null)
        {
            return false;
        }

        foreach (var term in terms)
        {
            if (string.Equals(Normalize(term), key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
