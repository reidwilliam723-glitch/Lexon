using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Lexon.AI;
using Lexon.Core;

namespace Lexon.SettingsModel;

public sealed class AiSettingsViewModel : INotifyPropertyChanged, IOwnedSettingsPage, IDisposable
{
    public static readonly string[] OwnedKeyList =
    [
        AppSettings.AIProviderKey,
        AppSettings.APIKeyKey,
        AppSettings.AIKeyValidatedKey,
        AppSettings.AIModelKey,
        AppSettings.AiSuggestionsWhileTypingKey,
        AppSettings.AiRewriteOnRequestKey,
        AppSettings.AiPrefetchOnSelectionKey
    ];

    public static IReadOnlyList<string> ProviderLabels { get; } = AiProviderCatalog.AllProviders;

    public static readonly TimeSpan KeyProbeDelay = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan ClipboardWatchTimeout = TimeSpan.FromMinutes(3);

    public const string CloudExplainText =
        "When cloud AI suggestions are on, the words around your cursor are sent to your chosen provider as you type. Rewrites send only the text you select. Local Ollama on this PC is not gated by the typing toggle. Nothing is sent in Local-only mode or in blocked apps and password fields.";

    public const string WatchingClipboardMessage =
        "Lexon is watching your clipboard for the next few minutes to catch a key that matches this provider. It only looks for a matching key and does not store or send anything else. Cancel anytime.";

    public const string OpenKeyPageFailedMessage = "Could not open the key page. Paste a key here instead.";
    public const string ClipboardWatchFailedMessage = "Could not watch the clipboard. Paste your key here after you copy it.";
    public const string ClipboardCancelledMessage = "Cancelled. You can still paste a key.";
    public const string ClipboardTimedOutMessage = "Timed out waiting for a key. Paste it here if you already copied it.";
    public const string ProviderHint = "Paste an API key to connect. OpenAI is recommended.";
    public const string GetKeyHint =
        "Opens the provider’s key page, then watches your clipboard for a matching key only. Lexon does not store or send anything else from the clipboard. Cancel anytime.";

    private readonly AppSettings _settings;
    private readonly PersistScheduler _persist;
    private readonly AiProbeSession _session;
    private readonly IAiPolicyPublisher _policy;
    private readonly IClipboardWatch _clipboard;
    private readonly IUrlLauncher _urls;
    private readonly IDelayScheduler _delays;
    private readonly Func<bool> _readLocalOnly;
    private readonly Func<AppSettings> _liveSnapshot;
    private readonly Action<Action>? _invokeOnUi;

    private readonly ObservableCollection<string> _models = [];
    private bool _advancedVisible;
    private int _providerIndex;
    private string _apiKeyText = string.Empty;
    private string? _selectedModel;
    private bool _aiTyping;
    private bool _aiRewrite = true;
    private bool _aiPrefetch;
    private bool _localOnly;
    private string _statusActiveLine = "AI is off";
    private string _statusDetail = "Paste a key to connect. Lexon will check it automatically.";
    private AiStatusKind _statusKind = AiStatusKind.Secondary;
    private bool _isWaitingForClipboard;
    private bool _showKeyBox;
    private bool _showGetKey;
    private bool _showModel;
    private bool _aiFlagsEnabled = true;
    private string _heading = "OpenAI (recommended)";
    private bool _isDirty;
    private bool _firstTabProbeDone;
    private bool _probeAfterSeed;
    private string _lastSeededModel = string.Empty;
    private string? _waitingProvider;
    private string? _lastProbeProvider;
    private string? _lastProbeKey;
    private string? _lastProbeModel;
    private bool _lastProbeLocalOnly;
    private IDisposable? _probeDelay;
    private IDisposable? _clipboardTimeout;
    private bool _disposed;
    private EventHandler? _clipboardHandler;

