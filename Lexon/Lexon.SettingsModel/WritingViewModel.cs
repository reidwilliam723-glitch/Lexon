using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lexon.SettingsModel;

public sealed class WritingViewModel : INotifyPropertyChanged, IOwnedSettingsPage
{
    public const string UnavailableStyleSummary = "Available while Lexon is running.";
    public const string UnavailableLearningMessage = "Learned data is available while Lexon is running.";
    public const string ImportSuccessMessage = "Imported learned vocabulary and writing style.";
    public const string ImportInvalidMessage = "That file is not a valid Lexon learning export.";
    public const string ResetConfirmMessage =
        "Clear the learned writing-style profile only? Vocabulary and other personalization are left unchanged.";
    public const string SaveFailedMessage = "Couldn't save that file.";
    public const string ReadFailedMessage = "Couldn't read that file.";
    public const string LearningFilter = "Lexon learning (*.json)|*.json";
    public const string DefaultExportName = "lexon-learning.json";

    public static readonly string[] OwnedKeyList =
    [
        AppSettings.GrammarCheckingKey,
        AppSettings.AutoCorrectTyposKey,
        AppSettings.AutoInsertSpacesKey,
        AppSettings.AutoCorrectContractionsKey,
        AppSettings.DocumentConsistencyCheckingKey,
        AppSettings.AllowCodeSwitchingKey,
        AppSettings.GrammarSensitivityKey,
        AppSettings.MuteGrammarForCasualAppsKey,
        AppSettings.GrammarMutedAppsKey,
        AppSettings.EnableRewriteHotkeyKey,
        AppSettings.EnableGrammarHotkeyKey,
        AppSettings.CustomTerminologyKey,
        AppSettings.AppTerminologyOverridesKey,
        AppSettings.DefaultWritingModeKey,
        AppSettings.UseLearnedWordsKey,
        AppSettings.LearnedWordsMutedAppsKey
    ];

    public static IReadOnlyList<string> SensitivityLabels { get; } = ["Low", "Medium", "High"];

    public static IReadOnlyList<string> WritingModeLabels { get; } =
    [
        "Plain language",
        "More formal",
        "More concise",
        "Expand",
        "Fix grammar"
    ];

    private readonly AppSettings _settings;
    private readonly PersistScheduler _persist;
    private readonly IPersonalizationService? _personalization;
    private readonly IWritingDialogs? _dialogs;
    private readonly IFileDialogService? _files;
    private readonly IMessageService? _messages;
    private readonly ObservableCollection<AdaptationItem> _adaptations = [];
    private bool _grammarChecking = true;
    private bool _autoCorrectTypos = true;
    private bool _autoInsertSpaces = true;
    private bool _autoCorrectContractions;
    private bool _documentConsistencyChecking = true;
    private bool _allowCodeSwitching = true;
    private int _sensitivityIndex = 1;
    private bool _muteCasual;
    private string _mutedAppsText = string.Empty;
    private bool _useLearnedWords = true;
    private string _learnedMutedAppsText = string.Empty;
    private bool _enableRewriteHotkey = true;
    private bool _enableGrammarHotkey = true;
    private int _writingModeIndex;
    private string _styleSummary = UnavailableStyleSummary;
    private int _selectedAdaptationIndex = -1;
    private bool _isDirty;

    public WritingViewModel(
        AppSettings settings,
        PersistScheduler persist,
        IPersonalizationService? personalization = null,
        IWritingDialogs? dialogs = null,
        IFileDialogService? files = null,
        IMessageService? messages = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _personalization = personalization;
        _dialogs = dialogs;
        _files = files;
        _messages = messages;
    }

    public IReadOnlyList<string> OwnedKeys => OwnedKeyList;

    public bool IsDirty => _isDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? UserEdited;

    public ObservableCollection<AdaptationItem> Adaptations => _adaptations;

    public bool GrammarChecking
    {
        get => _grammarChecking;
        set => SetBool(ref _grammarChecking, value, v => _settings.GrammarChecking = v);
    }

