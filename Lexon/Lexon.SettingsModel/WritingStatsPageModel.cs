using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Lexon.SettingsModel;

public sealed class WritingStatsPageModel : INotifyPropertyChanged
{
    private readonly IWritingStatsReader _reader;
    private readonly Action _back;
    private string _accepted = "—";
    private string _expansionUses = "—";
    private string _keystrokes = "—";
    private string _timeSaved = "—";
    private string _unavailable = string.Empty;

    public WritingStatsPageModel(IWritingStatsReader reader, Action back)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _back = back ?? throw new ArgumentNullException(nameof(back));
        TopExpansions = [];
    }

    public ObservableCollection<string> TopExpansions { get; }

    public string Accepted
    {
        get => _accepted;
        private set => Set(ref _accepted, value);
    }

    public string ExpansionUses
    {
        get => _expansionUses;
        private set => Set(ref _expansionUses, value);
    }

    public string KeystrokesSaved
    {
        get => _keystrokes;
        private set => Set(ref _keystrokes, value);
    }

    public string TimeSaved
    {
        get => _timeSaved;
        private set => Set(ref _timeSaved, value);
    }

    public string Unavailable
    {
        get => _unavailable;
        private set => Set(ref _unavailable, value);
    }

    public bool IsAvailable => string.IsNullOrEmpty(Unavailable);

    public void Refresh()
    {
        var stats = _reader.Read();
        if (stats == null)
        {
            Unavailable = "Statistics are available only while Lexon is running.";
            Accepted = "—";
            ExpansionUses = "—";
            KeystrokesSaved = "—";
            TimeSaved = "—";
            TopExpansions.Clear();
            OnPropertyChanged(nameof(IsAvailable));
            return;
        }

        Unavailable = string.Empty;
        Accepted = stats.SuggestionsAccepted.ToString("N0", CultureInfo.CurrentCulture);
        ExpansionUses = stats.ExpansionUses.ToString("N0", CultureInfo.CurrentCulture);
        KeystrokesSaved = stats.KeystrokesSaved.ToString("N0", CultureInfo.CurrentCulture);
        TimeSaved = FormatDuration(stats.EstimatedSecondsSaved);
        TopExpansions.Clear();
        if (stats.TopExpansions.Count == 0)
        {
            TopExpansions.Add("No expansions recorded");
        }
        else
        {
            foreach (var (trigger, uses) in stats.TopExpansions)
            {
                TopExpansions.Add($"{trigger} — {uses.ToString("N0", CultureInfo.CurrentCulture)}");
            }
        }

        OnPropertyChanged(nameof(IsAvailable));
    }

    public void Back() => _back();

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string FormatDuration(double seconds)
    {
        if (seconds < 60)
        {
            return $"{seconds:0.0} seconds";
        }

        var minutes = (int)(seconds / 60);
        var remainder = seconds - (minutes * 60);
        return remainder < 0.5
            ? $"{minutes} minutes"
            : $"{minutes} minutes {remainder:0} seconds";
    }

    private void Set(ref string field, string value, [CallerMemberName] string? name = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
