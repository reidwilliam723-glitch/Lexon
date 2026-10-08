using Lexon.Core;
using Lexon.Core.Models;

namespace Lexon.SettingsModel;

public readonly record struct AppControlState(
    string App,
    bool BlockAssistance,
    string Tone,
    bool Grammar,
    bool LearnedWords)
{
    public const string DefaultTone = "Default";
}

/// <summary>
/// One view of the existing per-app lists: blocked apps, tone overrides,
/// muted grammar, and muted learned words.
/// </summary>
public static class AppControlEditor
{
    public static IReadOnlyList<string> ToneChoices { get; } =
        [AppControlState.DefaultTone, "Casual", "Formal", "Code", "Neutral"];

    public static IReadOnlyList<string> Apps(AppSettings settings)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Add(names, settings.BlockedApplications);
        if (settings.AppCategoryOverrides != null)
        {
            foreach (var row in settings.AppCategoryOverrides)
            {
                if (AppCategoryMapper.TryParseRow(row, out var app, out _))
                {
                    Add(names, [app]);
                }
            }
        }

        Add(names, settings.GrammarMutedApps);
        Add(names, settings.LearnedWordsMutedApps);
        return names.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static AppControlState Read(AppSettings settings, string? app)
    {
        var name = ApplicationName.Normalize(app);
        var tone = AppControlState.DefaultTone;
        var exe = AppCategoryMapper.EnsureExeExtension(name);
        if (settings.AppCategoryOverrides != null)
        {
            foreach (var row in settings.AppCategoryOverrides)
            {
                if (AppCategoryMapper.TryParseRow(row, out var existing, out var category)
                    && string.Equals(existing, exe, StringComparison.OrdinalIgnoreCase))
                {
                    tone = category.ToString();
                    break;
                }
            }
        }

        return new AppControlState(
            name,
            Contains(settings.BlockedApplications, name, ApplicationName.Normalize),
            tone,
            !Contains(settings.GrammarMutedApps, name, ApplicationName.Normalize),
            !Contains(settings.LearnedWordsMutedApps, name, ApplicationName.Normalize));
    }

    public static void Apply(AppSettings settings, AppControlState state)
    {
        var name = ApplicationName.Normalize(state.App);
        if (name.Length == 0)
        {
            return;
        }

        settings.BlockedApplications = SetFlag(
            settings.BlockedApplications,
            name,
            state.BlockAssistance,
            ApplicationName.Normalize,
            static stored => stored);

        var exe = AppCategoryMapper.EnsureExeExtension(name);
        var rows = (settings.AppCategoryOverrides ?? []).Where(row =>
        {
            if (!AppCategoryMapper.TryParseRow(row, out var existing, out _))
            {
                return !string.IsNullOrWhiteSpace(row);
            }

            return !string.Equals(existing, exe, StringComparison.OrdinalIgnoreCase);
        }).ToList();
        if (!string.Equals(state.Tone, AppControlState.DefaultTone, StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<AppWritingCategory>(state.Tone, true, out var category))
        {
            rows.Add(AppCategoryMapper.FormatRow(name, category));
        }

        settings.AppCategoryOverrides = rows;
        settings.GrammarMutedApps = SetFlag(
            settings.GrammarMutedApps,
            exe,
            !state.Grammar,
            ApplicationName.Normalize,
            static stored => stored);
        settings.LearnedWordsMutedApps = SetFlag(
            settings.LearnedWordsMutedApps,
            name,
            !state.LearnedWords,
            ApplicationName.Normalize,
            static stored => stored);
    }

    private static void Add(HashSet<string> names, IEnumerable<string>? source)
    {
        if (source == null)
        {
            return;
        }

        foreach (var item in source)
        {
            var name = ApplicationName.Normalize(item);
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }
    }

    private static bool Contains(IEnumerable<string>? source, string app, Func<string?, string> normalize)
    {
        if (source == null)
        {
            return false;
        }

        foreach (var item in source)
        {
            if (string.Equals(normalize(item), app, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> SetFlag(
        IEnumerable<string>? source,
        string storedName,
        bool include,
        Func<string?, string> normalize,
        Func<string, string> keep)
    {
        var next = new List<string>();
        if (source != null)
        {
            foreach (var item in source)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                if (string.Equals(normalize(item), normalize(storedName), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                next.Add(keep(item));
            }
        }

        if (include)
        {
            next.Add(storedName);
        }

        return next;
    }
}