    public AiSettingsViewModel(
        AppSettings settings,
        PersistScheduler persist,
        AiProbeSession session,
        IAiPolicyPublisher policy,
        IClipboardWatch clipboard,
        IUrlLauncher urls,
        IDelayScheduler delays,
        Func<bool> readLocalOnly,
        Func<AppSettings> liveSnapshot,
        Action<Action>? invokeOnUi = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _delays = delays ?? throw new ArgumentNullException(nameof(delays));
        _readLocalOnly = readLocalOnly ?? throw new ArgumentNullException(nameof(readLocalOnly));
        _liveSnapshot = liveSnapshot ?? throw new ArgumentNullException(nameof(liveSnapshot));
        _invokeOnUi = invokeOnUi;

        _session.Persist = OnSessionPersist;
        _session.ResolveModelAfterSuccess = OnResolveModelAfterSuccess;
        _session.OnStatusChanged = OnSessionStatusChanged;
        _clipboardHandler = (_, _) => OnClipboardUpdated();
        _clipboard.Updated += _clipboardHandler;
    }

    public IReadOnlyList<string> OwnedKeys => OwnedKeyList;

    public bool IsDirty => _isDirty;

    public AiProbeSession Session => _session;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Models => _models;

    public bool AdvancedVisible
    {
        get => _advancedVisible;
        private set => SetField(ref _advancedVisible, value);
    }

    public int ProviderIndex
    {
        get => _providerIndex;
        set
        {
            var index = value >= 0 && value < ProviderLabels.Count ? value : 0;
            if (_providerIndex == index)
            {
                return;
            }

            _providerIndex = index;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            StopClipboardWatch();
            UpdateEntryMode();
            OnPropertyChanged();
            ScheduleProbe();
        }
    }

    public string ApiKeyText
    {
        get => _apiKeyText;
        set
        {
            var next = value ?? string.Empty;
            if (_apiKeyText == next)
            {
                return;
            }

            _apiKeyText = next;
            OnPropertyChanged();
            if (_persist.IsLoading)
            {
                return;
            }

            // Key text is not saved until a probe succeeds; only schedule a probe.
            ScheduleProbe();
        }
    }

    public string? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (_selectedModel == value)
            {
                return;
            }

