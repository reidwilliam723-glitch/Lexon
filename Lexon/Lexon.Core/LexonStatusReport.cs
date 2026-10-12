namespace Lexon.Core;

public sealed class LexonStatusSnapshot
{
    public string Version { get; init; } = string.Empty;

    public bool IsRunning { get; init; }

    public bool IsEnabled { get; init; }

    public string CurrentProcess { get; init; } = string.Empty;

    public bool FocusedWindow { get; init; }

    public bool SecureField { get; init; }

    public bool AppPaused { get; init; }

    public bool QuietHours { get; init; }

    public string Spelling { get; init; } = string.Empty;

    public bool HistoryEnabled { get; init; }

    public int HistoryCount { get; init; }
}

/// <summary>
/// Help text a person can copy. It names the app and the switches, not what was typed.
/// </summary>
public static class LexonStatusReport
{
    public static string Format(LexonStatusSnapshot status)
    {
        var canInteract = status.IsRunning
            && status.IsEnabled
            && status.FocusedWindow
            && !status.SecureField
            && !status.AppPaused
            && !status.QuietHours;
        var reason = !status.IsRunning ? "Lexon is not running."
            : !status.IsEnabled ? "Lexon is paused."
            : !status.FocusedWindow ? "No app is in focus."
            : status.SecureField ? "The focused field is hidden, such as a password box."
            : status.AppPaused ? "Lexon is paused in the focused app."
            : status.QuietHours ? "Quiet hours are on."
            : "Lexon can offer suggestions in the focused app.";

        return string.Join(Environment.NewLine,
        [
            "Lexon check",
            $"Version: {status.Version}",
            $"Windows: {Environment.OSVersion.VersionString}",
            $"Running: {Yes(status.IsRunning)}",
            $"Enabled: {Yes(status.IsEnabled)}",
            $"Focused process: {(string.IsNullOrWhiteSpace(status.CurrentProcess) ? "(none)" : status.CurrentProcess)}",
            $"Can interact: {Yes(canInteract)}",
            $"Reason: {reason}",
            $"Spelling: {status.Spelling}",
            $"Quiet hours: {Yes(status.QuietHours)}",
            $"Edit history: {(status.HistoryEnabled ? status.HistoryCount.ToString() : "off")}",
            "This report does not include typed text, document names, or window titles."
        ]);
    }

    public static bool CanInteract(LexonStatusSnapshot status)
        => status.IsRunning
            && status.IsEnabled
            && status.FocusedWindow
            && !status.SecureField
            && !status.AppPaused
            && !status.QuietHours;

    private static string Yes(bool value) => value ? "yes" : "no";
}
