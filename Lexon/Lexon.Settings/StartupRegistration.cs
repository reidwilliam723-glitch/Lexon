using Lexon.Core;
using Lexon.SettingsModel;

namespace Lexon.Settings;

internal static class StartupRegistration
{
    public static IStartupRegistration Create()
        => AppPackaging.IsPackaged
            ? new PackagedStartupRegistration()
            : new WindowsStartupRegistration();
}
