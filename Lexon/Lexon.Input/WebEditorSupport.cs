using System.IO;

namespace Lexon.Input;

/// <summary>
/// Google Docs / Chrome / Edge / Firefox do not expose a real Win32 edit
/// control. WM_GETTEXT is usually the window title, and batched Backspace
/// is dropped. Lexon must use the keystroke buffer and select-then-type.
/// </summary>
public static class WebEditorSupport
{
    public static bool IsBrowserProcess(string? processName)
    {
        var name = Path.GetFileNameWithoutExtension(processName ?? string.Empty)
            .ToLowerInvariant();
        return name is "chrome" or "msedge" or "msedgewebview2" or "firefox"
            or "brave" or "opera" or "vivaldi" or "chromium";
    }

    public static bool IsGoogleDocs(string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return false;
        }

        return windowTitle.Contains("Google Docs", StringComparison.OrdinalIgnoreCase)
            || windowTitle.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBrowserWindowClass(string? className)
        => !string.IsNullOrEmpty(className)
           && (className.Contains("Chrome", StringComparison.OrdinalIgnoreCase)
               || className.Contains("Mozilla", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// WhatsApp Desktop is an Electron window. Its accessibility caret often
    /// points at a message bubble instead of the compose box.
    /// </summary>
    public static bool IsWhatsApp(string? processName)
    {
        var name = Path.GetFileNameWithoutExtension(processName ?? string.Empty);
        return name.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase)
            || name.Equals("WhatsApp.Root", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCodeEditorShell(string? processName)
    {
        var name = Path.GetFileNameWithoutExtension(processName ?? string.Empty)
            .ToLowerInvariant();
        return name is "cursor" or "code" or "code - insiders" or "devenv"
            or "windsurf" or "antigravity" or "zed" or "notepad++";
    }

    public static bool IsWebDocumentEditor(string? processName, string? windowTitle, string? className)
        => !IsCodeEditorShell(processName)
           && (IsBrowserProcess(processName) || IsGoogleDocs(windowTitle));

    /// <summary>
    /// True when a live WM_GETTEXT / UIA payload is the browser chrome
    /// (tab title), not the document the user is typing in.
    /// </summary>
    public static bool ShouldIgnoreLiveText(string? liveText, string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(liveText))
        {
            return true;
        }

        var text = liveText.Trim();
        var title = windowTitle?.Trim() ?? string.Empty;
        if (title.Length == 0)
        {
            return text.Contains("Google Docs", StringComparison.OrdinalIgnoreCase)
                || text.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase);
        }

        if (text.Equals(title, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (title.StartsWith(text, StringComparison.OrdinalIgnoreCase)
            && (IsGoogleDocs(title) || title.Length - text.Length < 24))
        {
            return true;
        }

        return IsGoogleDocs(text);
    }

    public static int CountWords(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return 1;
        }

        var count = phrase.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, count);
    }
}
