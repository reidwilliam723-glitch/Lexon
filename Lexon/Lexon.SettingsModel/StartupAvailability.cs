namespace Lexon.SettingsModel;

/// <summary>
/// Whether the app can change the "Start with Windows" setting itself.
/// </summary>
public enum StartupAvailability
{
    /// <summary>The app can enable or disable startup.</summary>
    Available,

    /// <summary>Windows Settings / Task Manager turned it off; the app cannot re-enable it.</summary>
    DisabledByUser,

    /// <summary>Group policy or admin policy blocks startup.</summary>
    DisabledByPolicy,

    /// <summary>Startup registration is not available on this install.</summary>
    Unavailable
}