    public bool AutoCorrectTypos
    {
        get => _autoCorrectTypos;
        set => SetBool(ref _autoCorrectTypos, value, v => _settings.AutoCorrectTypos = v);
    }

    public bool AutoInsertSpaces
    {
        get => _autoInsertSpaces;
        set => SetBool(ref _autoInsertSpaces, value, v => _settings.AutoInsertSpaces = v);
    }

    public bool AutoCorrectContractions
    {
        get => _autoCorrectContractions;
        set => SetBool(ref _autoCorrectContractions, value, v => _settings.AutoCorrectContractions = v);
    }

    public bool DocumentConsistencyChecking
    {
        get => _documentConsistencyChecking;
        set => SetBool(ref _documentConsistencyChecking, value, v => _settings.DocumentConsistencyChecking = v);
    }

    public bool AllowCodeSwitching
    {
        get => _allowCodeSwitching;
        set => SetBool(ref _allowCodeSwitching, value, v => _settings.AllowCodeSwitching = v);
    }

    public int SensitivityIndex
    {
        get => _sensitivityIndex;
        set
        {
            var index = value >= 0 && value < SensitivityLabels.Count ? value : 1;
            if (_sensitivityIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _sensitivityIndex = index;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.GrammarSensitivity = SensitivityLabels[index];
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public bool MuteGrammarForCasualApps
    {
        get => _muteCasual;
        set => SetBool(ref _muteCasual, value, v => _settings.MuteGrammarForCasualApps = v);
    }

    public string MutedAppsText
    {
        get => _mutedAppsText;
        set
        {
            if (_mutedAppsText == value)
            {
                return;
            }

            _mutedAppsText = value ?? string.Empty;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.GrammarMutedApps = BlockedAppList.ParseMutedGrammar(_mutedAppsText);
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public bool UseLearnedWords
    {
        get => _useLearnedWords;
        set => SetBool(ref _useLearnedWords, value, v => _settings.UseLearnedWords = v);
    }

    public string LearnedMutedAppsText
    {
        get => _learnedMutedAppsText;
        set
        {
            if (_learnedMutedAppsText == value)
            {
                return;
            }

            _learnedMutedAppsText = value ?? string.Empty;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.LearnedWordsMutedApps = BlockedAppList.ParseMutedGrammar(_learnedMutedAppsText);
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public void AcceptExternalMutes(IReadOnlyList<string> grammarMuted, IReadOnlyList<string> learnedMuted)
    {
        _mutedAppsText = BlockedAppList.FormatCsv(grammarMuted);
        _learnedMutedAppsText = BlockedAppList.FormatCsv(learnedMuted);
        _settings.GrammarMutedApps = grammarMuted.ToList();
        _settings.LearnedWordsMutedApps = learnedMuted.ToList();
        OnPropertyChanged(nameof(MutedAppsText));
        OnPropertyChanged(nameof(LearnedMutedAppsText));
    }

    public bool EnableRewriteHotkey
    {
        get => _enableRewriteHotkey;
        set => SetBool(ref _enableRewriteHotkey, value, v => _settings.EnableRewriteHotkey = v);
    }

    public bool EnableGrammarHotkey
    {
        get => _enableGrammarHotkey;
        set => SetBool(ref _enableGrammarHotkey, value, v => _settings.EnableGrammarHotkey = v);
    }

    public int WritingModeIndex
    {
        get => _writingModeIndex;
        set
        {
            var index = value >= 0 && value < WritingModeLabels.Count ? value : 0;
            if (_writingModeIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _writingModeIndex = index;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.DefaultWritingMode = WritingModeLabels[index];
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public string StyleSummary
    {
        get => _styleSummary;
        private set
        {
            if (_styleSummary == value)
            {
                return;
            }

            _styleSummary = value;
            OnPropertyChanged();
        }
    }

    public int SelectedAdaptationIndex
    {
        get => _selectedAdaptationIndex;
        set
        {
            var index = value;
            if (index < -1 || index >= _adaptations.Count)
            {
                index = -1;
            }

            if (_selectedAdaptationIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _selectedAdaptationIndex = index;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanUndoAdaptation));
        }
    }

    public bool CanUndoAdaptation =>
        _personalization != null && _selectedAdaptationIndex >= 0 && _selectedAdaptationIndex < _adaptations.Count;

    public void Load()
    {
        _persist.IsLoading = true;
        try
        {
            _grammarChecking = _settings.GrammarChecking;
            _autoCorrectTypos = _settings.AutoCorrectTypos;
            _autoInsertSpaces = _settings.AutoInsertSpaces;
            _autoCorrectContractions = _settings.AutoCorrectContractions;
            _documentConsistencyChecking = _settings.DocumentConsistencyChecking;
            _allowCodeSwitching = _settings.AllowCodeSwitching;
            _sensitivityIndex = IndexOfSensitivity(_settings.GrammarSensitivity);
            _muteCasual = _settings.MuteGrammarForCasualApps;
            var fromProfile = _settings.GrammarMutedApps ?? [];
            var currentParsed = BlockedAppList.ParseMutedGrammar(_mutedAppsText);
            if (!MutedListEquals(currentParsed, fromProfile))
            {
                _mutedAppsText = BlockedAppList.FormatCsv(fromProfile);
            }

            _useLearnedWords = _settings.UseLearnedWords;
            var learnedFromProfile = _settings.LearnedWordsMutedApps ?? [];
            var learnedParsed = BlockedAppList.ParseMutedGrammar(_learnedMutedAppsText);
            if (!MutedListEquals(learnedParsed, learnedFromProfile))
            {
                _learnedMutedAppsText = BlockedAppList.FormatCsv(learnedFromProfile);
            }

            _enableRewriteHotkey = _settings.EnableRewriteHotkey;
            _enableGrammarHotkey = _settings.EnableGrammarHotkey;
            _writingModeIndex = IndexOfWritingMode(_settings.DefaultWritingMode);
            _isDirty = false;
            OnPropertyChanged(nameof(GrammarChecking));
            OnPropertyChanged(nameof(AutoCorrectTypos));
            OnPropertyChanged(nameof(AutoInsertSpaces));
            OnPropertyChanged(nameof(AutoCorrectContractions));
            OnPropertyChanged(nameof(DocumentConsistencyChecking));
            OnPropertyChanged(nameof(AllowCodeSwitching));
            OnPropertyChanged(nameof(SensitivityIndex));
            OnPropertyChanged(nameof(MuteGrammarForCasualApps));
            OnPropertyChanged(nameof(MutedAppsText));
            OnPropertyChanged(nameof(UseLearnedWords));
            OnPropertyChanged(nameof(LearnedMutedAppsText));
            OnPropertyChanged(nameof(EnableRewriteHotkey));
            OnPropertyChanged(nameof(EnableGrammarHotkey));
            OnPropertyChanged(nameof(WritingModeIndex));
            RefreshLearning();
        }
        finally
        {
            _persist.IsLoading = false;
        }
    }

    public void RefreshLearning()
    {
        if (_personalization == null)
        {
            StyleSummary = UnavailableStyleSummary;
            _adaptations.Clear();
            _selectedAdaptationIndex = -1;
            OnPropertyChanged(nameof(SelectedAdaptationIndex));
            OnPropertyChanged(nameof(CanUndoAdaptation));
            return;
        }

        StyleSummary = _personalization.GetStyleSummary();
        _adaptations.Clear();
        foreach (var item in _personalization.GetAdaptations())
        {
            _adaptations.Add(item);
        }

        _selectedAdaptationIndex = -1;
        OnPropertyChanged(nameof(SelectedAdaptationIndex));
        OnPropertyChanged(nameof(CanUndoAdaptation));
    }

    public void CopyOwnedTo(AppSettings target)
    {
        target.GrammarChecking = _grammarChecking;
        target.AutoCorrectTypos = _autoCorrectTypos;
        target.AutoInsertSpaces = _autoInsertSpaces;
        target.AutoCorrectContractions = _autoCorrectContractions;
        target.DocumentConsistencyChecking = _documentConsistencyChecking;
        target.AllowCodeSwitching = _allowCodeSwitching;
        target.GrammarSensitivity = SensitivityLabels[_sensitivityIndex];
        target.MuteGrammarForCasualApps = _muteCasual;
        target.GrammarMutedApps = BlockedAppList.ParseMutedGrammar(_mutedAppsText);
        target.UseLearnedWords = _useLearnedWords;
        target.LearnedWordsMutedApps = BlockedAppList.ParseMutedGrammar(_learnedMutedAppsText);
        target.EnableRewriteHotkey = _enableRewriteHotkey;
        target.EnableGrammarHotkey = _enableGrammarHotkey;
        target.CustomTerminology = [.. _settings.CustomTerminology ?? []];
        target.AppTerminologyOverrides = [.. _settings.AppTerminologyOverrides ?? []];
        target.DefaultWritingMode = WritingModeLabels[_writingModeIndex];
    }

    public void MarkClean() => _isDirty = false;

    public void ShowWritingStats() => _dialogs?.ShowWritingStats();

    public void ShowLearnedWords() => _dialogs?.ShowLearnedWords();

    public void ShowTerminology()
    {
        _dialogs?.ShowTerminology(
            _settings.CustomTerminology ?? [],
            _settings.AppTerminologyOverrides ?? [],
            (global, appRows) =>
            {
                _settings.CustomTerminology = global.ToList();
                _settings.AppTerminologyOverrides = appRows.ToList();
                MarkDirtyAndSchedule();
            });
    }

    public void ExportLearning()
    {
        if (_personalization == null)
        {
            _messages?.Info(UnavailableLearningMessage, "Export");
            return;
        }

        if (_files == null)
        {
            return;
        }

        var path = _files.PickSavePath(LearningFilter, DefaultExportName);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.WriteAllText(path, _personalization.ExportLearningData());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _messages?.Info(SaveFailedMessage, "Export");
        }
    }

    public void ImportLearning()
    {
        if (_personalization == null)
        {
            _messages?.Info(UnavailableLearningMessage, "Import");
            return;
        }

        if (_files == null)
        {
            return;
        }

        var path = _files.PickOpenPath(LearningFilter);
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

        if (_personalization.ImportLearningData(json))
        {
            RefreshLearning();
            _messages?.Info(ImportSuccessMessage, "Import");
        }
        else
        {
            _messages?.Info(ImportInvalidMessage, "Import");
        }
    }

    public void ResetWritingStyle()
    {
        if (_personalization == null)
        {
            return;
        }

        if (_messages != null && !_messages.Confirm(ResetConfirmMessage, "Reset writing style"))
        {
            return;
        }

        if (_messages == null)
        {
            return;
        }

        _personalization.ResetWritingStyle();
        RefreshLearning();
    }

    public void UndoSelectedAdaptation()
    {
        if (_personalization == null || !CanUndoAdaptation)
        {
            return;
        }

        var id = _adaptations[_selectedAdaptationIndex].Id;
        _personalization.UndoAdaptation(id);
        RefreshLearning();
    }

    private void SetBool(ref bool field, bool value, Action<bool> apply, [CallerMemberName] string? name = null)
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
        OnPropertyChanged(name);
    }

    private void MarkDirtyAndSchedule()
    {
        _isDirty = true;
        _persist.Schedule(DateTime.UtcNow);
        UserEdited?.Invoke();
    }

    private static int IndexOfWritingMode(string? value)
    {
        for (var i = 0; i < WritingModeLabels.Count; i++)
        {
            if (WritingModeLabels[i].Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private static int IndexOfSensitivity(string? value)
    {
        for (var i = 0; i < SensitivityLabels.Count; i++)
        {
            if (string.Equals(SensitivityLabels[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 1; // Medium
    }

    private static bool MutedListEquals(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
