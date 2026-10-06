using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Lexon.Core.Models;

namespace Lexon.SettingsModel;

public sealed class AppToneViewModel : INotifyPropertyChanged, IOwnedSettingsPage
{
    public static readonly string[] OwnedKeyList = [AppSettings.AppCategoryOverridesKey];

    public static IReadOnlyList<string> ToneLabels { get; } = ["Casual", "Formal", "Code", "Neutral"];

    private readonly AppSettings _settings;
    private readonly PersistScheduler _persist;
    private readonly IProcessPicker _picker;
    private readonly ObservableCollection<string> _rows = [];
    private int _selectedRowIndex = -1;
    private int _selectedToneIndex;
    private bool _isDirty;

    public AppToneViewModel(AppSettings settings, PersistScheduler persist, IProcessPicker picker)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
    }

    public IReadOnlyList<string> OwnedKeys => OwnedKeyList;

    public bool IsDirty => _isDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? UserEdited;

    public ObservableCollection<string> Rows => _rows;

    public int SelectedRowIndex
    {
        get => _selectedRowIndex;
        set
        {
            var index = value;
            if (index < -1 || index >= _rows.Count)
            {
                index = -1;
            }

            if (_selectedRowIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _selectedRowIndex = index;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRemove));
        }
    }

    public int SelectedToneIndex
    {
        get => _selectedToneIndex;
        set
        {
            var index = value >= 0 && value < ToneLabels.Count ? value : 0;
            if (_selectedToneIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _selectedToneIndex = index;
            OnPropertyChanged();
            // Tone choice is not stored; it applies only when adding an app.
        }
    }

    public bool CanRemove => _selectedRowIndex >= 0 && _selectedRowIndex < _rows.Count;

    public void Load()
    {
        _persist.IsLoading = true;
        try
        {
            _rows.Clear();
            foreach (var row in _settings.AppCategoryOverrides ?? [])
            {
                if (!string.IsNullOrWhiteSpace(row))
                {
                    _rows.Add(row);
                }
            }

            _selectedRowIndex = -1;
            _selectedToneIndex = 0;
            _isDirty = false;
            OnPropertyChanged(nameof(SelectedRowIndex));
            OnPropertyChanged(nameof(SelectedToneIndex));
            OnPropertyChanged(nameof(CanRemove));
        }
        finally
        {
            _persist.IsLoading = false;
        }
    }

    public void CopyOwnedTo(AppSettings target)
    {
        target.AppCategoryOverrides = _rows
            .Where(static r => !string.IsNullOrWhiteSpace(r))
            .ToList();
    }

    public void MarkClean() => _isDirty = false;

    public void AddRunningApp()
    {
        if (!Enum.TryParse<AppWritingCategory>(ToneLabels[_selectedToneIndex], ignoreCase: true, out var category))
        {
            return;
        }

        var picked = _picker.Pick("Select a running application for this tone:");
        if (string.IsNullOrEmpty(picked))
        {
            return;
        }

        var next = AppToneList.AddOrReplace(_rows, picked, category);
        ReplaceRows(next);
        if (!_persist.IsLoading)
        {
            ApplyToSettingsAndSchedule();
        }
    }

    public void RemoveSelected()
    {
        if (!CanRemove)
        {
            return;
        }

        var next = AppToneList.RemoveAt(_rows, _selectedRowIndex);
        ReplaceRows(next);
        _selectedRowIndex = -1;
        OnPropertyChanged(nameof(SelectedRowIndex));
        OnPropertyChanged(nameof(CanRemove));
        if (!_persist.IsLoading)
        {
            ApplyToSettingsAndSchedule();
        }
    }

    private void ReplaceRows(IReadOnlyList<string> next)
    {
        _rows.Clear();
        foreach (var row in next)
        {
            if (!string.IsNullOrWhiteSpace(row))
            {
                _rows.Add(row);
            }
        }
    }

    private void ApplyToSettingsAndSchedule()
    {
        _settings.AppCategoryOverrides = _rows
            .Where(static r => !string.IsNullOrWhiteSpace(r))
            .ToList();
        _isDirty = true;
        _persist.Schedule(DateTime.UtcNow);
        UserEdited?.Invoke();
        // Tone is read from the profile at run time (AppCategoryMapper.ParseOverrides);
        // Program tray indicator and SelectionRewriteService do not cache overrides.
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
