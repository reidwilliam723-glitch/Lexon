using Velopack;
using Velopack.Sources;

namespace Lexon.Settings;

/// <summary>
/// Velopack-backed update check. Downloads in the background and only ever
/// restarts the app after the person explicitly confirms the prompt.
/// </summary>
internal static class UpdateChecker
{
    /// <summary>
    /// Repository that hosts the Lexon releases consumed by Velopack. The
    /// packaging script (Settings\release.ps1) uploads to this same repo.
    /// </summary>
    private const string RepositoryUrl = "https://github.com/reidwilliam723-glitch/Lexon";

    private static readonly SemaphoreSlim CheckGate = new(1, 1);

    /// <summary>
    /// Fire-and-forget check used at startup. Stays silent unless an update was
    /// downloaded and is ready to install.
    /// </summary>
    public static Task CheckOnStartupAsync(Action<Action> invokeOnUi, Action requestShutdown)
        => RunAsync(invokeOnUi, requestShutdown, interactive: false);

    /// <summary>
    /// Check triggered by the tray menu. Always reports an outcome so the
    /// person knows the click did something.
    /// </summary>
    public static Task CheckInteractiveAsync(Action<Action> invokeOnUi, Action requestShutdown)
        => RunAsync(invokeOnUi, requestShutdown, interactive: true);

    private static async Task RunAsync(Action<Action> invokeOnUi, Action requestShutdown, bool interactive)
    {
        // A startup check and a tray click can overlap; let the first one finish.
        if (!await CheckGate.WaitAsync(interactive ? TimeSpan.FromSeconds(30) : TimeSpan.Zero))
        {
            return;
        }

        try
        {
            // GithubSource uses the releases list API, which currently returns
            // no assets for the newest Lexon tag. latest/download has the files.
            var manager = new UpdateManager(new SimpleWebSource(
                $"{RepositoryUrl}/releases/latest/download"));

            if (!manager.IsInstalled)
            {
                // Portable/dev build: there is no Velopack install to update.
                if (interactive)
                {
                    Report(invokeOnUi,
                        "This copy of Lexon was not installed with the Lexon installer, so it cannot update itself.\n\n" +
                        "Install the version from Lexon.exe to get automatic updates.");
                }

                return;
            }

            var update = await manager.CheckForUpdatesAsync();
            if (update == null)
            {
                if (interactive)
                {
                    Report(invokeOnUi, $"Lexon {manager.CurrentVersion} is up to date.");
                }

                return;
            }

            await manager.DownloadUpdatesAsync(update);

            // Prompt instead of restarting silently — the person may be mid-sentence.
            invokeOnUi(() =>
            {
                var answer = MessageBox.Show(
                    $"Lexon {update.TargetFullRelease.Version} is ready to install. Restart now?",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (answer != DialogResult.Yes)
                {
                    return;
                }

                // Stage the updater to wait for this process to exit, then leave
                // through the app's own shutdown so the service stops and the tray
                // icon is removed. ApplyUpdatesAndRestart terminates the process
                // outright, which strands a dead tray icon beside the new instance.
                manager.WaitExitThenApplyUpdates(update, silent: false, restart: true);
                requestShutdown();
            });
        }
        catch (Exception ex)
        {
            // An unreachable feed must never crash or block the app.
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            if (interactive)
            {
                Report(invokeOnUi, $"Could not check for updates right now.\n\n{ex.Message}");
            }
        }
        finally
        {
            CheckGate.Release();
        }
    }

    private static void Report(Action<Action> invokeOnUi, string message)
    {
        invokeOnUi(() => MessageBox.Show(
            message,
            "Lexon updates",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information));
    }
}
