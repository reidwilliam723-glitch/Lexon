using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Lexon.Core;
using Lexon.Core.Grammar;

namespace Lexon.SettingsModel;

public sealed class CheckLexonViewModel : INotifyPropertyChanged, IOwnedSettingsPage
{
    public static readonly string[] OwnedKeyList =
    [
        AppSettings.QuietHoursEnabledKey,
        AppSettings.QuietHoursStartKey,
        AppSettings.QuietHoursEndKey,
        AppSettings.EditHistoryEnabledKey
    ];

    private readonly AppSettings _settings;
    private readonly PersistScheduler _persist;
    private readonly ILexonCheck? _check;
    private readonly IEditHistory? _history;
    private bool _quietHours;
    private string _quietStart = "22:00";
    private string _quietEnd = "07:00";
    private bool _historyEnabled = true;
    private string _running = "Available while Lexon is running.";
    private string _enabled = "Available while Lexon is running.";
    private string _currentApp = "Available while Lexon is running.";
    private string _canInteract = "Available while Lexon is running.";
    private string _report = string.Empty;
    private bool _isDirty;

    public CheckLexonViewModel(
        AppSettings settings,
        PersistScheduler persist,
        ILexonCheck? check = null,
        IEditHistory? history = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _check = check;
        _history = history;
        HistoryRows = new ObservableCollection<EditHistoryRow>();
        if (_history != null)
        {
            _history.Changed += (_, _) => RefreshHistory();
        }
    }

    public IReadOnlyList<string> OwnedKeys => OwnedKeyList;

    public bool IsDirty => _isDirty;

    public ObservableCollection<EditHistoryRow> HistoryRows { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? UserEdited;

    public bool QuietHoursEnabled
    {
        get => _quietHours;
        set => SetBool(ref _quietHours, value, v => _settings.QuietHoursEnabled = v);
    }

    public string QuietHoursStart
    {
        get => _quietStart;
        set => SetTime(ref _quietStart, value, v => _settings.QuietHoursStart = v);
    }

    public string QuietHoursEnd
    {
        get => _quietEnd;
        set => SetTime(ref _quietEnd, value, v => _settings.QuietHoursEnd = v);
    }

    public bool EditHistoryEnabled
    {
        get => _historyEnabled;
        set
        {
            SetBool(ref _historyEnabled, value, v => _settings.EditHistoryEnabled = v);
            if (_history != null && !_persist.IsLoading)
            {
                _history.Enabled = value;
            }
        }
    }

    public string RunningText
    {
        get => _running;
        private set => SetDisplay(ref _running, value);
    }

    public string EnabledText
    {
        get => _enabled;
        private set => SetDisplay(ref _enabled, value);
    }

    public string CurrentAppText
    {
        get => _currentApp;
        private set => SetDisplay(ref _currentApp, value);
    }

    public string CanInteractText
    {
        get => _canInteract;
        private set => SetDisplay(ref _canInteract, value);
    }

    public string ReportText => _report;

    public void Load()
    {
        _persist.IsLoading = true;
        try
        {
            _quietHours = _settings.QuietHoursEnabled;
            _quietStart = string.IsNullOrWhiteSpace(_settings.QuietHoursStart) ? "22:00" : _settings.QuietHoursStart;
            _quietEnd = string.IsNullOrWhiteSpace(_settings.QuietHoursEnd) ? "07:00" : _settings.QuietHoursEnd;
            _historyEnabled = _settings.EditHistoryEnabled;
            if (_history != null)
            {
                _history.Enabled = _historyEnabled;
            }

            _isDirty = false;
            OnPropertyChanged(nameof(QuietHoursEnabled));
            OnPropertyChanged(nameof(QuietHoursStart));
            OnPropertyChanged(nameof(QuietHoursEnd));
            OnPropertyChanged(nameof(EditHistoryEnabled));
            RefreshStatus();
        }
        finally
        {
            _persist.IsLoading = false;
        }
    }

    public void CopyOwnedTo(AppSettings target)
    {
        target.QuietHoursEnabled = _quietHours;
        target.QuietHoursStart = _quietStart;
        target.QuietHoursEnd = _quietEnd;
        target.EditHistoryEnabled = _historyEnabled;
    }

    public void MarkClean() => _isDirty = false;

    public void RefreshStatus()
    {
        if (_check == null)
        {
            RunningText = "Available while Lexon is running.";
            EnabledText = "Available while Lexon is running.";
            CurrentAppText = "Available while Lexon is running.";
            CanInteractText = "Available while Lexon is running.";
            _report = string.Empty;
        }
        else
        {
            var status = _check.Capture();
            RunningText = status.IsRunning ? "Lexon is running." : "Lexon is not running.";
            EnabledText = status.IsEnabled ? "Suggestions are on." : "Lexon is paused.";
            CurrentAppText = string.IsNullOrWhiteSpace(status.CurrentProcess)
                ? "No app is in focus."
                : status.CurrentProcess;
            CanInteractText = LexonStatusReport.CanInteract(status)
                ? "Lexon can offer suggestions here."
                : Reason(status);
            _report = LexonStatusReport.Format(status);
        }

        OnPropertyChanged(nameof(ReportText));
        RefreshHistory();
    }

    public void ClearHistory()
    {
        _history?.Clear();
        RefreshHistory();
    }

    public void UndoLatest()
    {
        if (_history == null || !_history.CanUndoLatest)
        {
            return;
        }

        _history.UndoLatest();
        RefreshHistory();
    }

    private void RefreshHistory()
    {
        HistoryRows.Clear();
        if (_history == null)
        {
            return;
        }

        var first = true;
        foreach (var entry in _history.Entries)
        {
            HistoryRows.Add(new EditHistoryRow(
                string.IsNullOrEmpty(entry.Original) ? "(inserted)" : entry.Original,
                entry.Replacement,
                first && _history.CanUndoLatest));
            first = false;
        }

        OnPropertyChanged(nameof(HistoryRows));
    }

    private static string Reason(LexonStatusSnapshot status)
    {
        if (!status.IsEnabled)
        {
            return "Lexon is paused.";
        }

        if (!status.FocusedWindow)
        {
            return "No app is in focus.";
        }

        if (status.SecureField)
        {
            return "This field is hidden, such as a password box.";
        }

        if (status.AppPaused)
        {
            return "Lexon is paused in this app.";
        }

        if (status.QuietHours)
        {
            return "Quiet hours are on.";
        }

        return "Lexon can offer suggestions here.";
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

    private void SetTime(ref string field, string? value, Action<string> apply, [CallerMemberName] string? name = null)
    {
        var text = (value ?? string.Empty).Trim();
        if (!QuietHours.TryParse(text, out var time))
        {
            OnPropertyChanged(name);
            return;
        }

        text = time.ToString("HH:mm");
        if (field == text)
        {
            return;
        }

        field = text;
        if (_persist.IsLoading)
        {
            OnPropertyChanged(name);
            return;
        }

        apply(text);
        MarkDirtyAndSchedule();
        OnPropertyChanged(name);
    }

    private void SetDisplay(ref string field, string value, [CallerMemberName] string? name = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(name);
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

public sealed class EditHistoryRow
{
    public EditHistoryRow(string original, string replacement, bool canUndo)
    {
        Original = original;
        Replacement = replacement;
        CanUndo = canUndo;
    }

    public string Original { get; }

    public string Replacement { get; }

    public bool CanUndo { get; }
}
