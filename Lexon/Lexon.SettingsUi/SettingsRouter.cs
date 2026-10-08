namespace Lexon.SettingsUi;

public enum SettingsEntryPoint
{
    Tray,
    Shortcut,
    ShowEvent,
    Warm,
    GalleryMenu
}

public enum SettingsSurface
{
    SettingsWindow,
    SettingsForm,
    Gallery
}

/// <summary>
/// Decides which settings UI a request opens. Two failed WPF opens in one
/// session stay on the classic form.
/// </summary>
public sealed class SettingsRouter
{
    public SettingsRouter(bool classicSettings, bool galleryEnabled)
    {
        ClassicSettings = classicSettings;
        GalleryEnabled = galleryEnabled;
    }

    public bool ClassicSettings { get; }

    public bool GalleryEnabled { get; }

    public int WpfFailures { get; private set; }

    public bool WpfGaveUp => WpfFailures >= 2;

    public bool ShouldWarmWpf => !ClassicSettings && !WpfGaveUp;

    public SettingsSurface Resolve(SettingsEntryPoint entry)
    {
        if (entry == SettingsEntryPoint.GalleryMenu && GalleryEnabled)
        {
            return SettingsSurface.Gallery;
        }

        if (ClassicSettings || WpfGaveUp)
        {
            return SettingsSurface.SettingsForm;
        }

        return SettingsSurface.SettingsWindow;
    }

    public void RecordWpfFailure() => WpfFailures++;

    public SettingsSurface OpenOrFallback(SettingsEntryPoint entry, Func<SettingsSurface> tryWpf)
    {
        var target = Resolve(entry);
        if (target != SettingsSurface.SettingsWindow)
        {
            return target;
        }

        try
        {
            return tryWpf();
        }
        catch (Exception)
        {
            RecordWpfFailure();
            return SettingsSurface.SettingsForm;
        }
    }
}
