using System.Globalization;

namespace Lexon.Core;

public static class ApplicationName
{
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var trimmed = name.Trim();
        var file = trimmed;
        try
        {
            file = Path.GetFileName(trimmed);
        }
        catch (ArgumentException)
        {
            file = trimmed;
        }

        if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            file = file[..^4];
        }

        return file;
    }
}

public static class TextUnits
{
    public static int GraphemeCount(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }
}

public static class TextReplacement
{
    /// <summary>
    /// When a selection is still active, one Backspace deletes it. Otherwise
    /// delete one Backspace per grapheme (emoji-safe), not per UTF-16 unit.
    /// </summary>
    public static int BackspacesForReplace(string? oldText, bool selectionStillActive)
    {
        if (string.IsNullOrEmpty(oldText))
        {
            return 0;
        }

        return selectionStillActive ? 1 : TextUnits.GraphemeCount(oldText);
    }
}

/// <summary>
/// Password detection policy. Unknown answers fail closed.
/// </summary>
public static class SecureFieldPolicy
{
    public static bool IsSecure(bool nativePasswordStyle, bool? uiaIsPassword, bool controlHandleValid)
    {
        if (!controlHandleValid)
        {
            return true;
        }

        if (nativePasswordStyle)
        {
            return true;
        }

        if (uiaIsPassword == true)
        {
            return true;
        }

        if (uiaIsPassword == false)
        {
            return false;
        }

        return true;
    }
}
