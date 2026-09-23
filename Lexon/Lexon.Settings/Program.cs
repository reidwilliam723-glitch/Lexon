using System.Diagnostics;
using Lexon.Service;
using Lexon.Onboarding.Wizard;
using Lexon.Profiles;
using Lexon.Core;
using Lexon.Core.Interfaces;
using Lexon.Core.Models;
using Lexon.Ui;
using Velopack;

namespace Lexon.Settings;

static class Program
{
    private const string MutexName = @"Global\Lexon_SingleInstance_Mutex";
    private const string MutexNameLocal = @"Local\Lexon_SingleInstance_Mutex";
    private const string ShowSettingsEventName = @"Global\Lexon_ShowSettings_Event";
    private const string ShowSettingsEventNameLocal = @"Local\Lexon_ShowSettings_Event";

    private static LexonServiceComposer.CompositionResult? _composition;
    private static SystemTrayManager? _trayManager;
    private static SettingsForm? _settingsForm;
    private static KeyboardShortcutsForm? _shortcutsForm;
    private static CrashReporter? _crashReporter;
    private static Mutex? _instanceMutex;
    private static EventWaitHandle? _showSettingsEvent;
    private static Thread? _showSettingsWaiter;
    private static volatile bool _shuttingDown;
    private static System.Windows.Forms.Timer? _pauseTimer;
    private static AboutForm? _aboutForm;
    private static bool _restartedAfterCrash;
    private static bool _suppressQuickPause;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // Must be the first thing that runs: Velopack intercepts the install,
        // update, and uninstall hook arguments here and exits on its own.
        VelopackApp.Build().Run();

        if (args.Any(argument => argument.Equals("--paint-probe", StringComparison.OrdinalIgnoreCase)))
        {
            SettingsPaintProbe.Run();
            return;
        }

