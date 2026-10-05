using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lexon.SettingsModel;

public sealed class PrivacySettingsViewModel : INotifyPropertyChanged, IOwnedSettingsPage
{
    public static readonly string[] OwnedKeyList =
    [
        AppSettings.LocalModeKey,
        AppSettings.BlockedApplicationsKey
    ];

    private readonly AppSettings _settings;
    private readonly PersistScheduler _persist;
    private readonly IAiPolicyPublisher _policy;
    private readonly IProcessPicker _picker;
    private readonly ICloudAiActivityViewer _activity;
    private readonly Action? _refresh;
    private bool _localOnly;
    private string _blockedAppsText = string.Empty;
    private bool _isDirty;

    public PrivacySettingsViewModel(
        AppSettings settings,
        PersistScheduler persist,
        IAiPolicyPublisher policy,
        IProcessPicker picker,
        ICloudAiActivityViewer activity,
        Action? refresh = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _refresh = refresh;
    }

    public IReadOnlyList<string> OwnedKeys => OwnedKeyList;

    public bool IsDirty => _isDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool LocalOnly
    {
        get => _localOnly;
        set
        {
            if (_localOnly == value)
            {
                return;
            }

            _localOnly = value;
            OnPropertyChanged();
            if (_persist.IsLoading)
            {
                return;
            }

            _settings.LocalMode = value;
            PublishPolicy();
            MarkDirtyAndSchedule();
        }
    }

    public string BlockedAppsText
    {
        get => _blockedAppsText;
        set
        {
            if (_blockedAppsText == value)
            {
                return;
            }

            _blockedAppsText = value ?? string.Empty;
            OnPropertyChanged();
            if (_persist.IsLoading)
            {
                return;
            }

            _settings.BlockedApplications = BlockedAppList.Parse(_blockedAppsText);
            MarkDirtyAndSchedule();
        }
    }

    public void Load()
    {
        _persist.IsLoading = true;
        try
        {
            _refresh?.Invoke();
            _localOnly = _settings.LocalMode;
            // Keep the typed text when its parsed list matches the profile so
            // an activate-reload does not reformat trailing commas or casing.
            var fromProfile = _settings.BlockedApplications ?? [];
            var currentParsed = BlockedAppList.Parse(_blockedAppsText);
            if (!ListEquals(currentParsed, fromProfile))
            {
                _blockedAppsText = BlockedAppList.FormatCsv(fromProfile);
            }

            _isDirty = false;
            OnPropertyChanged(nameof(LocalOnly));
            OnPropertyChanged(nameof(BlockedAppsText));
        }
        finally
        {
            _persist.IsLoading = false;
        }
    }

    public void CopyOwnedTo(AppSettings target)
    {
        target.LocalMode = _localOnly;
        target.BlockedApplications = BlockedAppList.Parse(_blockedAppsText);
    }

    public void MarkClean() => _isDirty = false;

    public void AddRunningApp()
    {
        var picked = _picker.Pick("Select a running application to block:");
        if (string.IsNullOrEmpty(picked))
        {
            return;
        }

        var current = BlockedAppList.Parse(_blockedAppsText);
        if (!BlockedAppList.TryAdd(current, picked, out _))
        {
            return;
        }

        _blockedAppsText = BlockedAppList.FormatCsv(current);
        _settings.BlockedApplications = current;
        OnPropertyChanged(nameof(BlockedAppsText));
        if (!_persist.IsLoading)
        {
            MarkDirtyAndSchedule();
        }
    }

    public void ShowActivityLog()
    {
        _activity.Show();
    }

    private void MarkDirtyAndSchedule()
    {
        _isDirty = true;
        _persist.Schedule(DateTime.UtcNow);
    }

    private void PublishPolicy()
    {
        _refresh?.Invoke();
        _settings.LocalMode = _localOnly;
        // AiProbeSession lives on the classic form. That form already syncs
        // LocalMode on load (SyncAiConnectionFromLoad / EnterLocalOnly), so the
        // gallery only publishes the shared access policy and saves LocalMode.
        _policy.Publish(
            _localOnly,
            _settings.AiSuggestionsWhileTyping,
            _settings.AiRewriteOnRequest,
            _settings.AiPrefetchOnSelection);
    }

    private static bool ListEquals(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
