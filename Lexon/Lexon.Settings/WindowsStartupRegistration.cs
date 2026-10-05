using Lexon.SettingsModel;

namespace Lexon.Settings;

/// <summary>
/// Thin wrapper over <see cref="WindowsStartup"/>. <c>SetEnabled</c> is void
/// and swallows registry errors, so success is the post-write Run-key state.
/// </summary>
internal sealed class WindowsStartupRegistration : IStartupRegistration
{
    public bool IsEnabled() => WindowsStartup.IsEnabled();

    public bool TrySetEnabled(bool enabled)
    {
        WindowsStartup.SetEnabled(enabled);
        return WindowsStartup.IsEnabled() == enabled;
    }
}