        if (args.Any(argument => argument.Equals("--write-icon", StringComparison.OrdinalIgnoreCase)))
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Lexon.ico");
            iconPath = Path.GetFullPath(iconPath);
            LexonIconFactory.WriteIco(iconPath);
            return;
        }

        _restartedAfterCrash = args.Any(argument => argument.Equals("--after-crash", StringComparison.OrdinalIgnoreCase));

        if (!TryBecomeSingleInstance())
        {
            SignalExistingInstanceToShowSettings();
            return;
        }

        // Initialize crash reporter
        _crashReporter = new CrashReporter("Lexon");

        // Check Group Policy for crash reporting setting
        var enableCrashReporting = CheckGroupPolicyCrashReporting();
        if (enableCrashReporting)
        {
            _crashReporter.SetupGlobalExceptionHandling();
        }

        AppDomain.CurrentDomain.UnhandledException += OnFatalException;

        try
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => _crashReporter?.LogCrash(e.Exception, "ThreadException");

            // Build service composition using shared composer
            try
            {
                _composition = LexonServiceComposer.BuildAsync().GetAwaiter().GetResult();
                var lexonService = _composition.Service;
            }
            catch (Exception ex)
            {
                _crashReporter.LogCrash(ex, "Failed during composition", isTerminating: true);
                ShowCrashMessageBox("Lexon failed to start during service composition.", ex);
                return;
            }

            // Check if this is first run (no profile saved or onboarding not completed)
            var isFirstRun = !(_composition.Storage.ExistsAsync(Profile.GetStorageKey(Profile.DefaultProfileId)).GetAwaiter().GetResult()) ||
                              !(_composition.Storage.ExistsAsync("onboarding_state").GetAwaiter().GetResult());

            if (isFirstRun)
            {
                // Show onboarding wizard
                try
                {
                    using var wizard = new OnboardingWizard(_composition.Storage, _composition.Profile, _composition.ThemeManager);
                    var result = wizard.ShowDialog();

                    // If user cancels onboarding, exit the application
                    if (result != DialogResult.OK)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _crashReporter.LogCrash(ex, "Failed during onboarding", isTerminating: true);
                    ShowCrashMessageBox("Lexon failed during the onboarding wizard.", ex);
                    return;
                }
            }

            _composition.Service.StartAsync().Wait();

            // Initialize system tray manager
            _trayManager = new SystemTrayManager();
            _trayManager.SetStatus(ServiceStatus.Active);
            _trayManager.SettingsRequested += OnSettingsRequested;
            _trayManager.ExitRequested += OnExitRequested;
            _trayManager.ToggleRequested += OnToggleRequested;
            _trayManager.KeyboardShortcutsRequested += OnKeyboardShortcutsRequested;
            _trayManager.UpdatesRequested += OnUpdatesRequested;
            _trayManager.AboutRequested += OnAboutRequested;
            _trayManager.UndoRequested += OnUndoRequested;
            _trayManager.PauseFifteenRequested += OnPauseFifteenRequested;
            _trayManager.PauseThisAppRequested += OnPauseThisAppRequested;
            _trayManager.ResumeRequested += OnResumeRequested;
            UpdateChecker.BusyChanged += busy => _trayManager?.SetUpdatesBusy(busy);
            _composition.KeyboardShortcutManager.ShortcutTriggered += OnShortcutTriggered;
            _composition.UndoManager.Changed += (_, _) =>
                _trayManager.SetUndoAvailability(_composition.UndoManager.CanUndo, _composition.UndoManager.LastUndoLabel);

            StartShowSettingsWaiter();
            StartUpdateChecker();

            // Build Settings in the background so the first tray click does not wait
            // on constructing the whole window. Idle fires once the tray is up.
            Application.Idle += WarmSettingsOnIdle;

            MaybeShowWelcomeBalloon();
            Application.Idle += ShowCoachOnIdle;

            // Wire quick toggle to update tray status
            _composition.QuickToggleManager.ToggleStateChanged += (sender, isEnabled) =>
            {
                if (isEnabled)
                {
                    CancelTimedPause();
                    UpdateTrayStatus(true);
                    _trayManager?.ShowStatusToast("Lexon enabled", true);
                    return;
                }

                UpdateTrayStatus(false);
                if (_suppressQuickPause)
                {
                    return;
                }

                var minutes = GetQuickPauseMinutes();
                StartTimedPause(minutes);
                _trayManager?.ShowStatusToast(
                    minutes <= 0
                        ? "Lexon paused until you turn it back on"
                        : $"Paused for {FormatPauseDuration(minutes)}",
                    false);
            };
            _composition.FocusTracker.ContextChanged += (_, _) =>
                UpdateTrayStatus(_composition.QuickToggleManager.IsEnabled);

            // Run application message loop (no main form, tray-resident)
            Application.Run();
        }
        catch (Exception ex)
        {
            _crashReporter.LogCrash(ex, "Failed during startup", isTerminating: true);
            ShowCrashMessageBox("Lexon failed to start unexpectedly.", ex);
        }
        finally
        {
            ShutdownSingleInstance();
        }
    }

    private static bool TryBecomeSingleInstance()
    {
        try
        {
            _instanceMutex = new Mutex(true, MutexName, out var createdNew);
            if (!createdNew)
            {
                _instanceMutex.Dispose();
                _instanceMutex = null;
                return false;
            }
        }
        catch (UnauthorizedAccessException)
        {
            _instanceMutex = new Mutex(true, MutexNameLocal, out var createdNew);
            if (!createdNew)
            {
                _instanceMutex.Dispose();
                _instanceMutex = null;
                return false;
            }
        }

        try
        {
            _showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
        }
        catch (UnauthorizedAccessException)
        {
            _showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventNameLocal);
        }

        return true;
    }

    private static void SignalExistingInstanceToShowSettings()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ShowSettingsEventName, out var globalEvent))
            {
                using (globalEvent)
                {
                    globalEvent.Set();
                }

                return;
            }
        }
        catch
        {
            // Fall through to the local event name.
        }

        try
        {
            if (EventWaitHandle.TryOpenExisting(ShowSettingsEventNameLocal, out var localEvent))
            {
                using (localEvent)
                {
                    localEvent.Set();
                }
            }
        }
        catch
        {
            // Nothing we can do — the first instance is not listening.
        }
    }

    private static void StartShowSettingsWaiter()
    {
        if (_showSettingsEvent == null)
        {
            return;
        }

        _showSettingsWaiter = new Thread(() =>
        {
            while (!_shuttingDown)
            {
                try
                {
                    if (_showSettingsEvent.WaitOne(250) && !_shuttingDown)
                    {
                        _trayManager?.InvokeOnUiThread(() => OnSettingsRequested(null, EventArgs.Empty));
                    }
                }
                catch
                {
                    break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "Lexon.ShowSettingsWaiter"
        };
        _showSettingsWaiter.Start();
    }

    private static void ShutdownSingleInstance()
    {
        _shuttingDown = true;
        try
        {
            _showSettingsEvent?.Set();
        }
        catch
        {
            // Ignore — we are shutting down.
        }

        if (_showSettingsWaiter != null && _showSettingsWaiter.IsAlive)
        {
            _showSettingsWaiter.Join(500);
        }

        _showSettingsEvent?.Dispose();
        _showSettingsEvent = null;

        try
        {
            _instanceMutex?.ReleaseMutex();
        }
        catch
        {
            // Mutex may already be released.
        }

        _instanceMutex?.Dispose();
        _instanceMutex = null;
    }

    private static void ShowCrashMessageBox(string message, Exception ex)
    {
        var fullMessage = $"{message}\n\nException: {ex.GetType().Name}\n{ex.Message}";
        MessageBox.Show(
            fullMessage,
            "Lexon Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static void UpdateTrayStatus(bool isEnabled)
    {
        if (_trayManager == null || _composition == null)
        {
            return;
        }

        if (!isEnabled)
        {
            _trayManager.SetStatus(ServiceStatus.Inactive);
            return;
        }

        var app = _composition.FocusTracker.GetCurrentContext().ApplicationName;
        var overrides = AppCategoryMapper.ParseOverrides(_composition.Profile.GetSetting<List<string>>("AppCategoryOverrides", []));
        var tone = AppCategoryMapper.Resolve(app, overrides);
        _trayManager.SetStatus(ServiceStatus.Active, $"Tone: {tone}");
    }

    private static void OnShortcutTriggered(object? sender, Lexon.Input.ShortcutTriggeredEventArgs e)
    {
        if (e.ShortcutName != "OpenSettings")
        {
            return;
        }

        // Swallow Ctrl+Shift+S so the foreground app does not also receive it.
        e.EventArgs.Handled = true;
        _trayManager?.InvokeOnUiThread(() => OnSettingsRequested(null, EventArgs.Empty));
    }

    private static void WarmSettingsOnIdle(object? sender, EventArgs e)
    {
        Application.Idle -= WarmSettingsOnIdle;
        if (_shuttingDown)
        {
            return;
        }

        EnsureSettingsForm();
    }

    private static void EnsureSettingsForm()
    {
        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            return;
        }

        _settingsForm = new SettingsForm(_composition!.Profile, _composition.PrivacyGuard, _composition.Storage, _composition.ThemeManager, _composition.SuggestionPipeline, _composition.SuggestionOverlay, _composition.PersonalizationManager, _composition.TextExpansionManager, _composition.EditConfirmation, provider => LexonServiceComposer.ApplyAiProvider(_composition, provider), _composition.CloudAiLog);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
    }

    private static void OnSettingsRequested(object? sender, EventArgs e)
    {
        try
        {
            EnsureSettingsForm();
            _settingsForm!.Present();
        }
        catch (Exception ex)
        {
            using var owner = new Form { TopMost = true, ShowInTaskbar = false };
            owner.Show();
            MessageBox.Show(
                owner,
                $"Could not open Settings.\n\n{ex.GetType().Name}: {ex.Message}",
                "Lexon",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void OnExitRequested(object? sender, EventArgs e)
    {
        ShutdownGracefully();
    }

    /// <summary>
    /// Stops the service and removes the tray icon before letting the message loop
    /// unwind. The Velopack update path exits through here too, so this must stay
    /// reachable without going through the tray menu.
    /// </summary>
    private static void ShutdownGracefully()
    {
        _shuttingDown = true;
        Application.Idle -= WarmSettingsOnIdle;
        Application.Idle -= ShowCoachOnIdle;
        CancelTimedPause();
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Close();
        }

        _composition?.Service.StopAsync().Wait();
        _trayManager?.Dispose();
        Application.Exit();
    }

    private static void OnToggleRequested(object? sender, EventArgs e)
    {
        CancelTimedPause();
        _composition?.QuickToggleManager.Toggle();
    }

    private static void OnAboutRequested(object? sender, EventArgs e)
    {
        InvokeOnUiThread(() =>
        {
            if (_aboutForm == null || _aboutForm.IsDisposed)
            {
                _aboutForm = new AboutForm();
                _aboutForm.FormClosed += (_, _) => _aboutForm = null;
            }

            _aboutForm.Show();
            _aboutForm.BringToFront();
        });
    }

    private static void OnUndoRequested(object? sender, EventArgs e)
    {
        if (_composition == null)
        {
            return;
        }

        if (!_composition.UndoManager.CanUndo)
        {
            _trayManager?.ShowStatusToast("Nothing to undo", false);
            return;
        }

        _composition.UndoManager.Undo();
        _trayManager?.ShowStatusToast("Undid last Lexon change", true);
    }

    private static void OnPauseFifteenRequested(object? sender, EventArgs e)
    {
        if (_composition == null)
        {
            return;
        }

        _suppressQuickPause = true;
        try
        {
            _composition.QuickToggleManager.SetEnabled(false);
        }
        finally
        {
            _suppressQuickPause = false;
        }

        StartTimedPause(15);
        _trayManager?.ShowStatusToast("Paused for 15 minutes", false);
    }

    private static int GetQuickPauseMinutes()
    {
        var minutes = _composition?.Profile.GetSetting("QuickPauseMinutes", 15) ?? 15;
        return minutes < 0 ? 15 : minutes;
    }

    private static void StartTimedPause(int minutes)
    {
        CancelTimedPause();
        if (minutes <= 0)
        {
            return;
        }

        var label = FormatPauseDuration(minutes);
        _trayManager?.SetPaused(true, $"Paused {label}");
        _pauseTimer = new System.Windows.Forms.Timer { Interval = minutes * 60 * 1000 };
        _pauseTimer.Tick += (_, _) => OnResumeRequested(null, EventArgs.Empty);
        _pauseTimer.Start();
    }

    private static string FormatPauseDuration(int minutes) => minutes switch
    {
        1 => "1 minute",
        60 => "1 hour",
        _ => $"{minutes} minutes"
    };

    private static void OnResumeRequested(object? sender, EventArgs e)
    {
        CancelTimedPause();
        _composition?.QuickToggleManager.SetEnabled(true);
        _trayManager?.SetPaused(false);
        _trayManager?.ShowStatusToast("Lexon resumed", true);
        UpdateTrayStatus(true);
    }

    private static void CancelTimedPause()
    {
        _pauseTimer?.Stop();
        _pauseTimer?.Dispose();
        _pauseTimer = null;
        _trayManager?.SetPaused(false);
    }

    private static void OnPauseThisAppRequested(object? sender, EventArgs e)
    {
        if (_composition == null)
        {
            return;
        }

        var app = _composition.FocusTracker.GetCurrentContext().ApplicationName;
        if (string.IsNullOrWhiteSpace(app))
        {
            _trayManager?.ShowStatusToast("No app in focus", false);
            return;
        }

        var blocked = _composition.Profile.GetSetting<List<string>>("BlockedApplications", []) ?? [];
        if (blocked.Any(name => name.Equals(app, StringComparison.OrdinalIgnoreCase)))
        {
            _trayManager?.ShowStatusToast($"{app} is already paused", false);
            return;
        }

        blocked.Add(app);
        _composition.Profile.SetSetting("BlockedApplications", blocked);
        _composition.PrivacyGuard.ReplaceBlockedApplications(blocked);
        _ = _composition.Profile.SaveAsync();
        _settingsForm?.ReloadBlockedApplications();
        _trayManager?.ShowStatusToast($"Paused in {app}", false);
    }

    private static void MaybeShowWelcomeBalloon()
    {
        if (_trayManager == null || _composition == null)
        {
            return;
        }

        var last = _composition.Profile.GetSetting("LastWelcomeVersion", string.Empty);
        if (string.Equals(last, AppVersion.Current, StringComparison.Ordinal))
        {
            return;
        }

        var text = string.IsNullOrEmpty(last)
            ? "Lexon is in the tray. Right-click the icon for pause, undo, and settings."
            : $"Lexon {AppVersion.Current} is ready. Right-click the tray icon for options.";
        _trayManager.ShowBalloonTip($"Lexon {AppVersion.Current}", text, ToolTipIcon.Info);
        _composition.Profile.SetSetting("LastWelcomeVersion", AppVersion.Current);
        _ = _composition.Profile.SaveAsync();
    }

    private static void ShowCoachOnIdle(object? sender, EventArgs e)
    {
        Application.Idle -= ShowCoachOnIdle;
        if (_shuttingDown || _composition == null
            || !_composition.Profile.GetSetting("ShowCoach", true)
            || _composition.Profile.GetSetting("CoachCompleted", false))
        {
            return;
        }

        using var coach = new CoachForm();
        coach.ShowDialog();
        _composition.Profile.SetSetting("CoachCompleted", true);
        _ = _composition.Profile.SaveAsync();
    }

    private static void OnFatalException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception)
        {
            return;
        }

        _crashReporter?.LogCrash(exception, "UnhandledException", e.IsTerminating);
        if (!e.IsTerminating || _shuttingDown)
        {
            return;
        }

        OfferRestartAfterCrash();
    }

    private static void OfferRestartAfterCrash()
    {
        var flag = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lexon",
            "crash-restart.flag");

        try
        {
            if (_restartedAfterCrash && File.Exists(flag)
                && DateTime.UtcNow - File.GetLastWriteTimeUtc(flag) < TimeSpan.FromMinutes(2))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(flag)!);
            File.WriteAllText(flag, DateTime.UtcNow.ToString("o"));
        }
        catch
        {
            // Restart is best-effort.
        }

        var answer = MessageBox.Show(
            "Lexon stopped unexpectedly. Restart now?",
            "Lexon",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Error);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--after-crash",
                UseShellExecute = true
            });
        }
        catch
        {
            // The crash path must not throw.
        }
    }

    private static void OnKeyboardShortcutsRequested(object? sender, EventArgs e)
    {
        if (_shortcutsForm == null || _shortcutsForm.IsDisposed)
        {
            _shortcutsForm = new KeyboardShortcutsForm(_composition.KeyboardShortcutManager);
            _shortcutsForm.FormClosed += (s, args) => _shortcutsForm = null;
        }
        _shortcutsForm.Show();
        _shortcutsForm.BringToFront();
    }

    private static void StartUpdateChecker()
    {
        if (!_composition!.Profile.GetSetting("EnableAutoUpdates", true))
        {
            return;
        }

        _ = Task.Run(() => UpdateChecker.CheckOnStartupAsync(InvokeOnUiThread, ShutdownGracefully));
    }

    private static void OnUpdatesRequested(object? sender, EventArgs e)
    {
        _ = Task.Run(() => UpdateChecker.CheckInteractiveAsync(InvokeOnUiThread, ShutdownGracefully));
    }

    private static void InvokeOnUiThread(Action action)
    {
        if (_shuttingDown)
        {
            return;
        }

        _trayManager?.InvokeOnUiThread(action);
    }

    private static bool CheckGroupPolicyCrashReporting()
    {
        try
        {
            var gpoManager = new GroupPolicyManager();
            gpoManager.Initialize();
            return gpoManager.PrivacySettings.EnableCrashReporting;
        }
        catch
        {
            // Default to enabled if Group Policy check fails
            return true;
        }
    }
}
