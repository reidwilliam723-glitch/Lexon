using System.Windows;
using Lexon.Profiles;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

/// <summary>
/// Live settings for gallery tabs. Null on <c>ControlGalleryWindow</c>
/// hides every settings tab and keeps the 1.2.0 gallery. Optional members
/// hide only the tabs that need them.
/// </summary>
public sealed class GallerySettingsServices
{
    public GallerySettingsServices(
        AppSettings settings,
        IStartupRegistration startup,
        PersistScheduler persist,
        Action save)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Startup = startup ?? throw new ArgumentNullException(nameof(startup));
        Persist = persist ?? throw new ArgumentNullException(nameof(persist));
        Save = save ?? throw new ArgumentNullException(nameof(save));
    }

    public AppSettings Settings { get; }

    public IStartupRegistration Startup { get; }

    public PersistScheduler Persist { get; }

    public Action Save { get; }

    public Action Reload { get; init; } = static () => { };

    public Action Push { get; init; } = static () => { };

    public Action? ApplyLive { get; init; }

    public Profile? Profile { get; init; }

    public IThemeSwitcher? ThemeSwitcher { get; init; }

    public IAiPolicyPublisher? AiPolicy { get; init; }

    public IProcessPicker? ProcessPicker { get; init; }

    public ICloudAiActivityViewer? ActivityViewer { get; init; }

    public Action<Window>? AttachOwner { get; init; }

    public IList<IOwnedSettingsPage> Pages { get; } = [];
}
