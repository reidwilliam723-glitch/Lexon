using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lexon.SettingsModel;

public sealed class AppsSettingsViewModel : INotifyPropertyChanged, IOwnedSettingsPage
{
    public static readonly string[] OwnedKeyList =
    [
        AppSettings.BlockedApplicationsKey,
        AppSettings.AppCategoryOverridesKey,
        AppSettings.GrammarMutedAppsKey,
        AppSettings.LearnedWordsMutedAppsKey
    ];

    private readonly AppSettings _settings;
    private readonly PersistScheduler _persist;
    private readonly IProcessPicker _picker;
    private readonly ObservableCollection<string> _apps = [];
    private readonly HashSet<string> _sessionApps = new(StringComparer.OrdinalIgnoreCase);
    private PrivacySettingsViewModel? _privacy;
    private AppToneViewModel? _tone;
    private WritingViewModel? _writing;
    private int _selectedIndex = -1;
    private bool _blockAssistance;
    private int _toneIndex;
    private bool _grammar = true;
    private bool _learnedWords = true;
    private bool _loading = true;
    private bool _isDirty;

    public AppsSettingsViewModel(AppSettings settings, PersistScheduler persist, IProcessPicker picker)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
    }

    public IReadOnlyList<string> OwnedKeys => OwnedKeyList;

    public static IReadOnlyList<string> ToneChoices => AppControlEditor.ToneChoices;

    public bool IsDirty => _isDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? UserEdited;

    public ObservableCollection<string> Apps => _apps;

    public bool HasApp => SelectedIndex >= 0 && SelectedIndex < _apps.Count;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var index = value >= 0 && value < _apps.Count ? value : -1;
            if (_selectedIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _selectedIndex = index;
            LoadSelected();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasApp));
        }
    }

    public bool BlockAssistance
    {
        get => _blockAssistance;
        set => SetFlag(ref _blockAssistance, value);
    }

    public int ToneIndex
    {
        get => _toneIndex;
        set
        {
            var index = value >= 0 && value < AppControlEditor.ToneChoices.Count ? value : 0;
            if (_toneIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _toneIndex = index;
            OnPropertyChanged();
            ApplySelected();
        }
    }

    public bool Grammar
    {
        get => _grammar;
        set => SetFlag(ref _grammar, value);
    }

    public bool LearnedWords
    {
        get => _learnedWords;
        set => SetFlag(ref _learnedWords, value);
    }

    public void Attach(PrivacySettingsViewModel? privacy, AppToneViewModel? tone, WritingViewModel? writing)
    {
        _privacy = privacy;
        _tone = tone;
        _writing = writing;
    }

    public void Load()
    {
        _loading = true;
        _persist.IsLoading = true;
        try
        {
            var selected = HasApp ? _apps[_selectedIndex] : null;
            _apps.Clear();
            foreach (var app in AppControlEditor.Apps(_settings).Concat(_sessionApps).Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase))
            {
                _apps.Add(app);
            }

            _selectedIndex = selected == null ? -1 : IndexOf(selected);
            LoadSelected();
            _isDirty = false;
            OnPropertyChanged(nameof(SelectedIndex));
            OnPropertyChanged(nameof(HasApp));
        }
        finally
        {
            _persist.IsLoading = false;
            _loading = false;
        }
    }

    public void CopyOwnedTo(AppSettings target)
    {
        if (_privacy is not { IsDirty: true }
            || SameList(BlockedAppList.Parse(_privacy.BlockedAppsText), _settings.BlockedApplications))
        {
            target.BlockedApplications = _settings.BlockedApplications?.ToList() ?? [];
        }

        if (_tone is not { IsDirty: true }
            || SameList(_tone.Rows, _settings.AppCategoryOverrides))
        {
            target.AppCategoryOverrides = _settings.AppCategoryOverrides?.ToList() ?? [];
        }

        if (_writing is not { IsDirty: true }
            || (SameList(BlockedAppList.ParseMutedGrammar(_writing.MutedAppsText), _settings.GrammarMutedApps)
                && SameList(BlockedAppList.ParseMutedGrammar(_writing.LearnedMutedAppsText), _settings.LearnedWordsMutedApps)))
        {
            target.GrammarMutedApps = _settings.GrammarMutedApps?.ToList() ?? [];
            target.LearnedWordsMutedApps = _settings.LearnedWordsMutedApps?.ToList() ?? [];
        }
    }

    public void MarkClean() => _isDirty = false;

    public void AddRunningApp()
    {
        var picked = _picker.Pick("Select a running application:");
        if (!string.IsNullOrEmpty(picked))
        {
            SelectApp(picked);
        }
    }

    public void SelectApp(string? name)
    {
        var app = Lexon.Core.ApplicationName.Normalize(name);
        if (app.Length == 0)
        {
            return;
        }

        _sessionApps.Add(app);
        if (IndexOf(app) < 0)
        {
            _apps.Add(app);
        }

        SelectedIndex = IndexOf(app);
    }

    private void SetFlag(ref bool field, bool value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged();
        ApplySelected();
    }

    private void LoadSelected()
    {
        _loading = true;
        try
        {
            if (!HasApp)
            {
                _blockAssistance = false;
                _toneIndex = 0;
                _grammar = true;
                _learnedWords = true;
            }
            else
            {
                var state = AppControlEditor.Read(_settings, _apps[_selectedIndex]);
                _blockAssistance = state.BlockAssistance;
                _toneIndex = ToneIndexOf(state.Tone);
                _grammar = state.Grammar;
                _learnedWords = state.LearnedWords;
            }

            OnPropertyChanged(nameof(BlockAssistance));
            OnPropertyChanged(nameof(ToneIndex));
            OnPropertyChanged(nameof(Grammar));
            OnPropertyChanged(nameof(LearnedWords));
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplySelected()
    {
        if (_loading || _persist.IsLoading || !HasApp)
        {
            return;
        }

        AppControlEditor.Apply(_settings, new AppControlState(
            _apps[_selectedIndex],
            _blockAssistance,
            AppControlEditor.ToneChoices[_toneIndex],
            _grammar,
            _learnedWords));
        _privacy?.AcceptExternalBlocked(_settings.BlockedApplications);
        _tone?.AcceptExternalRows(_settings.AppCategoryOverrides);
        _writing?.AcceptExternalMutes(_settings.GrammarMutedApps, _settings.LearnedWordsMutedApps);
        _isDirty = true;
        _persist.Schedule(DateTime.UtcNow);
        UserEdited?.Invoke();
    }

    private int IndexOf(string app)
    {
        for (var i = 0; i < _apps.Count; i++)
        {
            if (string.Equals(_apps[i], app, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static int ToneIndexOf(string tone)
    {
        for (var i = 0; i < AppControlEditor.ToneChoices.Count; i++)
        {
            if (string.Equals(AppControlEditor.ToneChoices[i], tone, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private static bool SameList(IEnumerable<string>? left, IEnumerable<string>? right)
    {
        var a = (left ?? []).Where(static s => !string.IsNullOrWhiteSpace(s)).ToList();
        var b = (right ?? []).Where(static s => !string.IsNullOrWhiteSpace(s)).ToList();
        return a.Count == b.Count && a.Zip(b).All(pair => string.Equals(pair.First, pair.Second, StringComparison.Ordinal));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
