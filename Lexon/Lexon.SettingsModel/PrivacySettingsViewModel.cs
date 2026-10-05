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
            _persist.Schedule(DateTime.UtcNow);
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
            _persist.Schedule(DateTime.UtcNow);
        }
    }

    public void Load()
    {
        _persist.IsLoading = true;
        try
        {
            _refresh?.Invoke();
            _localOnly = _settings.LocalMode;
            _blockedAppsText = BlockedAppList.FormatCsv(_settings.BlockedApplications);
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
            _persist.Schedule(DateTime.UtcNow);
        }
    }

    public void ShowActivityLog()
    {
        _activity.Show();
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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
