using Lexon.Core.Models;
using Lexon.Core.Interfaces;
using System.Text.Json;

namespace Lexon.Core.Learning;

/// <summary>
/// Manages personalized suggestion learning and user preference adaptation
/// </summary>
public class PersonalizationManager
{
    private readonly IStorage _storage;
    private readonly Dictionary<string, UserPreference> _userPreferences;
    private readonly Dictionary<string, ContextPattern> _contextPatterns;
    private readonly Dictionary<string, double> _suggestionScores;
    private readonly Dictionary<string, int> _acceptanceCounts;
    private readonly FeedbackCollector _feedbackCollector;
    private readonly WordTransitionModel _wordTransitions;
    private readonly WritingStyleProfile _styleProfile = new();
    private readonly Dictionary<string, List<bool>> _styleOutcomes = new(StringComparer.OrdinalIgnoreCase);
    private int _acceptedTotal;
    private int _rejectedTotal;
    private int _ignoredTotal;
    private int _charactersInserted;
    private string? _adaptedToneNote;
    private readonly List<AdaptationRecord> _adaptations = [];
    private readonly object _lock = new();
    private const string PreferencesKey = "user_preferences";
    private const string PatternsKey = "context_patterns";
    private const string ScoresKey = "suggestion_scores";
    private const string AcceptanceCountsKey = "suggestion_acceptance_counts";
    private const string WordTransitionsKey = "word_transitions";
    private const string StyleProfileKey = "writing_style_profile";
    private const string StyleOutcomesKey = "style_outcomes";
    private const string StatsKey = "writing_stats";
    
    public bool IsEnabled { get; set; } = true;
    public double LearningRate { get; set; } = 0.1; // How quickly to adapt
    public int MinSamplesForLearning { get; set; } = 5; // Minimum samples before applying learning

