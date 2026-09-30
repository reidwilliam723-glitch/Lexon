using Lexon.Core;
using Lexon.Core.Models;

namespace Lexon.SettingsModel;

public static class AppToneList
{
    public static List<string> AddOrReplace(IEnumerable<string> rows, string processName, AppWritingCategory category)
    {
        var app = AppCategoryMapper.EnsureExeExtension(processName);
        if (string.IsNullOrEmpty(app))
        {
            return rows.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        var next = rows
            .Where(row =>
            {
                if (!AppCategoryMapper.TryParseRow(row, out var existing, out _))
                {
                    return !string.IsNullOrWhiteSpace(row);
                }

                return existing != app;
            })
            .ToList();
        next.Add(AppCategoryMapper.FormatRow(app, category));
        return next;
    }

    public static List<string> RemoveAt(IEnumerable<string> rows, int index)
    {
        var list = rows.ToList();
        if (index >= 0 && index < list.Count)
        {
            list.RemoveAt(index);
        }

        return list;
    }
}
