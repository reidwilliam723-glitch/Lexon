using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lexon.SettingsModel;

public sealed class GeneralSettingsViewModel : INotifyPropertyChanged, IOwnedSettingsPage
{
    public const string StartupFailedMessage = "Couldn't change the Windows startup setting.";

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

    private bool _startWithWindows;
    private bool _minimizeToTray;
    private bool _checkForUpdates;
    private bool _openSettingsFullScreen;
    private int _quickPauseIndex = 3;
    private string _statusMessage = string.Empty;
    private bool _isDirty;

    public GeneralSettingsViewModel(AppSettings settings, IStartupRegistration startup, PersistScheduler persist)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _startup = startup ?? throw new ArgumentNullException(nameof(startup));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
    }

    public IReadOnlyList<string> OwnedKeys => OwnedKeyList;

    public bool IsDirty => _isDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

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
            OnPropertyChanged();
            StatusMessage = string.Empty;
            MarkDirtyAndSchedule();
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (!SetField(ref _minimizeToTray, value))
            {
                return;
            }

            if (_persist.IsLoading)
            {
                return;
            }

            // Gallery already hides instead of closing. Real close-to-tray
            // is wired when the standalone settings window exists.
            _settings.MinimizeToTray = value;
            MarkDirtyAndSchedule();
        }
    }

    public bool CheckForUpdates
    {
        get => _checkForUpdates;
        set
        {
            if (!SetField(ref _checkForUpdates, value))
            {
                return;
            }

            if (_persist.IsLoading)
            {
                return;
            }

            _settings.EnableAutoUpdates = value;
            MarkDirtyAndSchedule();
        }
    }

    public bool OpenSettingsFullScreen
    {
        get => _openSettingsFullScreen;
        set
        {
            if (!SetField(ref _openSettingsFullScreen, value))
            {
                return;
            }

            if (_persist.IsLoading)
            {
                return;
            }

            _settings.OpenSettingsFullScreen = value;
            MarkDirtyAndSchedule();
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
            OnPropertyChanged();
            if (_persist.IsLoading)
            {
                return;
            }

            _settings.QuickPauseMinutes = QuickPauseOptions.MinutesFromIndex(index);
            MarkDirtyAndSchedule();
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

    private void MarkDirtyAndSchedule()
    {
        _isDirty = true;
        _persist.Schedule(DateTime.UtcNow);
    }

    private bool SetField(ref bool field, bool value, [CallerMemberName] string? name = null)
    {
        if (field == value)
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