    public PersonalizationManager(IStorage storage, FeedbackCollector feedbackCollector)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _feedbackCollector = feedbackCollector ?? throw new ArgumentNullException(nameof(feedbackCollector));
        _userPreferences = new Dictionary<string, UserPreference>();
        _contextPatterns = new Dictionary<string, ContextPattern>();
        _suggestionScores = new Dictionary<string, double>();
        _acceptanceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _wordTransitions = new WordTransitionModel();
    }

    /// <summary>
    /// Loads persisted personalization data. Must be awaited by the caller
    /// before relying on learned preferences/scores — the constructor can't
    /// be async, so this has to run separately rather than fire-and-forget.
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadPersonalizationDataAsync();
    }

    /// <summary>
    /// Record a suggestion interaction for learning
    /// </summary>
    public void RecordInteraction(
        Suggestion suggestion,
        TextContext context,
        InteractionType interactionType,
        string? reason = null)
    {
        if (!IsEnabled) return;

        lock (_lock)
        {
            // Update suggestion scores based on interaction
            UpdateSuggestionScore(suggestion, interactionType);
            if (interactionType == InteractionType.Accepted)
            {
                var key = suggestion.Text.ToLowerInvariant();
                _acceptanceCounts.TryGetValue(key, out var count);
                _acceptanceCounts[key] = count + 1;
                _acceptedTotal++;
                _charactersInserted += suggestion.Text.Length;
            }
            else if (interactionType == InteractionType.Rejected)
            {
                _rejectedTotal++;
            }
            else if (interactionType == InteractionType.Ignored)
            {
                _ignoredTotal++;
            }

            // Learn context patterns
            LearnContextPattern(context, suggestion, interactionType);

            // Learn which words follow the previous word
            if (interactionType == InteractionType.Accepted)
            {
                _wordTransitions.Observe(GetPreviousWord(context), suggestion.Text, 3);
            }

            // Update user preferences
            UpdateUserPreferences(context, suggestion, interactionType);
        }

        // Also record in feedback collector
        var feedbackType = interactionType switch
        {
            InteractionType.Accepted => FeedbackType.Accepted,
            InteractionType.Rejected => FeedbackType.Rejected,
            InteractionType.Modified => FeedbackType.Modified,
            _ => FeedbackType.Ignored
        };
        
        _feedbackCollector.RecordFeedback(suggestion.Text, context.CurrentWord, feedbackType, reason);
        
        // Persist changes periodically
        if (ShouldPersist())
        {
            SavePersonalizationData();
        }
    }

    /// <summary>
    /// Get personalized score for a suggestion based on learned preferences
    /// </summary>
    public double GetPersonalizedScore(Suggestion suggestion, TextContext context)
    {
        if (!IsEnabled) return suggestion.Score;

        lock (_lock)
        {
            double personalizedScore = suggestion.Score;

            // Apply learned suggestion scores
            if (_suggestionScores.TryGetValue(suggestion.Text.ToLowerInvariant(), out var learnedScore))
            {
                personalizedScore = learnedScore;
            }

            // Apply context pattern adjustments
            var contextKey = GetContextKey(context);
            if (_contextPatterns.TryGetValue(contextKey, out var pattern))
            {
                var patternAdjustment = pattern.GetSuggestionAdjustment(suggestion.Text);
                personalizedScore += patternAdjustment * LearningRate;
            }

            // Apply user preference adjustments
            var preferenceKey = GetPreferenceKey(context);
            if (_userPreferences.TryGetValue(preferenceKey, out var preference))
            {
                var preferenceAdjustment = preference.GetSuggestionAdjustment(suggestion.Text);
                personalizedScore += preferenceAdjustment * LearningRate;
            }

            var followScore = _wordTransitions.GetFollowScore(GetPreviousWord(context), suggestion.Text);
            personalizedScore += followScore * 0.45;

            // Ensure score stays within valid range
            return Math.Max(0, Math.Min(1, personalizedScore));
        }
    }

    /// <summary>
    /// How many times this suggestion text has been accepted. Distinct from the
    /// blended 0–1 <see cref="GetPersonalizedScore"/> used for relevance ranking.
    /// </summary>
    public int GetAcceptanceCount(string suggestionText)
    {
        if (string.IsNullOrEmpty(suggestionText))
        {
            return 0;
        }

        lock (_lock)
        {
            return _acceptanceCounts.TryGetValue(suggestionText.ToLowerInvariant(), out var count) ? count : 0;
        }
    }

    public IReadOnlyList<string> GetTopFollowers(string? previousWord, int count = 3)
        => _wordTransitions.GetTopFollowers(previousWord ?? string.Empty, count);

    /// <summary>
    /// Get user's preferred suggestions for a given context
    /// </summary>
    public IEnumerable<string> GetPreferredSuggestions(TextContext context, int maxCount = 5)
    {
        if (!IsEnabled) return Enumerable.Empty<string>();

        lock (_lock)
        {
            var contextKey = GetContextKey(context);
            if (_contextPatterns.TryGetValue(contextKey, out var pattern))
            {
                return pattern.GetTopSuggestions(maxCount);
            }

            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Learn user's writing style and vocabulary preferences
    /// </summary>
    public void LearnWritingStyle(string userText, TextContext context)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(userText)) return;

        var words = CompletedWordExtractor.Extract(userText);

        lock (_lock)
        {
            foreach (var word in words)
            {
                var preferenceKey = $"vocabulary_{word.ToLowerInvariant()}";
                if (!_userPreferences.ContainsKey(preferenceKey))
                {
                    _userPreferences[preferenceKey] = new UserPreference
                    {
                        Type = PreferenceType.Vocabulary,
                        Value = word.ToLowerInvariant(),
                        Frequency = 1
                    };
                }
                else
                {
                    _userPreferences[preferenceKey].Frequency++;
                }
            }
        }

        _wordTransitions.LearnFromCompletedText(userText);
        var sample = WritingStyleAnalyzer.Analyze(userText);
        lock (_lock)
        {
            WritingStyleAnalyzer.Merge(_styleProfile, sample);
        }
    }

    public WritingStyleProfile GetStyleProfile()
    {
        lock (_lock)
        {
            return new WritingStyleProfile
            {
                AverageSentenceLength = _styleProfile.AverageSentenceLength,
                ContractionRate = _styleProfile.ContractionRate,
                FirstPersonRate = _styleProfile.FirstPersonRate,
                ExclamationRate = _styleProfile.ExclamationRate,
                SampleCount = _styleProfile.SampleCount
            };
        }
    }

    public string GetStyleHint(AppWritingCategory category)
    {
        if (ShouldAvoidStyle(category))
        {
            return string.Empty;
        }

        return GetStyleProfile().ToPromptHint();
    }

    public void RecordRewriteOutcome(AppWritingCategory category, string styleKey, bool accepted)
    {
        var key = $"{category}:{styleKey}";
        lock (_lock)
        {
            if (!_styleOutcomes.TryGetValue(key, out var list))
            {
                list = [];
                _styleOutcomes[key] = list;
            }

            list.Add(accepted);
            if (list.Count > 10)
            {
                list.RemoveAt(0);
            }

            if (ShouldAvoidStyleLocked(category, styleKey))
            {
                _adaptedToneNote = $"Adapted tone for {category} based on your feedback";
                if (!_adaptations.Any(a => !a.Undone && a.OutcomeKey == key))
                {
                    _adaptations.Insert(0, new AdaptationRecord
                    {
                        Summary = $"Stopped matching writing style for {category}",
                        OutcomeKey = key,
                        OccurredUtc = DateTime.UtcNow
                    });
                    if (_adaptations.Count > 20)
                    {
                        _adaptations.RemoveAt(_adaptations.Count - 1);
                    }
                }
            }
        }

        SavePersonalizationData();
    }

    public bool ShouldAvoidStyle(AppWritingCategory category, string styleKey = "default")
    {
        lock (_lock)
        {
            return ShouldAvoidStyleLocked(category, styleKey);
        }
    }

    private bool ShouldAvoidStyleLocked(AppWritingCategory category, string styleKey)
    {
        var key = $"{category}:{styleKey}";
        if (!_styleOutcomes.TryGetValue(key, out var list) || list.Count < 10)
        {
            return false;
        }

        return list.Count(accepted => !accepted) >= 7;
    }

    public string GetStyleSummary()
    {
        lock (_lock)
        {
            return _styleProfile.ToSummary();
        }
    }

    public void ResetWritingStyle()
    {
        lock (_lock)
        {
            _styleProfile.AverageSentenceLength = 0;
            _styleProfile.ContractionRate = 0;
            _styleProfile.FirstPersonRate = 0;
            _styleProfile.ExclamationRate = 0;
            _styleProfile.SampleCount = 0;
        }

        SavePersonalizationData();
    }

    public IReadOnlyList<AdaptationRecord> GetAdaptations()
    {
        lock (_lock)
        {
            return _adaptations.Select(a => new AdaptationRecord
            {
                Id = a.Id,
                Summary = a.Summary,
                OutcomeKey = a.OutcomeKey,
                OccurredUtc = a.OccurredUtc,
                Undone = a.Undone
            }).ToList();
        }
    }

    public bool UndoAdaptation(string id)
    {
        lock (_lock)
        {
            var record = _adaptations.FirstOrDefault(a => a.Id == id);
            if (record == null || record.Undone)
            {
                return false;
            }

            record.Undone = true;
            _styleOutcomes.Remove(record.OutcomeKey);
            if (_adaptations.All(a => a.Undone))
            {
                _adaptedToneNote = null;
            }
        }

        SavePersonalizationData();
        return true;
    }

    public int GetAcceptedTotal()
    {
        lock (_lock) return _acceptedTotal;
    }

    public int GetRejectedTotal()
    {
        lock (_lock) return _rejectedTotal;
    }

    public int GetIgnoredTotal()
    {
        lock (_lock) return _ignoredTotal;
    }

    public int GetCharactersInserted()
    {
        lock (_lock) return _charactersInserted;
    }

    public string? GetAdaptedToneNote()
    {
        lock (_lock) return _adaptedToneNote;
    }

    public void RecordCharactersInserted(int count)
    {
        if (count <= 0)
        {
            return;
        }

        lock (_lock)
        {
            _charactersInserted += count;
        }
    }

    /// <summary>
    /// Get user's vocabulary preferences
    /// </summary>
    public Dictionary<string, int> GetVocabularyPreferences()
    {
        lock (_lock)
        {
            return _userPreferences
                .Where(kvp => kvp.Value.Type == PreferenceType.Vocabulary)
                .ToDictionary(kvp => kvp.Value.Value, kvp => kvp.Value.Frequency);
        }
    }

    /// <summary>
    /// Reset learning data for a specific context or all data
    /// </summary>
    public void ResetLearning(string? contextKey = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(contextKey))
            {
                _userPreferences.Clear();
                _contextPatterns.Clear();
                _suggestionScores.Clear();
                _acceptanceCounts.Clear();
                _wordTransitions.Reset();
            }
            else
            {
                _contextPatterns.Remove(contextKey);
                _userPreferences.Remove(contextKey);
            }
        }

        SavePersonalizationData();
    }

    /// <summary>
    /// Export learning data for backup or analysis
    /// </summary>
    public string ExportLearningData()
    {
        PersonalizationData data;
        lock (_lock)
        {
            data = SnapshotLocked();
            data.ExportDate = DateTime.UtcNow;
        }

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Import learning data from backup. Returns false if the file is invalid.
    /// </summary>
    public bool ImportLearningData(string jsonData)
    {
        try
        {
            var data = JsonSerializer.Deserialize<PersonalizationData>(jsonData);
            if (data == null)
            {
                return false;
            }

            lock (_lock)
            {
                _userPreferences.Clear();
                _contextPatterns.Clear();
                _suggestionScores.Clear();
                _acceptanceCounts.Clear();
                _styleOutcomes.Clear();

                foreach (var kvp in data.UserPreferences)
                    _userPreferences[kvp.Key] = kvp.Value;

                foreach (var kvp in data.ContextPatterns)
                    _contextPatterns[kvp.Key] = kvp.Value;

                foreach (var kvp in data.SuggestionScores)
                    _suggestionScores[kvp.Key] = kvp.Value;

                if (data.AcceptanceCounts != null)
                {
                    foreach (var kvp in data.AcceptanceCounts)
                        _acceptanceCounts[kvp.Key] = kvp.Value;
                }

                if (data.WordTransitions != null)
                {
                    _wordTransitions.Reset();
                    _wordTransitions.ImportCounts(data.WordTransitions);
                }

                if (data.StyleProfile != null)
                {
                    _styleProfile.AverageSentenceLength = data.StyleProfile.AverageSentenceLength;
                    _styleProfile.ContractionRate = data.StyleProfile.ContractionRate;
                    _styleProfile.FirstPersonRate = data.StyleProfile.FirstPersonRate;
                    _styleProfile.ExclamationRate = data.StyleProfile.ExclamationRate;
                    _styleProfile.SampleCount = data.StyleProfile.SampleCount;
                }

                if (data.StyleOutcomes != null)
                {
                    foreach (var kvp in data.StyleOutcomes)
                        _styleOutcomes[kvp.Key] = kvp.Value;
                }

                if (data.Stats != null)
                {
                    _acceptedTotal = data.Stats.Accepted;
                    _rejectedTotal = data.Stats.Rejected;
                    _ignoredTotal = data.Stats.Ignored;
                    _charactersInserted = data.Stats.CharactersInserted;
                    _adaptedToneNote = data.Stats.AdaptedToneNote;
                    _adaptations.Clear();
                    if (data.Stats.Adaptations is { Count: > 0 })
                    {
                        _adaptations.AddRange(data.Stats.Adaptations);
                    }
                }
            }

            SavePersonalizationData();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private PersonalizationData SnapshotLocked()
    {
        return new PersonalizationData
        {
            UserPreferences = new Dictionary<string, UserPreference>(_userPreferences),
            ContextPatterns = new Dictionary<string, ContextPattern>(_contextPatterns),
            SuggestionScores = new Dictionary<string, double>(_suggestionScores),
            AcceptanceCounts = new Dictionary<string, int>(_acceptanceCounts),
            WordTransitions = _wordTransitions.ExportCounts(),
            StyleProfile = new WritingStyleProfile
            {
                AverageSentenceLength = _styleProfile.AverageSentenceLength,
                ContractionRate = _styleProfile.ContractionRate,
                FirstPersonRate = _styleProfile.FirstPersonRate,
                ExclamationRate = _styleProfile.ExclamationRate,
                SampleCount = _styleProfile.SampleCount
            },
            StyleOutcomes = new Dictionary<string, List<bool>>(_styleOutcomes),
            Stats = new WritingStatsPersist
            {
                Accepted = _acceptedTotal,
                Rejected = _rejectedTotal,
                Ignored = _ignoredTotal,
                CharactersInserted = _charactersInserted,
                AdaptedToneNote = _adaptedToneNote,
                Adaptations = _adaptations.ToList()
            }
        };
    }

    private void UpdateSuggestionScore(Suggestion suggestion, InteractionType interactionType)
    {
        var key = suggestion.Text.ToLowerInvariant();
        
        if (!_suggestionScores.ContainsKey(key))
        {
            _suggestionScores[key] = suggestion.Score;
        }
        
        var currentScore = _suggestionScores[key];
        var adjustment = interactionType switch
        {
            InteractionType.Accepted => 0.1,
            InteractionType.Rejected => -0.2,
            InteractionType.Modified => -0.05,
            _ => 0
        };
        
        _suggestionScores[key] = Math.Max(0, Math.Min(1, currentScore + adjustment * LearningRate));
    }

    private void LearnContextPattern(TextContext context, Suggestion suggestion, InteractionType interactionType)
    {
        var contextKey = GetContextKey(context);
        
        if (!_contextPatterns.ContainsKey(contextKey))
        {
            _contextPatterns[contextKey] = new ContextPattern
            {
                ContextKey = contextKey,
                SampleCount = 0
            };
        }
        
        var pattern = _contextPatterns[contextKey];
        pattern.RecordInteraction(suggestion.Text, interactionType);
        pattern.SampleCount++;
    }

    private void UpdateUserPreferences(TextContext context, Suggestion suggestion, InteractionType interactionType)
    {
        var preferenceKey = GetPreferenceKey(context);
        
        if (!_userPreferences.ContainsKey(preferenceKey))
        {
            _userPreferences[preferenceKey] = new UserPreference
            {
                Type = PreferenceType.Suggestion,
                Value = suggestion.Text,
                Frequency = 0,
                LastUsed = DateTime.UtcNow
            };
        }
        
        var preference = _userPreferences[preferenceKey];
        
        if (interactionType == InteractionType.Accepted)
        {
            preference.Frequency++;
            preference.LastUsed = DateTime.UtcNow;
        }
        else if (interactionType == InteractionType.Rejected)
        {
            preference.Frequency = Math.Max(0, preference.Frequency - 1);
        }
    }

    private string GetContextKey(TextContext context)
    {
        var lastWord = string.IsNullOrEmpty(context.CurrentWord) ? "empty" : context.CurrentWord.ToLowerInvariant();
        var appHash = string.IsNullOrEmpty(context.ApplicationName) ? "unknown" : context.ApplicationName.GetHashCode().ToString("X");
        return $"{appHash}_{lastWord}";
    }

    private static string? GetPreviousWord(TextContext context)
    {
        if (string.IsNullOrWhiteSpace(context.PreviousWords))
        {
            return null;
        }

        var parts = context.PreviousWords.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : parts[^1];
    }

    private string GetPreferenceKey(TextContext context)
    {
        return $"pref_{GetContextKey(context)}";
    }

    private bool ShouldPersist()
    {
        // Persist every 50 interactions or based on time
        lock (_lock)
        {
            return _suggestionScores.Count % 50 == 0;
        }
    }

    private async Task LoadPersonalizationDataAsync()
    {
        try
        {
            var preferencesData = await _storage.LoadAsync<string>(PreferencesKey);
            var loadedPreferences = string.IsNullOrEmpty(preferencesData)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, UserPreference>>(preferencesData);

            var patternsData = await _storage.LoadAsync<string>(PatternsKey);
            var loadedPatterns = string.IsNullOrEmpty(patternsData)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, ContextPattern>>(patternsData);

            var scoresData = await _storage.LoadAsync<string>(ScoresKey);
            var loadedScores = string.IsNullOrEmpty(scoresData)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, double>>(scoresData);

            var countsData = await _storage.LoadAsync<string>(AcceptanceCountsKey);
            var loadedCounts = string.IsNullOrEmpty(countsData)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, int>>(countsData);

            var transitionsData = await _storage.LoadAsync<string>(WordTransitionsKey);
            var loadedTransitions = string.IsNullOrEmpty(transitionsData)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(transitionsData);

            var styleData = await _storage.LoadAsync<string>(StyleProfileKey);
            var loadedStyle = string.IsNullOrEmpty(styleData)
                ? null
                : JsonSerializer.Deserialize<WritingStyleProfile>(styleData);

            var outcomesData = await _storage.LoadAsync<string>(StyleOutcomesKey);
            var loadedOutcomes = string.IsNullOrEmpty(outcomesData)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, List<bool>>>(outcomesData);

            var statsData = await _storage.LoadAsync<string>(StatsKey);
            var loadedStats = string.IsNullOrEmpty(statsData)
                ? null
                : JsonSerializer.Deserialize<WritingStatsPersist>(statsData);

            lock (_lock)
            {
                if (loadedPreferences != null)
                {
                    foreach (var kvp in loadedPreferences)
                        _userPreferences[kvp.Key] = kvp.Value;
                }

                if (loadedPatterns != null)
                {
                    foreach (var kvp in loadedPatterns)
                        _contextPatterns[kvp.Key] = kvp.Value;
                }

                if (loadedScores != null)
                {
                    foreach (var kvp in loadedScores)
                        _suggestionScores[kvp.Key] = kvp.Value;
                }

                if (loadedCounts != null)
                {
                    foreach (var kvp in loadedCounts)
                        _acceptanceCounts[kvp.Key] = kvp.Value;
                }

                if (loadedTransitions != null)
                {
                    _wordTransitions.ImportCounts(loadedTransitions);
                }

                if (loadedStyle != null)
                {
                    _styleProfile.AverageSentenceLength = loadedStyle.AverageSentenceLength;
                    _styleProfile.ContractionRate = loadedStyle.ContractionRate;
                    _styleProfile.FirstPersonRate = loadedStyle.FirstPersonRate;
                    _styleProfile.ExclamationRate = loadedStyle.ExclamationRate;
                    _styleProfile.SampleCount = loadedStyle.SampleCount;
                }

                if (loadedOutcomes != null)
                {
                    foreach (var kvp in loadedOutcomes)
                    {
                        _styleOutcomes[kvp.Key] = kvp.Value;
                    }
                }

                if (loadedStats != null)
                {
                    _acceptedTotal = loadedStats.Accepted;
                    _rejectedTotal = loadedStats.Rejected;
                    _ignoredTotal = loadedStats.Ignored;
                    _charactersInserted = loadedStats.CharactersInserted;
                    _adaptedToneNote = loadedStats.AdaptedToneNote;
                    if (loadedStats.Adaptations is { Count: > 0 })
                    {
                        _adaptations.Clear();
                        _adaptations.AddRange(loadedStats.Adaptations);
                    }
                }
            }
        }
        catch
        {
            // Start with empty data if loading fails
        }
    }

    private void SavePersonalizationData()
    {
        // Fire-and-forget is intentional (a save shouldn't block the caller
        // that just recorded an interaction), but failures are now caught
        // rather than left unobserved.
        _ = SavePersonalizationDataAsync();
    }

    private async Task SavePersonalizationDataAsync()
    {
        try
        {
            Dictionary<string, UserPreference> preferencesSnapshot;
            Dictionary<string, ContextPattern> patternsSnapshot;
            Dictionary<string, double> scoresSnapshot;
            Dictionary<string, int> countsSnapshot;
            Dictionary<string, Dictionary<string, int>> transitionsSnapshot;
            WritingStyleProfile styleSnapshot;
            Dictionary<string, List<bool>> outcomesSnapshot;
            WritingStatsPersist statsSnapshot;
            lock (_lock)
            {
                preferencesSnapshot = new Dictionary<string, UserPreference>(_userPreferences);
                patternsSnapshot = new Dictionary<string, ContextPattern>(_contextPatterns);
                scoresSnapshot = new Dictionary<string, double>(_suggestionScores);
                countsSnapshot = new Dictionary<string, int>(_acceptanceCounts);
                transitionsSnapshot = _wordTransitions.ExportCounts();
                styleSnapshot = new WritingStyleProfile
                {
                    AverageSentenceLength = _styleProfile.AverageSentenceLength,
                    ContractionRate = _styleProfile.ContractionRate,
                    FirstPersonRate = _styleProfile.FirstPersonRate,
                    ExclamationRate = _styleProfile.ExclamationRate,
                    SampleCount = _styleProfile.SampleCount
                };
                outcomesSnapshot = _styleOutcomes.ToDictionary(k => k.Key, v => v.Value.ToList());
                statsSnapshot = new WritingStatsPersist
                {
                    Accepted = _acceptedTotal,
                    Rejected = _rejectedTotal,
                    Ignored = _ignoredTotal,
                    CharactersInserted = _charactersInserted,
                    AdaptedToneNote = _adaptedToneNote,
                    Adaptations = _adaptations.ToList()
                };
            }

            var preferencesData = JsonSerializer.Serialize(preferencesSnapshot);
            await _storage.SaveAsync(PreferencesKey, preferencesData);

            var patternsData = JsonSerializer.Serialize(patternsSnapshot);
            await _storage.SaveAsync(PatternsKey, patternsData);

            var scoresData = JsonSerializer.Serialize(scoresSnapshot);
            await _storage.SaveAsync(ScoresKey, scoresData);

            var countsData = JsonSerializer.Serialize(countsSnapshot);
            await _storage.SaveAsync(AcceptanceCountsKey, countsData);

            var transitionsData = JsonSerializer.Serialize(transitionsSnapshot);
            await _storage.SaveAsync(WordTransitionsKey, transitionsData);

            await _storage.SaveAsync(StyleProfileKey, JsonSerializer.Serialize(styleSnapshot));
            await _storage.SaveAsync(StyleOutcomesKey, JsonSerializer.Serialize(outcomesSnapshot));
            await _storage.SaveAsync(StatsKey, JsonSerializer.Serialize(statsSnapshot));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save personalization data: {ex.Message}");
            // Silently fail if save fails
        }
    }
}

public class UserPreference
{
    public PreferenceType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    
    public double GetSuggestionAdjustment(string suggestion)
    {
        if (Type == PreferenceType.Suggestion && Value.Equals(suggestion, StringComparison.OrdinalIgnoreCase))
        {
            return Math.Min(0.3, Frequency * 0.05); // Cap adjustment at 0.3
        }
        return 0;
    }
}

public class ContextPattern
{
    public string ContextKey { get; set; } = string.Empty;
    public int SampleCount { get; set; }
    private readonly Dictionary<string, PatternStats> _suggestionStats = new();
    
    public void RecordInteraction(string suggestion, InteractionType interactionType)
    {
        var key = suggestion.ToLowerInvariant();
        
        if (!_suggestionStats.ContainsKey(key))
        {
            _suggestionStats[key] = new PatternStats();
        }
        
        var stats = _suggestionStats[key];
        stats.TotalCount++;
        
        if (interactionType == InteractionType.Accepted)
            stats.AcceptedCount++;
        else if (interactionType == InteractionType.Rejected)
            stats.RejectedCount++;
    }
    
    public double GetSuggestionAdjustment(string suggestion)
    {
        var key = suggestion.ToLowerInvariant();
        if (!_suggestionStats.ContainsKey(key)) return 0;
        
        var stats = _suggestionStats[key];
        if (stats.TotalCount < 5) return 0; // Need minimum samples
        
        var acceptanceRate = (double)stats.AcceptedCount / stats.TotalCount;
        return (acceptanceRate - 0.5) * 0.2; // Scale adjustment
    }
    
    public IEnumerable<string> GetTopSuggestions(int maxCount)
    {
        return _suggestionStats
            .Where(kvp => kvp.Value.TotalCount >= 5)
            .OrderByDescending(kvp => (double)kvp.Value.AcceptedCount / kvp.Value.TotalCount)
            .Take(maxCount)
            .Select(kvp => kvp.Key);
    }
}

public class PatternStats
{
    public int TotalCount { get; set; }
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
}

public enum PreferenceType
{
    Suggestion,
    Vocabulary,
    WritingStyle,
    Context
}

public enum InteractionType
{
    Accepted,
    Rejected,
    Modified,
    Ignored
}

public class PersonalizationData
{
    public Dictionary<string, UserPreference> UserPreferences { get; set; } = new();
    public Dictionary<string, ContextPattern> ContextPatterns { get; set; } = new();
    public Dictionary<string, double> SuggestionScores { get; set; } = new();
    public Dictionary<string, int> AcceptanceCounts { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> WordTransitions { get; set; } = new();
    public WritingStyleProfile? StyleProfile { get; set; }
    public Dictionary<string, List<bool>>? StyleOutcomes { get; set; }
    public WritingStatsPersist? Stats { get; set; }
    public DateTime ExportDate { get; set; }
}

public class WritingStatsPersist
{
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public int Ignored { get; set; }
    public int CharactersInserted { get; set; }
    public string? AdaptedToneNote { get; set; }
    public List<AdaptationRecord> Adaptations { get; set; } = [];
}