using System.ComponentModel;
using System.Runtime.CompilerServices;
using Lexon.Core;

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
    private readonly IMessageService? _messages;
    private readonly Action? _refresh;
    private readonly Func<AppSettings>? _liveSnapshot;
    private bool _localOnly;
    private string _blockedAppsText = string.Empty;
    private bool _isDirty;

    public PrivacySettingsViewModel(
        AppSettings settings,
        PersistScheduler persist,
        IAiPolicyPublisher policy,
        IProcessPicker picker,
        ICloudAiActivityViewer activity,
        Action? refresh = null,
        Func<AppSettings>? liveSnapshot = null,
        IMessageService? messages = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _refresh = refresh;
        _liveSnapshot = liveSnapshot;
        _messages = messages;
    }

    public IReadOnlyList<string> OwnedKeys => OwnedKeyList;

    public bool IsDirty => _isDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? UserEdited;

    public event Action<bool>? LocalOnlyChanged;

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
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.LocalMode = value;
            // Dirty before PropertyChanged so live snapshots see the new LocalMode.
            MarkDirtyAndSchedule();
            PublishPolicy();
            LocalOnlyChanged?.Invoke(_localOnly);
            OnPropertyChanged();
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
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.BlockedApplications = BlockedAppList.Parse(_blockedAppsText);
            MarkDirtyAndSchedule();
            OnPropertyChanged();
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
        if (!_persist.IsLoading)
        {
            MarkDirtyAndSchedule();
        }

        OnPropertyChanged(nameof(BlockedAppsText));
    }

    public void ShowActivityLog()
    {
        _activity.Show();
    }

    public void ShowPrivacyPreview()
    {
        _messages?.Info(PrivacyDisclosure.Body, PrivacyDisclosure.Title);
    }

    private void MarkDirtyAndSchedule()
    {
        _isDirty = true;
        _persist.Schedule(DateTime.UtcNow);
        UserEdited?.Invoke();
    }

    private void PublishPolicy()
    {
        _refresh?.Invoke();
        _settings.LocalMode = _localOnly;
        var snap = _liveSnapshot?.Invoke() ?? _settings;
        _policy.Publish(
            snap.LocalMode,
            snap.AiSuggestionsWhileTyping,
            snap.AiRewriteOnRequest,
            snap.AiPrefetchOnSelection);
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
