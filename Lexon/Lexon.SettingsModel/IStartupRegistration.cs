namespace Lexon.SettingsModel;

/// <summary>
/// Registry Run-key startup. The real wrapper lives in the exe because
/// <c>WindowsStartup</c> is there.
/// </summary>
public interface IStartupRegistration
{
    bool IsEnabled();

    bool TrySetEnabled(bool enabled);
}