            _selectedModel = value;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            MarkDirtyAndSchedule();
            OnPropertyChanged();
            ScheduleProbe();
        }
    }

    public bool AiSuggestionsWhileTyping
    {
        get => _aiTyping;
        set => SetAiFlag(ref _aiTyping, value, v => _settings.AiSuggestionsWhileTyping = v);
    }

    public bool AiRewriteOnRequest
    {
        get => _aiRewrite;
        set => SetAiFlag(ref _aiRewrite, value, v => _settings.AiRewriteOnRequest = v);
    }

    public bool AiPrefetchOnSelection
    {
        get => _aiPrefetch;
        set => SetAiFlag(ref _aiPrefetch, value, v => _settings.AiPrefetchOnSelection = v);
    }

    public string StatusActiveLine
    {
        get => _statusActiveLine;
        private set => SetField(ref _statusActiveLine, value);
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => SetField(ref _statusDetail, value);
    }

    public AiStatusKind StatusKind
    {
        get => _statusKind;
        private set => SetField(ref _statusKind, value);
    }

    public bool IsWaitingForClipboard
    {
        get => _isWaitingForClipboard;
        private set => SetField(ref _isWaitingForClipboard, value);
    }

    public bool ShowKeyBox
    {
        get => _showKeyBox;
        private set => SetField(ref _showKeyBox, value);
    }

    public bool ShowGetKeyButton
    {
        get => _showGetKey;
        private set => SetField(ref _showGetKey, value);
    }

    public bool ShowModel
    {
        get => _showModel;
        private set => SetField(ref _showModel, value);
    }

    public bool AiFlagsEnabled
    {
        get => _aiFlagsEnabled;
        private set => SetField(ref _aiFlagsEnabled, value);
    }

    public string Heading
    {
        get => _heading;
        private set => SetField(ref _heading, value);
    }

    public bool ShowProviderCombo => _advancedVisible;

    public bool ShowMoreProvidersLink => !_advancedVisible;

    /// <summary>
    /// True when the combo or key box differs from the last validated session
    /// values. Not the same as <see cref="IsDirty"/> (flags/model only).
    /// </summary>
    public bool HasUnsavedUiState
        => !string.Equals(SelectedUiProvider(), _session.ActiveProvider, StringComparison.OrdinalIgnoreCase)
            || _apiKeyText != _session.ActiveApiKey;

    public void Load()
    {
        _persist.IsLoading = true;
        try
        {
            _localOnly = _readLocalOnly();
            _settings.LocalMode = _localOnly;

            var storedProvider = _settings.AIProvider;
            var storedKey = _settings.APIKey;
            var storedModel = _settings.AIModel;
            var storedValidated = _settings.AIKeyValidated;

            var needsReseed =
                !string.Equals(_session.ActiveProvider, storedProvider, StringComparison.OrdinalIgnoreCase)
                || _session.ActiveApiKey != storedKey
                || _session.AiValidated != storedValidated
                || !string.Equals(_lastSeededModel, storedModel, StringComparison.Ordinal);

            if (needsReseed)
            {
                StopClipboardWatch();
                _session.ActiveProvider = storedProvider;
                _session.ActiveApiKey = storedKey;
                _session.AiValidated = storedValidated;
                _lastSeededModel = storedModel ?? string.Empty;
                if (!_localOnly)
                {
                    var keyPresent = storedValidated
                        && !storedProvider.Equals("None", StringComparison.OrdinalIgnoreCase);
                    var installed = _session.InstalledProviderName();
                    _session.Connection.SeedFromInstalled(
                        installed,
                        string.IsNullOrWhiteSpace(storedModel) ? null : storedModel,
                        keyPresent);
                }

                _probeAfterSeed = !_localOnly
                    && !storedProvider.Equals("None", StringComparison.OrdinalIgnoreCase);

                if (AiProviderCatalog.ShowAdvancedByDefault(storedProvider))
                {
                    _advancedVisible = true;
                }

                _providerIndex = ResolveStoredProviderIndex(storedProvider);
                _apiKeyText = _session.ActiveApiKey;
                _aiTyping = _settings.AiSuggestionsWhileTyping;
                _aiRewrite = _settings.AiRewriteOnRequest;
                _aiPrefetch = _settings.AiPrefetchOnSelection;
                _isDirty = false;

                UpdateEntryMode(fillModels: true, preferredModel: storedModel);
                PaintFromSession();

                OnPropertyChanged(nameof(AdvancedVisible));
                OnPropertyChanged(nameof(ShowProviderCombo));
                OnPropertyChanged(nameof(ShowMoreProvidersLink));
                OnPropertyChanged(nameof(ProviderIndex));
                OnPropertyChanged(nameof(ApiKeyText));
                OnPropertyChanged(nameof(AiSuggestionsWhileTyping));
                OnPropertyChanged(nameof(AiRewriteOnRequest));
                OnPropertyChanged(nameof(AiPrefetchOnSelection));
                OnPropertyChanged(nameof(SelectedModel));
            }
            else
            {
                // Keep in-progress provider/key/advanced/clipboard; refresh live pieces.
                if (!_isDirty)
                {
                    _aiTyping = _settings.AiSuggestionsWhileTyping;
                    _aiRewrite = _settings.AiRewriteOnRequest;
                    _aiPrefetch = _settings.AiPrefetchOnSelection;
                    OnPropertyChanged(nameof(AiSuggestionsWhileTyping));
                    OnPropertyChanged(nameof(AiRewriteOnRequest));
                    OnPropertyChanged(nameof(AiPrefetchOnSelection));
                }

                UpdateEntryMode(fillModels: true, preferredModel: _selectedModel);
                PaintFromSession();
                OnPropertyChanged(nameof(SelectedModel));
            }
        }
        finally
        {
            _persist.IsLoading = false;
        }

        if (_firstTabProbeDone && _probeAfterSeed && !_localOnly)
        {
            _probeAfterSeed = false;
            _ = ProbeNowAsync();
        }
    }

    /// <summary>
    /// Called when the AI tab is shown. Performs Load and at most one probe
    /// (first show, after a seed from changed stored values, or when unsaved
    /// inputs still need a check that was lost while the window was hidden).
    /// </summary>
    public void OnTabSelected()
    {
        Load();
        if (_localOnly)
        {
            _session.Connection.EnterLocalOnly();
            PaintFromSession();
            UpdateEntryMode(fillModels: false);
            return;
        }

        if (!_firstTabProbeDone)
        {
            _firstTabProbeDone = true;
            _probeAfterSeed = false;
            _ = ProbeNowAsync();
            return;
        }

        // Load() already ran a changed-values probe when needed.
        if (_probeAfterSeed)
        {
            return;
        }

        if (HasUnsavedUiState && !IsProbePendingOrRunning())
        {
            _ = ProbeNowAsync();
        }
    }

    public void CopyOwnedTo(AppSettings target)
    {
        // Validated session state only — never the key text box or combo alone.
        target.AIProvider = _session.ActiveProvider;
        target.APIKey = _session.ActiveApiKey;
        target.AIKeyValidated = _session.AiValidated;
        target.AIModel = ResolveSelectedModel();
        target.AiSuggestionsWhileTyping = _aiTyping;
        target.AiRewriteOnRequest = _aiRewrite;
        target.AiPrefetchOnSelection = _aiPrefetch;
    }

    public void MarkClean()
    {
        _isDirty = false;
        _lastSeededModel = ResolveSelectedModel();
    }

    public void ShowMoreProviders()
    {
        if (_advancedVisible)
        {
            return;
        }

        _advancedVisible = true;
        OnPropertyChanged(nameof(AdvancedVisible));
        OnPropertyChanged(nameof(ShowProviderCombo));
        OnPropertyChanged(nameof(ShowMoreProvidersLink));
        UpdateEntryMode();
    }

    public void GetApiKey()
    {
        var provider = SelectedUiProvider();
        var url = AiProviderCatalog.KeyCreationUrl(provider);
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        if (!_urls.TryOpen(url))
        {
            SetOverrideStatus(OpenKeyPageFailedMessage, AiStatusKind.Error);
            return;
        }

        StartClipboardWatch(provider);
    }

    public void CancelWait()
        => StopClipboardWatch(ClipboardCancelledMessage);

    public void OnKeyLostFocus()
    {
        if (_persist.IsLoading)
        {
            return;
        }

        CancelProbeDelay();
        _ = ProbeNowAsync();
    }

    public void OnLocalOnlyChanged(bool localOnly)
    {
        _localOnly = localOnly;
        _settings.LocalMode = localOnly;
        if (localOnly)
        {
            CancelProbeDelay();
            StopClipboardWatch();
            _session.EnterLocalOnly();
            UpdateEntryMode();
            PaintFromSession();
            return;
        }

        // Leaving Local-only: save (session Persist already ran from EnterLocalOnly
        // path when entering; on leave classic calls ApplyNow then schedules probe).
        if (!_persist.IsLoading)
        {
            MarkDirtyAndSchedule();
        }

        UpdateEntryMode();
        ScheduleProbe();
    }

    public void OnTabLeft()
    {
        // Leaving the tab must not cancel a pending key probe — only the watch.
        StopClipboardWatch();
    }

    /// <summary>
    /// Gallery window hidden or closed: cancel pending probes and the watch.
    /// </summary>
    public void OnWindowHidden()
    {
        StopClipboardWatch();
        CancelProbeDelay();
        _session.Gate.Invalidate();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        OnWindowHidden();
        _clipboardTimeout?.Dispose();
        _clipboardTimeout = null;
        if (_clipboardHandler != null)
        {
            _clipboard.Updated -= _clipboardHandler;
            _clipboardHandler = null;
        }

        _session.Persist = static () => { };
        _session.OnStatusChanged = null;
    }

    private void SetAiFlag(ref bool field, bool value, Action<bool> apply, [CallerMemberName] string? name = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        if (_persist.IsLoading)
        {
            OnPropertyChanged(name);
            return;
        }

        apply(value);
        MarkDirtyAndSchedule();
        PublishPolicyNow();
        OnPropertyChanged(name);
    }

    private void PublishPolicyNow()
    {
        var snap = _liveSnapshot();
        _policy.Publish(
            snap.LocalMode,
            snap.AiSuggestionsWhileTyping,
            snap.AiRewriteOnRequest,
            snap.AiPrefetchOnSelection);
    }

    private void MarkDirtyAndSchedule()
    {
        _isDirty = true;
        _persist.Schedule(DateTime.UtcNow);
    }

    private void OnSessionPersist()
    {
        if (_persist.IsLoading)
        {
            return;
        }

        _settings.AIProvider = _session.ActiveProvider;
        _settings.APIKey = _session.ActiveApiKey;
        _settings.AIKeyValidated = _session.AiValidated;
        _settings.AIModel = ResolveSelectedModel();
        _lastSeededModel = _settings.AIModel;
        MarkDirtyAndSchedule();
    }

    private string OnResolveModelAfterSuccess(string model)
    {
        EnsureDefaultModel(SelectedUiProvider());
        return ResolveSelectedModel();
    }

    private void OnSessionStatusChanged()
    {
        void paint() => PaintFromSession();
        if (_invokeOnUi != null)
        {
            _invokeOnUi(paint);
            return;
        }

        paint();
    }

    private void ScheduleProbe()
    {
        if (_persist.IsLoading || _localOnly)
        {
            return;
        }

        CancelProbeDelay();
        _probeDelay = _delays.Schedule(KeyProbeDelay, () =>
        {
            _probeDelay = null;
            _ = ProbeNowAsync();
        });
    }

    private void CancelProbeDelay()
    {
        _probeDelay?.Dispose();
        _probeDelay = null;
    }

    private bool IsProbePendingOrRunning()
        => _probeDelay != null || _session.Connection.State == AiConnectionState.Checking;

    private bool ShouldSkipDuplicateProbe(string provider, string key, string model)
    {
        if (_lastProbeProvider == null
            || !string.Equals(_lastProbeProvider, provider, StringComparison.OrdinalIgnoreCase)
            || _lastProbeKey != key
            || !string.Equals(_lastProbeModel, model, StringComparison.Ordinal)
            || _lastProbeLocalOnly != _localOnly)
        {
            return false;
        }

        // Retry after Failed (or other non-success states); skip while still
        // Connected or Checking for the same inputs.
        var state = _session.Connection.State;
        return state is AiConnectionState.Connected or AiConnectionState.Checking;
    }

    private async Task ProbeNowAsync()
    {
        if (_disposed || _persist.IsLoading)
        {
            return;
        }

        var provider = SelectedUiProvider();
        var key = _apiKeyText.Trim();
        var model = ResolveSelectedModel();
        if (ShouldSkipDuplicateProbe(provider, key, model))
        {
            return;
        }

        _lastProbeProvider = provider;
        _lastProbeKey = key;
        _lastProbeModel = model;
        _lastProbeLocalOnly = _localOnly;

        await _session.ProbeAsyncCore(
            _localOnly,
            provider,
            key,
            model,
            _isWaitingForClipboard);
    }

    private bool ShouldProbeSavedProvider()
    {
        return !_localOnly;
    }

    private void StartClipboardWatch(string provider)
    {
        StopClipboardWatch();
        _waitingProvider = provider;
        if (!_clipboard.Start())
        {
            SetOverrideStatus(ClipboardWatchFailedMessage, AiStatusKind.Warning);
            _waitingProvider = null;
            return;
        }

        IsWaitingForClipboard = true;
        _clipboardTimeout?.Dispose();
        _clipboardTimeout = _delays.Schedule(ClipboardWatchTimeout, () =>
        {
            _clipboardTimeout = null;
            StopClipboardWatch(ClipboardTimedOutMessage);
        });
        SetOverrideStatus(WatchingClipboardMessage, AiStatusKind.Info);
    }

    private void StopClipboardWatch(string? status = null)
    {
        _clipboardTimeout?.Dispose();
        _clipboardTimeout = null;
        _clipboard.Stop();
        _waitingProvider = null;
        IsWaitingForClipboard = false;
        if (!string.IsNullOrEmpty(status))
        {
            SetOverrideStatus(status, AiStatusKind.Secondary);
        }
    }

    private void OnClipboardUpdated()
    {
        var provider = _waitingProvider;
        if (string.IsNullOrEmpty(provider) || !_isWaitingForClipboard)
        {
            return;
        }

        string? text;
        try
        {
            text = _clipboard.ReadText();
        }
        catch
        {
            return;
        }

        if (!AiProviderCatalog.LooksLikeApiKey(provider, text))
        {
            return;
        }

        var key = AiProviderCatalog.FirstLine(text);
        StopClipboardWatch();
        _apiKeyText = key;
        OnPropertyChanged(nameof(ApiKeyText));
        CancelProbeDelay();
        _ = ProbeNowAsync();
    }

    private void UpdateEntryMode(bool fillModels = true, string? preferredModel = null)
    {
        var provider = SelectedUiProvider();
        var needsKey = AiProviderCatalog.UsesApiKey(provider) && !_localOnly;
        ShowKeyBox = needsKey;
        ShowGetKeyButton = needsKey;
        ShowModel = !provider.Equals("None", StringComparison.OrdinalIgnoreCase) && !_localOnly;
        if (ShowModel && fillModels)
        {
            FillModelChoices(provider, preferredModel ?? _selectedModel);
        }

        if (!needsKey)
        {
            StopClipboardWatch();
        }

        if (!_advancedVisible)
        {
            Heading = "OpenAI (recommended)";
        }
        else if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            Heading = "Ollama (local)";
        }
        else if (provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            Heading = "No AI provider";
        }
        else
        {
            Heading = provider;
        }

        AiFlagsEnabled = !_localOnly;
    }

    private void FillModelChoices(string provider, string? selected)
    {
        var models = AiModelChoices.ForProvider(provider, selected);
        if (models.Count == 0)
        {
            _models.Clear();
            _selectedModel = null;
            OnPropertyChanged(nameof(SelectedModel));
            return;
        }

        var pick = AiModelChoices.ResolveSelected(provider, string.IsNullOrWhiteSpace(selected) ? null : selected);
        if (!models.Contains(pick))
        {
            pick = models[0];
        }

        var previous = _persist.IsLoading;
        _persist.IsLoading = true;
        try
        {
            _models.Clear();
            foreach (var model in models)
            {
                _models.Add(model);
            }

            _selectedModel = pick;
            OnPropertyChanged(nameof(SelectedModel));
        }
        finally
        {
            _persist.IsLoading = previous;
        }
    }

    private void EnsureDefaultModel(string provider)
    {
        if (string.IsNullOrEmpty(_selectedModel) || _models.Count == 0)
        {
            FillModelChoices(provider, AiProviderCatalog.DefaultModel(provider));
        }
    }

    private string SelectedUiProvider()
        => AiModelChoices.ResolveUiProvider(
            _advancedVisible,
            _advancedVisible ? ProviderLabels[_providerIndex] : AiProviderCatalog.Recommended);

    private string ResolveSelectedModel()
    {
        var provider = SelectedUiProvider();
        return AiModelChoices.ResolveSelected(provider, _selectedModel);
    }

    private string CurrentModelOrDefault(string provider)
    {
        if (!string.IsNullOrWhiteSpace(_selectedModel))
        {
            return _selectedModel;
        }

        return string.IsNullOrWhiteSpace(_settings.AIModel)
            ? AiProviderCatalog.DefaultModel(provider)
            : _settings.AIModel;
    }

    private int ResolveStoredProviderIndex(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return IndexOfProvider(AiProviderCatalog.Recommended);
        }

        var index = IndexOfProvider(stored);
        return index >= 0 ? index : IndexOfProvider(AiProviderCatalog.Recommended);
    }

    private static int IndexOfProvider(string name)
    {
        for (var i = 0; i < ProviderLabels.Count; i++)
        {
            if (string.Equals(ProviderLabels[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private void PaintFromSession()
    {
        StatusActiveLine = _session.Connection.ActiveLine;
        StatusDetail = _session.Connection.Detail;
        StatusKind = MapStatusKind(_session.Connection);
    }

    private void SetOverrideStatus(string detail, AiStatusKind kind)
    {
        StatusActiveLine = _session.Connection.ActiveLine;
        StatusDetail = detail;
        StatusKind = kind;
    }

    private static AiStatusKind MapStatusKind(AiConnectionStatus connection)
        => connection.State switch
        {
            AiConnectionState.LocalOnlyOff => AiStatusKind.Warning,
            AiConnectionState.Checking => AiStatusKind.Info,
            AiConnectionState.Connected => AiStatusKind.Success,
            AiConnectionState.Failed => connection.Detail.StartsWith("Lexon could not switch", StringComparison.Ordinal)
                ? AiStatusKind.Warning
                : AiStatusKind.Error,
            _ => AiStatusKind.Secondary
        };

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
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
