namespace Lexon.SettingsUi;

/// <summary>
/// Opens Settings through the router. A WPF failure is recorded, reported,
/// and that request falls back to the classic form. After two failures the
/// router stops trying WPF, so later requests do not report an error.
/// </summary>
public static class SettingsOpenFlow
{
    public static void Open(
        SettingsRouter router,
        SettingsEntryPoint entry,
        Action showWpf,
        Action showClassic,
        Action<Exception>? reportError)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(showWpf);
        ArgumentNullException.ThrowIfNull(showClassic);

        if (router.Resolve(entry) == SettingsSurface.SettingsWindow)
        {
            try
            {
                showWpf();
                return;
            }
            catch (Exception ex)
            {
                router.RecordWpfFailure();
                reportError?.Invoke(ex);
            }
        }

        showClassic();
    }
}
