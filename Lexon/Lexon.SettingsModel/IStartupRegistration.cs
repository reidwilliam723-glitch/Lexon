namespace Lexon.SettingsModel;

/// <summary>
/// Start-with-Windows registration. Unpackaged uses the Run key; packaged uses a StartupTask.
/// </summary>
public interface IStartupRegistration
{
    bool IsEnabled();

    bool TrySetEnabled(bool enabled);

    StartupAvailability Availability { get; }

    /// <summary>
    /// Short status when the toggle cannot be changed from inside Lexon.
    /// </summary>
    string? StatusHint { get; }
}
