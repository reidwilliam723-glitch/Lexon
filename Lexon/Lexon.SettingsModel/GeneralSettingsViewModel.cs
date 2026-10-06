using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lexon.SettingsModel;

public sealed class GeneralSettingsViewModel : INotifyPropertyChanged, IOwnedSettingsPage
{
    public const string StartupFailedMessage = "Couldn't change the Windows startup setting.";
    public const string SettingsFilter = "Lexon settings (*.json)|*.json";
    public const string DefaultSettingsFileName = "lexon-settings.json";
    public const string ExportSuccessMessage = "Settings exported.";
    public const string ImportSuccessMessage = "Settings imported.";
    public const string ImportInvalidMessage = "That file is not a valid Lexon settings export.";
    public const string SaveFailedMessage = "Couldn't save that file.";
    public const string ReadFailedMessage = "Couldn't read that file.";
    public const string BackupUnavailableMessage = "Settings backup is available while Lexon is running.";

    public static readonly string[] OwnedKeyList =
    [
        AppSettings.MinimizeToTrayKey,
        AppSettings.EnableAutoUpdatesKey,
        AppSettings.OpenSettingsFullScreenKey,
        AppSettings.QuickPauseMinutesKey
    ];

    public static IReadOnlyList<string> QuickPauseLabels { get; } =
    [
        "Until I turn it back on",
        "1 minute",
        "5 minutes",
        "15 minutes",
        "30 minutes",
        "1 hour"
    ];

    private readonly AppSettings _settings;
    private readonly IStartupRegistration _startup;
    private readonly PersistScheduler _persist;
    private readonly ISettingsBackupService? _backup;
    private readonly IFileDialogService? _files;
    private readonly IMessageService? _messages;
    private readonly Action? _afterImport;

    private bool _startWithWindows;
    private bool _minimizeToTray;
    private bool _checkForUpdates;
    private bool _openSettingsFullScreen;
    private int _quickPauseIndex = 3;
    private string _statusMessage = string.Empty;
    private bool _isDirty;

    public GeneralSettingsViewModel(
        AppSettings settings,
        IStartupRegistration startup,
        PersistScheduler persist,
        ISettingsBackupService? backup = null,
        IFileDialogService? files = null,
        IMessageService? messages = null,
        Action? afterImport = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _startup = startup ?? throw new ArgumentNullException(nameof(startup));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _backup = backup;
        _files = files;
        _messages = messages;
        _afterImport = afterImport;
    }

    public IReadOnlyList<string> OwnedKeys => OwnedKeyList;

    public bool IsDirty => _isDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? UserEdited;

    public event Action<bool>? OpenFullScreenChanged;

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows == value)
            {
                return;
            }

            if (_persist.IsLoading)
            {
                _startWithWindows = value;
                OnPropertyChanged();
                return;
            }

            if (!_startup.TrySetEnabled(value))
            {
                OnPropertyChanged();
                StatusMessage = StartupFailedMessage;
                return;
            }

            _startWithWindows = value;
            StatusMessage = string.Empty;
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (_minimizeToTray == value)
            {
                return;
            }

            _minimizeToTray = value;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            // Gallery already hides instead of closing. Real close-to-tray
            // is wired when the standalone settings window exists.
            _settings.MinimizeToTray = value;
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public bool CheckForUpdates
    {
        get => _checkForUpdates;
        set
        {
            if (_checkForUpdates == value)
            {
                return;
            }

            _checkForUpdates = value;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.EnableAutoUpdates = value;
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public bool OpenSettingsFullScreen
    {
        get => _openSettingsFullScreen;
        set
        {
            if (_openSettingsFullScreen == value)
            {
                return;
            }

            _openSettingsFullScreen = value;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.OpenSettingsFullScreen = value;
            MarkDirtyAndSchedule();
            OnPropertyChanged();
            OpenFullScreenChanged?.Invoke(value);
        }
    }

    public int QuickPauseIndex
    {
        get => _quickPauseIndex;
        set
        {
            var index = QuickPauseOptions.IndexFromMinutes(QuickPauseOptions.MinutesFromIndex(value));
            if (_quickPauseIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _quickPauseIndex = index;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.QuickPauseMinutes = QuickPauseOptions.MinutesFromIndex(index);
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public void Load()
    {
        _persist.IsLoading = true;
        try
        {
            _startWithWindows = _startup.IsEnabled();
            _minimizeToTray = _settings.MinimizeToTray;
            _checkForUpdates = _settings.EnableAutoUpdates;
            _openSettingsFullScreen = _settings.OpenSettingsFullScreen;
            _quickPauseIndex = QuickPauseOptions.IndexFromMinutes(_settings.QuickPauseMinutes);
            _statusMessage = string.Empty;
            _isDirty = false;
            OnPropertyChanged(nameof(StartWithWindows));
            OnPropertyChanged(nameof(MinimizeToTray));
            OnPropertyChanged(nameof(CheckForUpdates));
            OnPropertyChanged(nameof(OpenSettingsFullScreen));
            OnPropertyChanged(nameof(QuickPauseIndex));
            OnPropertyChanged(nameof(StatusMessage));
        }
        finally
        {
            _persist.IsLoading = false;
        }
    }

    public void CopyOwnedTo(AppSettings target)
    {
        target.MinimizeToTray = _minimizeToTray;
        target.EnableAutoUpdates = _checkForUpdates;
        target.OpenSettingsFullScreen = _openSettingsFullScreen;
        target.QuickPauseMinutes = QuickPauseOptions.MinutesFromIndex(_quickPauseIndex);
    }

    public void MarkClean() => _isDirty = false;

    public void ExportSettings()
    {
        if (_backup == null)
        {
            _messages?.Info(BackupUnavailableMessage, "Export");
            return;
        }

        if (_files == null)
        {
            return;
        }

        var path = _files.PickSavePath(SettingsFilter, DefaultSettingsFileName);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.WriteAllText(path, _backup.ExportJson(includePersonalData: false));
            _messages?.Info(ExportSuccessMessage, "Export");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _messages?.Info(SaveFailedMessage, "Export");
        }
    }

    public void ImportSettings()
    {
        if (_backup == null)
        {
            _messages?.Info(BackupUnavailableMessage, "Import");
            return;
        }

        if (_files == null)
        {
            return;
        }

        var path = _files.PickOpenPath(SettingsFilter);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _messages?.Info(ReadFailedMessage, "Import");
            return;
        }

        if (_backup.ImportJson(json, overwrite: true))
        {
            _afterImport?.Invoke();
            Load();
            _messages?.Info(ImportSuccessMessage, "Import");
        }
        else
        {
            _messages?.Info(ImportInvalidMessage, "Import");
        }
    }

    private void MarkDirtyAndSchedule()
    {
        _isDirty = true;
        _persist.Schedule(DateTime.UtcNow);
        UserEdited?.Invoke();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
