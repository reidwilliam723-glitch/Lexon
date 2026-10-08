using System.ComponentModel;
using System.Runtime.CompilerServices;
using Lexon.Core;
using Lexon.Core.Theming;

namespace Lexon.SettingsModel;

public sealed class AppearanceSettingsViewModel : INotifyPropertyChanged, IOwnedSettingsPage
{
    public static readonly string[] OwnedKeyList =
    [
        AppSettings.ThemeKey,
        AppSettings.SuggestionSortModeKey,
        AppSettings.SuggestionPlacementKey,
        AppSettings.SuggestionAcceptKeyKey,
        AppSettings.RequireConfirmationForEditsKey
    ];

    public static IReadOnlyList<string> ThemeLabels { get; } = ["Light", "Dark", "High Contrast"];

    public static IReadOnlyList<string> SortLabels { get; } = ["Most Relevant", "Most Used"];

    public static IReadOnlyList<string> PlacementLabels { get; } = ["Below the word", "Above the word"];

    public static IReadOnlyList<string> AcceptKeyLabels { get; } = ["Tab", "Enter", "Right arrow", "Numbers only"];

    private readonly AppSettings _settings;
    private readonly PersistScheduler _persist;
    private readonly IThemeSwitcher _themes;
    private int _themeIndex;
    private int _sortIndex;
    private int _placementIndex;
    private int _acceptKeyIndex;
    private bool _previewRewrites = true;
    private bool _applyingTheme;
    private bool _isDirty;

    public AppearanceSettingsViewModel(AppSettings settings, PersistScheduler persist, IThemeSwitcher themes)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        _themes.ThemeChanged += OnExternalThemeChanged;
    }

    public IReadOnlyList<string> OwnedKeys => OwnedKeyList;

    public bool IsDirty => _isDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? UserEdited;

    public int ThemeIndex
    {
        get => _themeIndex;
        set
        {
            var index = value >= 0 && value < ThemeLabels.Count ? value : 0;
            if (_themeIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _themeIndex = index;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            var name = ThemeLabels[index];
            _settings.Theme = name;
            _applyingTheme = true;
            try
            {
                _themes.Apply(name);
            }
            finally
            {
                _applyingTheme = false;
            }

            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public int SortIndex
    {
        get => _sortIndex;
        set
        {
            var index = value == 1 ? 1 : 0;
            if (_sortIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _sortIndex = index;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.SuggestionSortMode = index == 1 ? "Used" : "Relevant";
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public int PlacementIndex
    {
        get => _placementIndex;
        set
        {
            var index = value == 1 ? 1 : 0;
            if (_placementIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _placementIndex = index;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.SuggestionPlacement = index == 1 ? "Above" : "Below";
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public int AcceptKeyIndex
    {
        get => _acceptKeyIndex;
        set
        {
            var index = value is >= 0 and <= 3 ? value : 0;
            if (_acceptKeyIndex == index)
            {
                if (value != index)
                {
                    OnPropertyChanged();
                }

                return;
            }

            _acceptKeyIndex = index;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.SuggestionAcceptKey = StoredAcceptKey(index);
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public bool PreviewRewrites
    {
        get => _previewRewrites;
        set
        {
            if (_previewRewrites == value)
            {
                return;
            }

            _previewRewrites = value;
            if (_persist.IsLoading)
            {
                OnPropertyChanged();
                return;
            }

            _settings.RequireConfirmationForEdits = value;
            MarkDirtyAndSchedule();
            OnPropertyChanged();
        }
    }

    public void SelectThemeByName(string name)
    {
        ThemeIndex = IndexOfTheme(name);
    }

    public void Load()
    {
        _persist.IsLoading = true;
        try
        {
            _themeIndex = IndexOfTheme(_settings.Theme);
            _sortIndex = string.Equals(_settings.SuggestionSortMode, "Used", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            _placementIndex = string.Equals(_settings.SuggestionPlacement, "Above", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            _acceptKeyIndex = IndexOfAcceptKey(_settings.SuggestionAcceptKey);
            _previewRewrites = _settings.RequireConfirmationForEdits;
            _isDirty = false;
            OnPropertyChanged(nameof(ThemeIndex));
            OnPropertyChanged(nameof(SortIndex));
            OnPropertyChanged(nameof(PlacementIndex));
            OnPropertyChanged(nameof(AcceptKeyIndex));
            OnPropertyChanged(nameof(PreviewRewrites));
        }
        finally
        {
            _persist.IsLoading = false;
        }
    }

    public void CopyOwnedTo(AppSettings target)
    {
        target.Theme = ThemeLabels[_themeIndex];
        target.SuggestionSortMode = _sortIndex == 1 ? "Used" : "Relevant";
        target.SuggestionPlacement = _placementIndex == 1 ? "Above" : "Below";
        target.SuggestionAcceptKey = StoredAcceptKey(_acceptKeyIndex);
        target.RequireConfirmationForEdits = _previewRewrites;
    }

    public void MarkClean() => _isDirty = false;

    private void MarkDirtyAndSchedule()
    {
        _isDirty = true;
        _persist.Schedule(DateTime.UtcNow);
        UserEdited?.Invoke();
    }

    private void OnExternalThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        if (_applyingTheme || _persist.IsLoading)
        {
            return;
        }

        // Startup reads ThemeManager's theme_config storage, not AppSettings.Theme.
        // ThemeManager.SetTheme already persisted theme_config; gallery buttons go
        // through ThemeIndex (dirty + schedule) so the profile Theme key stays in
        // sync for the classic combo. External changes only update the in-memory
        // selection — writing Theme here would be vestigial for live UI.
        var index = IndexOfTheme(e.NewTheme.Name);
        _settings.Theme = ThemeLabels[index];
        if (_themeIndex == index)
        {
            return;
        }

        _themeIndex = index;
        OnPropertyChanged(nameof(ThemeIndex));
    }

    private static string StoredAcceptKey(int index) => index switch
    {
        1 => SuggestionAcceptKey.Enter,
        2 => SuggestionAcceptKey.Right,
        3 => SuggestionAcceptKey.Numbers,
        _ => SuggestionAcceptKey.Tab
    };

    private static int IndexOfAcceptKey(string? stored) => SuggestionAcceptKey.Normalize(stored) switch
    {
        SuggestionAcceptKey.Enter => 1,
        SuggestionAcceptKey.Right => 2,
        SuggestionAcceptKey.Numbers => 3,
        _ => 0
    };

    private static int IndexOfTheme(string? name)
    {
        for (var i = 0; i < ThemeLabels.Count; i++)
        {
            if (string.Equals(ThemeLabels[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
