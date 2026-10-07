namespace Lexon.Core.Models;

public enum AppWritingCategory
{
    Neutral,
    Casual,
    Formal,
    Code
}

public static class AppCategoryMapper
{
    private static readonly Dictionary<string, AppWritingCategory> BuiltIn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["slack.exe"] = AppWritingCategory.Casual,
        ["discord.exe"] = AppWritingCategory.Casual,
        ["teams.exe"] = AppWritingCategory.Casual,
        ["ms-teams.exe"] = AppWritingCategory.Casual,
        ["outlook.exe"] = AppWritingCategory.Formal,
        ["winword.exe"] = AppWritingCategory.Formal,
        ["excel.exe"] = AppWritingCategory.Formal,
        ["powerpnt.exe"] = AppWritingCategory.Formal,
        ["code.exe"] = AppWritingCategory.Code,
        ["devenv.exe"] = AppWritingCategory.Code,
        ["idea64.exe"] = AppWritingCategory.Code,
        ["rider64.exe"] = AppWritingCategory.Code,
        ["cursor.exe"] = AppWritingCategory.Code,
        ["windowsterminal.exe"] = AppWritingCategory.Code,
        ["wt.exe"] = AppWritingCategory.Code,
        ["powershell.exe"] = AppWritingCategory.Code,
        ["pwsh.exe"] = AppWritingCategory.Code,
        ["cmd.exe"] = AppWritingCategory.Code
    };

    public static AppWritingCategory Resolve(string? processName, IReadOnlyDictionary<string, AppWritingCategory>? overrides = null)
    {
        var key = Normalize(processName);
        if (string.IsNullOrEmpty(key))
        {
            return AppWritingCategory.Neutral;
        }

        if (overrides != null && overrides.TryGetValue(key, out var mapped))
        {
            return mapped;
        }

        return BuiltIn.TryGetValue(key, out var builtIn) ? builtIn : AppWritingCategory.Neutral;
    }

    public static IReadOnlyDictionary<string, AppWritingCategory> BuiltInMappings => BuiltIn;

    public static string ToneHint(AppWritingCategory category) => category switch
    {
        AppWritingCategory.Casual => "casual, conversational, short",
        AppWritingCategory.Formal => "formal, professional, complete sentences",
        AppWritingCategory.Code => "concise, technical, suitable for code comments",
        _ => string.Empty
    };

    public static Dictionary<string, AppWritingCategory> ParseOverrides(IEnumerable<string>? rows)
    {
        var result = new Dictionary<string, AppWritingCategory>(StringComparer.OrdinalIgnoreCase);
        if (rows == null)
        {
            return result;
        }

        foreach (var row in rows)
        {
            if (!TryParseRow(row, out var app, out var category))
            {
                continue;
            }

            result[app] = category;
        }

        return result;
    }

    public static bool TryParseRow(string? row, out string app, out AppWritingCategory category)
    {
        app = string.Empty;
        category = AppWritingCategory.Neutral;
        if (string.IsNullOrWhiteSpace(row))
        {
            return false;
        }

        var parts = row.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        app = Normalize(parts[0]);
        if (string.IsNullOrEmpty(app) || !Enum.TryParse(parts[1], true, out category))
        {
            return false;
        }

        return true;
    }

    public static string FormatRow(string app, AppWritingCategory category) => $"{EnsureExeExtension(app)}|{category}";

    public static string Normalize(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        var name = processName.Trim();
        var slash = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
        if (slash >= 0 && slash < name.Length - 1)
        {
            name = name[(slash + 1)..];
        }

        return name.ToLowerInvariant();
    }

    public static string EnsureExeExtension(string? processName)
    {
        var name = Normalize(processName);
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        while (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return string.IsNullOrEmpty(name) ? string.Empty : name + ".exe";
    }
}
