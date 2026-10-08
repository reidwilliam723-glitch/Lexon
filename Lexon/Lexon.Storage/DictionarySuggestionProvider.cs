using Lexon.Core.Interfaces;
using Lexon.Core.Learning;
using Lexon.Core.Models;
using System.Reflection;
using System.Text.Json;

namespace Lexon.Storage;

/// <summary>
/// Suggestion provider backed by a full English lexicon (embedded) plus
/// a small persisted set of words the user has actually typed.
/// </summary>
public class DictionarySuggestionProvider : ISuggestionProvider, ILearnableSuggestionProvider
{
    private static readonly StringComparer WordComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IStorage _storage;
    private readonly Dictionary<char, string[]> _sortedLexicon = new();
    private readonly Dictionary<char, string[]> _firstLetterCommon = new();
    private readonly HashSet<string> _lexiconWords = new(WordComparer);
    private readonly Dictionary<string, int> _frequencyRank = new(WordComparer);
    private readonly HashSet<string> _learnedWords = new(WordComparer);
    private readonly Dictionary<char, List<string>> _learnedByLetter = new();
    private readonly Dictionary<string, int> _sightings = new(WordComparer);
    private readonly HashSet<string> _blockedWords = new(WordComparer);
    private readonly object _lock = new();
    private string? _lastLearnedWord;
    private DateTimeOffset? _lastLearnedAt;

    private const string LearnedWordsKey = "learned_words";
    private const string LearnedWordsResetKey = "learned_words_reset_v2";
    private const string LegacyDictionaryKey = "word_dictionary";
    private const int MaxDictionarySuggestions = 40;
    private const int ProbationSightings = 3;

    public string Name => "Dictionary";

    /// <summary>
    /// When this returns false for the current app, words that exist only because
    /// the user typed them are left out of suggestions. Null means they are included.
    /// </summary>
    public Func<string?, bool>? AllowsLearnedWords { get; set; }

    public DictionarySuggestionProvider(IStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        LoadLexicon();
        LoadLearnedWords();
    }

