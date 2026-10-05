using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

/// <summary>
/// Live settings for the gallery General tab. Null on <c>ControlGalleryWindow</c>
/// hides that tab and keeps the 1.2.0 gallery.
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

    /// <summary>
    /// Re-read <see cref="Settings"/> from the profile. The host supplies this
    /// because the view model does not take a <c>Profile</c>.
    /// </summary>
    public Action Reload { get; init; } = static () => { };

    /// <summary>
    /// Write current <see cref="Settings"/> into the in-memory profile so the
    /// classic form sees changes before the debounced disk save.
    /// </summary>
    public Action Push { get; init; } = static () => { };
}
