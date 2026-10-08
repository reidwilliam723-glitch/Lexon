using System.Diagnostics;
using Lexon.Core.Theming;
using Lexon.SettingsUi;
using Lexon.Storage;

namespace Lexon.Settings;

internal static class UiOpenTiming
{
    public static void Run()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ui-timing.txt"));
        void Log(string line) => File.AppendAllText(path, line + Environment.NewLine);
        File.WriteAllText(path, string.Empty);
        try
        {
            Log("started");
            var storage = new EncryptedStorage(Path.Combine(Path.GetTempPath(), "LexonUiTiming"));
            var themes = new ThemeManager(storage);
            themes.InitializeAsync().GetAwaiter().GetResult();
            Log("themes ready");

            var profile = new Lexon.Profiles.Profile(storage);
            var coldForm = Stopwatch.StartNew();
            using var form = new SettingsForm(profile, null, storage, themes);
            form.CreateControl();
            coldForm.Stop();
            Log($"SettingsForm cold (ctor+CreateControl): {coldForm.ElapsedMilliseconds} ms");

            var firstShow = Stopwatch.StartNew();
            form.Show();
            Application.DoEvents();
            firstShow.Stop();
            form.Hide();
            Application.DoEvents();
            Log($"SettingsForm first Show: {firstShow.ElapsedMilliseconds} ms");

            var warmShow = Stopwatch.StartNew();
            form.Show();
            Application.DoEvents();
            warmShow.Stop();
            form.Hide();
            Log($"SettingsForm warm Show: {warmShow.ElapsedMilliseconds} ms");

            var beforeWpf = Process.GetCurrentProcess().WorkingSet64;
            var persist = new Lexon.SettingsModel.PersistScheduler(() => { });
            var services = new GallerySettingsServices(
                new Lexon.SettingsModel.AppSettings(),
                new WindowsStartupRegistration(),
                persist,
                persist.Flush);
            var (coldSettings, warmSettings) = SettingsUiHost.MeasureSettingsOpen(themes, services);
            var afterWpf = Process.GetCurrentProcess().WorkingSet64;
            Log($"WPF settings cold (first Show): {coldSettings} ms");
            Log($"WPF settings warm (second Show): {warmSettings} ms");
            Log($"Working set before WPF settings: {beforeWpf / (1024 * 1024)} MB");
            Log($"Working set after WPF settings: {afterWpf / (1024 * 1024)} MB");
            Log($"Working set delta: {(afterWpf - beforeWpf) / (1024 * 1024)} MB");

            var (coldWpf, warmWpf) = SettingsUiHost.MeasureGalleryOpen(themes);
            Log($"WPF gallery cold (first Show): {coldWpf} ms");
            Log($"WPF gallery warm (second Show): {warmWpf} ms");
            SettingsUiHost.Shutdown();
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            try { SettingsUiHost.Shutdown(); } catch { /* ignore */ }
            Environment.Exit(1);
        }

        Environment.Exit(0);
    }
}
