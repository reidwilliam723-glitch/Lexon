using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lexon.SettingsModel;

public sealed class LearnedWordsPageModel : INotifyPropertyChanged
{
    private readonly ILearnedWordCatalog _catalog;
    private readonly IMessageService? _messages;
    private readonly Action _back;
    private string _search = string.Empty;
    private string _addText = string.Empty;

    public LearnedWordsPageModel(ILearnedWordCatalog catalog, IMessageService? messages, Action back)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _messages = messages;
        _back = back ?? throw new ArgumentNullException(nameof(back));
        VisibleWords = [];
    }

    public ObservableCollection<string> VisibleWords { get; }

    public string SearchText
    {
        get => _search;
        set
        {
            if (_search == value)
            {
                return;
            }

            _search = value ?? string.Empty;
            OnPropertyChanged();
            Refresh();
        }
    }

    public string AddText
    {
        get => _addText;
        set => Set(ref _addText, value ?? string.Empty);
    }

    public void Refresh()
    {
        var query = _search.Trim();
        VisibleWords.Clear();
        foreach (var word in _catalog.Words)
        {
            if (query.Length == 0
                || word.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                VisibleWords.Add(word);
            }
        }
    }

    public void Delete(IReadOnlyList<string> words)
    {
        var chosen = words.Where(static word => !string.IsNullOrWhiteSpace(word)).ToArray();
        if (chosen.Length == 0)
        {
            return;
        }

        _catalog.Remove(chosen);
        Refresh();
    }

    public void NeverLearn(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return;
        }

        _catalog.NeverLearn(word);
        Refresh();
    }

    public void UndoLast()
    {
        _catalog.UndoLast();
        Refresh();
    }

    public void Add()
    {
        var word = _addText.Trim();
        if (word.Length == 0)
        {
            return;
        }

        _catalog.Add(word);
        AddText = string.Empty;
        Refresh();
    }

    public void ClearAll()
    {
        if (_messages != null
            && !_messages.Confirm(
                "Remove every learned custom word? Dictionary words are not affected.",
                "Clear learned words"))
        {
            return;
        }

        _catalog.Clear();
        Refresh();
    }

    public void Back() => _back();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set(ref string field, string value, [CallerMemberName] string? name = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