    public Task<IEnumerable<Suggestion>> GetSuggestionsAsync(TextContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.CurrentWord))
        {
            return Task.FromResult(Enumerable.Empty<Suggestion>());
        }

        var prefix = context.CurrentWord.ToLowerInvariant();
        var matches = new HashSet<string>(WordComparer);

        var includeLearned = AllowsLearnedWords?.Invoke(context.ApplicationName) ?? true;
        lock (_lock)
        {
            if (prefix.Length == 1)
            {
                CollectFirstLetterMatches(prefix[0], matches, includeLearned);
            }
            else
            {
                CollectPrefixMatches(prefix, matches, includeLearned);
            }
        }

        IEnumerable<Suggestion> suggestions = matches
            .Select(word => new Suggestion
            {
                Text = word,
                Source = "Dictionary",
                Score = ScoreForWord(word, prefix)
            })
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Text.Length)
            .Take(MaxDictionarySuggestions);

        return Task.FromResult(suggestions);
    }

    public Task<string?> RewriteTextAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public Task<string?> ImproveGrammarAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public Task<string?> ChangeToneAsync(string text, string tone, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public void AddWord(string word) => LearnWords(new[] { word }, null);

    public void AddWords(IEnumerable<string> words) => LearnWords(words, null);

    public void LearnWords(IEnumerable<string> words, IReadOnlyList<string>? offeredCompletions = null)
    {
        var changed = false;
        lock (_lock)
        {
            foreach (var word in words)
            {
                var normalized = Normalize(word);
                if (!ShouldLearnWord(normalized))
                {
                    continue;
                }

                if (UnfinishedWordFilter.IsPrefixOfOfferedCompletion(normalized, offeredCompletions ?? Array.Empty<string>()))
                {
                    continue;
                }

                _sightings.TryGetValue(normalized, out var count);
                count++;
                _sightings[normalized] = count;
                _lastLearnedWord = normalized;
                _lastLearnedAt = DateTimeOffset.UtcNow;
                changed = true;

                if (count >= ProbationSightings && _learnedWords.Add(normalized))
                {
                    AddToLetterList(_learnedByLetter, normalized);
                }
            }
        }

        if (changed)
        {
            SaveLearnedWords();
        }
    }

    public IReadOnlyList<string> GetLearnedWords()
    {
        lock (_lock)
        {
            return _learnedWords.OrderBy(w => w, WordComparer).ToList();
        }
    }

    public void AddExplicitWord(string word)
    {
        var normalized = Normalize(word);
        if (normalized.Length == 0)
        {
            return;
        }

        lock (_lock)
        {
            if (_blockedWords.Contains(normalized) || _lexiconWords.Contains(normalized))
            {
                return;
            }

            _sightings[normalized] = Math.Max(ProbationSightings, _sightings.GetValueOrDefault(normalized));
            _lastLearnedWord = normalized;
            _lastLearnedAt = DateTimeOffset.UtcNow;
            if (_learnedWords.Add(normalized))
            {
                AddToLetterList(_learnedByLetter, normalized);
            }
        }

        SaveLearnedWords();
    }

    public void RemoveLearnedWord(string word)
    {
        var normalized = Normalize(word);
        if (normalized.Length == 0)
        {
            return;
        }

        var changed = false;
        lock (_lock)
        {
            changed = RemoveLearnedCore(normalized);
            _sightings.Remove(normalized);
            if (string.Equals(_lastLearnedWord, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _lastLearnedWord = null;
                _lastLearnedAt = null;
            }
        }

        if (changed)
        {
            SaveLearnedWords();
        }
    }

    public void NeverLearnWord(string word)
    {
        var normalized = Normalize(word);
        if (normalized.Length == 0)
        {
            return;
        }

        lock (_lock)
        {
            _blockedWords.Add(normalized);
            RemoveLearnedCore(normalized);
            _sightings.Remove(normalized);
            if (string.Equals(_lastLearnedWord, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _lastLearnedWord = null;
                _lastLearnedAt = null;
            }
        }

        SaveLearnedWords();
    }

    public void ClearLearnedWords()
    {
        lock (_lock)
        {
            ResetLearnedState();
        }

        SaveLearnedWords();
    }

    private void ResetLearnedState()
    {
        _learnedWords.Clear();
        _learnedByLetter.Clear();
        _sightings.Clear();
        _blockedWords.Clear();
        _lastLearnedWord = null;
        _lastLearnedAt = null;
    }

    public bool IsLearnedOnly(string word)
    {
        var normalized = Normalize(word);
        if (normalized.Length == 0)
        {
            return false;
        }

        lock (_lock)
        {
            return _learnedWords.Contains(normalized) && !_lexiconWords.Contains(normalized);
        }
    }

    public bool UndoLastLearn(TimeSpan? maxAge = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(_lastLearnedWord))
            {
                return false;
            }

            if (maxAge is { } age && _lastLearnedAt is { } at && DateTimeOffset.UtcNow - at > age)
            {
                return false;
            }

            var word = _lastLearnedWord;
            if (_sightings.TryGetValue(word, out var count))
            {
                if (count <= 1)
                {
                    _sightings.Remove(word);
                }
                else
                {
                    _sightings[word] = count - 1;
                }

                if (_sightings.GetValueOrDefault(word) < ProbationSightings)
                {
                    RemoveLearnedCore(word);
                }
            }
            else
            {
                RemoveLearnedCore(word);
            }

            _lastLearnedWord = null;
            _lastLearnedAt = null;
        }

        SaveLearnedWords();
        return true;
    }

    private bool RemoveLearnedCore(string normalized)
    {
        if (!_learnedWords.Remove(normalized))
        {
            return false;
        }

        RebuildLearnedByLetter();
        return true;
    }

    private void RebuildLearnedByLetter()
    {
        _learnedByLetter.Clear();
        foreach (var learned in _learnedWords)
        {
            AddToLetterList(_learnedByLetter, learned);
        }
    }

    public int WordCount
    {
        get
        {
            lock (_lock)
            {
                return _lexiconWords.Count + _learnedWords.Count(w => !_lexiconWords.Contains(w));
            }
        }
    }

    private void CollectFirstLetterMatches(char letter, HashSet<string> matches, bool includeLearned)
    {
        if (_firstLetterCommon.TryGetValue(letter, out var common))
        {
            foreach (var word in common)
            {
                matches.Add(word);
            }
        }

        if (includeLearned && _learnedByLetter.TryGetValue(letter, out var learned))
        {
            foreach (var word in learned)
            {
                matches.Add(word);
            }
        }
    }

    private void CollectPrefixMatches(string prefix, HashSet<string> matches, bool includeLearned)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return;
        }

        var letter = prefix[0];
        if (_sortedLexicon.TryGetValue(letter, out var sorted))
        {
            var index = LowerBound(sorted, prefix);
            for (var i = index; i < sorted.Length; i++)
            {
                var word = sorted[i];
                if (!word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                matches.Add(word);
            }
        }

        if (includeLearned && _learnedByLetter.TryGetValue(letter, out var learned))
        {
            foreach (var word in learned)
            {
                if (word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(word);
                }
            }
        }
    }

    private double ScoreForWord(string word, string prefix)
    {
        if (_learnedWords.Contains(word))
        {
            return 0.94;
        }

        if (_frequencyRank.TryGetValue(word, out var rank) && _frequencyRank.Count > 1)
        {
            var frequency = 1.0 - (rank / (double)(_frequencyRank.Count - 1));
            return 0.58 + frequency * 0.30;
        }

        // Full-lexicon tails: prefer closer completions of the typed prefix
        // so obscure 20-letter words don't drown out real ones.
        var extra = Math.Max(0, word.Length - prefix.Length);
        return extra <= 4 ? 0.56 : 0.50;
    }

    private void LoadLexicon()
    {
        var frequencyWords = LoadEmbeddedLines("Lexon.Storage.Resources.common-words.txt");
        for (var i = 0; i < frequencyWords.Count; i++)
        {
            var word = Normalize(frequencyWords[i]);
            if (word.Length == 0)
            {
                continue;
            }

            if (!_frequencyRank.ContainsKey(word))
            {
                _frequencyRank[word] = _frequencyRank.Count;
            }
        }

        var buckets = new Dictionary<char, List<string>>();
        void AddLexeme(string raw)
        {
            var word = Normalize(raw);
            if (word.Length == 0 || !_lexiconWords.Add(word))
            {
                return;
            }

            AddToLetterList(buckets, word);
        }

        foreach (var word in LoadEmbeddedLines("Lexon.Storage.Resources.english-words.txt"))
        {
            AddLexeme(word);
        }

        foreach (var word in _frequencyRank.Keys)
        {
            AddLexeme(word);
        }

        foreach (var kvp in buckets)
        {
            kvp.Value.Sort(WordComparer);
            _sortedLexicon[kvp.Key] = kvp.Value.ToArray();
        }

        var firstLetter = new Dictionary<char, List<string>>();
        foreach (var word in _frequencyRank.Keys)
        {
            var letter = word[0];
            if (!firstLetter.TryGetValue(letter, out var list))
            {
                list = new List<string>(MaxDictionarySuggestions);
                firstLetter[letter] = list;
            }

            if (list.Count < MaxDictionarySuggestions)
            {
                list.Add(word);
            }
        }

        foreach (var kvp in firstLetter)
        {
            _firstLetterCommon[kvp.Key] = kvp.Value.ToArray();
        }
    }

    private void LoadLearnedWords()
    {
        try
        {
            var alreadyReset = _storage.ExistsAsync(LearnedWordsResetKey).GetAwaiter().GetResult();
            if (!alreadyReset)
            {
                WipePersistedLearnedWords();
                return;
            }

            var learnedJson = _storage.LoadAsync<string>(LearnedWordsKey).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(learnedJson))
            {
                ImportLearnedJson(learnedJson);
                PruneIncompleteLearnedWords();
            }
        }
        catch
        {
            // Ignore corrupt learned-word storage.
        }
    }

    private void WipePersistedLearnedWords()
    {
        lock (_lock)
        {
            ResetLearnedState();
        }

        TryDelete(LearnedWordsKey);
        TryDelete(LegacyDictionaryKey);
        SaveLearnedWords();
        _storage.SaveAsync(LearnedWordsResetKey, "1").GetAwaiter().GetResult();
    }

    private void TryDelete(string key)
    {
        try
        {
            _storage.DeleteAsync(key).GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort wipe of the old blob.
        }
    }

    private void ImportLearnedJson(string json)
    {
        var trimmed = json.TrimStart();
        if (trimmed.StartsWith('['))
        {
            var words = JsonSerializer.Deserialize<List<string>>(json);
            if (words != null)
            {
                ImportPromotedWords(words);
            }

            return;
        }

        var state = JsonSerializer.Deserialize<LearnedVocabularyState>(json);
        if (state == null)
        {
            return;
        }

        foreach (var blocked in state.Blocked ?? [])
        {
            var normalized = Normalize(blocked);
            if (normalized.Length > 0)
            {
                _blockedWords.Add(normalized);
            }
        }

        if (state.Sightings != null)
        {
            foreach (var kvp in state.Sightings)
            {
                var normalized = Normalize(kvp.Key);
                if (normalized.Length > 0)
                {
                    _sightings[normalized] = kvp.Value;
                }
            }
        }

        ImportPromotedWords(state.Words ?? []);
        _lastLearnedWord = Normalize(state.LastLearned);
        if (_lastLearnedWord.Length == 0)
        {
            _lastLearnedWord = null;
        }

        _lastLearnedAt = state.LastLearnedAt;
    }

    private void ImportPromotedWords(IEnumerable<string> words)
    {
        foreach (var word in words)
        {
            var normalized = Normalize(word);
            if (!ShouldLearnWord(normalized))
            {
                continue;
            }

            _sightings[normalized] = Math.Max(ProbationSightings, _sightings.GetValueOrDefault(normalized));
            if (_learnedWords.Add(normalized))
            {
                AddToLetterList(_learnedByLetter, normalized);
            }
        }
    }

    private void SaveLearnedWords()
    {
        try
        {
            LearnedVocabularyState snapshot;
            lock (_lock)
            {
                snapshot = new LearnedVocabularyState
                {
                    Words = _learnedWords.ToList(),
                    Sightings = new Dictionary<string, int>(_sightings, WordComparer),
                    Blocked = _blockedWords.ToList(),
                    LastLearned = _lastLearnedWord,
                    LastLearnedAt = _lastLearnedAt
                };
            }

            var data = JsonSerializer.Serialize(snapshot);
            _storage.SaveAsync(LearnedWordsKey, data).GetAwaiter().GetResult();
        }
        catch
        {
            // Learning must never crash typing.
        }
    }

    private bool ShouldLearnWord(string word)
    {
        if (word.Length < 3)
        {
            return false;
        }

        if (_blockedWords.Contains(word))
        {
            return false;
        }

        if (_lexiconWords.Contains(word) || _frequencyRank.ContainsKey(word))
        {
            return false;
        }

        return !HasLongerLexiconCompletion(word);
    }

    private bool HasLongerLexiconCompletion(string word)
    {
        if (!_sortedLexicon.TryGetValue(word[0], out var sorted) || sorted.Length == 0)
        {
            return false;
        }

        var index = LowerBound(sorted, word);
        for (var i = index; i < sorted.Length; i++)
        {
            var candidate = sorted[i];
            if (!candidate.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (candidate.Length > word.Length)
            {
                return true;
            }
        }

        return false;
    }

    private void PruneIncompleteLearnedWords()
    {
        var junk = _learnedWords.Where(word => !ShouldLearnWord(word)).ToList();
        if (junk.Count == 0)
        {
            return;
        }

        foreach (var word in junk)
        {
            _learnedWords.Remove(word);
            _sightings.Remove(word);
        }

        RebuildLearnedByLetter();
        SaveLearnedWords();
    }

    private sealed class LearnedVocabularyState
    {
        public List<string> Words { get; set; } = new();
        public Dictionary<string, int> Sightings { get; set; } = new();
        public List<string> Blocked { get; set; } = new();
        public string? LastLearned { get; set; }
        public DateTimeOffset? LastLearnedAt { get; set; }
    }

    private static void AddToLetterList(Dictionary<char, List<string>> buckets, string word)
    {
        var letter = word[0];
        if (!buckets.TryGetValue(letter, out var list))
        {
            list = new List<string>();
            buckets[letter] = list;
        }

        list.Add(word);
    }

    private static int LowerBound(string[] sorted, string prefix)
    {
        var low = 0;
        var high = sorted.Length;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (WordComparer.Compare(sorted[mid], prefix) < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private static string Normalize(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return string.Empty;
        }

        var trimmed = word.Trim().TrimStart('\uFEFF').ToLowerInvariant();
        foreach (var ch in trimmed)
        {
            if (ch is < 'a' or > 'z')
            {
                return string.Empty;
            }
        }

        return trimmed;
    }

    private static List<string> LoadEmbeddedLines(string resourceName)
    {
        var words = new List<string>();
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return words;
            }

            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                var word = line.Trim().TrimStart('\uFEFF');
                if (word.Length > 0)
                {
                    words.Add(word);
                }
            }
        }
        catch
        {
            return words;
        }

        return words;
    }
}
