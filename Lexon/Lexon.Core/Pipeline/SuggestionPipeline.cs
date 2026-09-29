using Lexon.Core.Interfaces;
using Lexon.Core.Models;
using Lexon.Core.Learning;

namespace Lexon.Core.Pipeline;

/// <summary>
/// Multi-stage suggestion pipeline that orchestrates multiple suggestion providers
/// </summary>
public class SuggestionPipeline : ISuggestionPipeline
{
    private volatile ISuggestionProvider[] _providers = [];
    private readonly object _providersLock = new();
    private readonly IPrivacyGuard _privacyGuard;
    private PersonalizationManager? _personalizationManager;
    private AiAccessPolicy? _accessPolicy;
    private CloudAiActivityLog? _aiLog;
    private bool _isEnabled = true;
    private string _sortMode = "Relevant";
    private int _aiEpoch;
    private CancellationTokenSource _epochCts = new();

    public SuggestionPipeline(IPrivacyGuard privacyGuard)
    {
        _privacyGuard = privacyGuard ?? throw new ArgumentNullException(nameof(privacyGuard));
    }

    public bool IsEnabled => _isEnabled;

    public void SetEnabled(bool isEnabled)
    {
        _isEnabled = isEnabled;
    }

    public void SetSortMode(string sortMode)
    {
        _sortMode = string.Equals(sortMode, "Used", StringComparison.OrdinalIgnoreCase)
            ? "Used"
            : "Relevant";
    }

    /// <summary>
    /// Set the personalization manager for learning user preferences
    /// </summary>
    public void SetPersonalizationManager(PersonalizationManager personalizationManager)
    {
        _personalizationManager = personalizationManager;
    }

    public int AiEpoch => Volatile.Read(ref _aiEpoch);

    public void SetAccessPolicy(AiAccessPolicy? policy) => _accessPolicy = policy;

    public void SetActivityLog(CloudAiActivityLog? log) => _aiLog = log;

    /// <summary>
    /// Drops in-flight slow-path AI results and cancels their tokens.
    /// </summary>
    public void BumpAiEpoch()
    {
        Interlocked.Increment(ref _aiEpoch);
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _epochCts, next);
        try
        {
            previous.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        previous.Dispose();
    }

    public void AddProvider(ISuggestionProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        lock (_providersLock)
        {
            _providers = [.. _providers, provider];
        }
    }

    public void RemoveProvider(string providerName)
    {
        lock (_providersLock)
        {
            _providers = _providers.Where(p => p.Name != providerName).ToArray();
        }
    }

    public async Task<IEnumerable<Suggestion>> GetSuggestionsAsync(TextContext context, CancellationToken cancellationToken = default)
    {
        return await CollectAsync(context, fastPathOnly: true, cancellationToken);
    }

    public async Task<IEnumerable<Suggestion>> GetSupplementalSuggestionsAsync(TextContext context, CancellationToken cancellationToken = default)
    {
        return await CollectAsync(context, fastPathOnly: false, cancellationToken);
    }

    private async Task<IEnumerable<Suggestion>> CollectAsync(TextContext context, bool fastPathOnly, CancellationToken cancellationToken)
    {
        if (!_isEnabled)
        {
            return Enumerable.Empty<Suggestion>();
        }

        if (_privacyGuard.IsSecureField(context))
        {
            return Enumerable.Empty<Suggestion>();
        }

        var snapshot = _providers;
        var fast = snapshot.Where(p => p.IsFastPath).ToList();
        var slow = snapshot.Where(p => !p.IsFastPath).ToList();
        List<ISuggestionProvider> providers;
        if (fastPathOnly)
        {
            providers = fast;
        }
        else
        {
            if (_privacyGuard.IsApplicationBlocked(context.ApplicationName)
                || _privacyGuard.ShouldBlockAssistance(context))
            {
                return Enumerable.Empty<Suggestion>();
            }

            providers = slow.Where(p => !ShouldSkipSlowProvider(p)).ToList();
        }

        if (providers.Count == 0)
        {
            return Enumerable.Empty<Suggestion>();
        }

        var epoch = AiEpoch;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _epochCts.Token);
        if (!fastPathOnly)
        {
            foreach (var provider in providers)
            {
                _aiLog?.TryRecordSuggest(provider.Name, context.ApplicationName);
            }
        }

        IEnumerable<Suggestion>[] results;
        try
        {
            results = await Task.WhenAll(providers.Select(p => p.GetSuggestionsAsync(context, linked.Token)));
        }
        catch (OperationCanceledException)
        {
            return Enumerable.Empty<Suggestion>();
        }

        if (AiEpoch != epoch)
        {
            return Enumerable.Empty<Suggestion>();
        }

        var allSuggestions = results.SelectMany(s => s).ToList();

        if (_personalizationManager != null && _personalizationManager.IsEnabled)
        {
            allSuggestions = allSuggestions.Select(s =>
            {
                var personalizedScore = _personalizationManager.GetPersonalizedScore(s, context);
                var keepScore = IsPinned(s);
                return new Suggestion
                {
                    Text = s.Text,
                    Source = s.Source,
                    Score = keepScore ? Math.Max(s.Score, 0.99) : personalizedScore,
                    Category = s.Category,
                    Metadata = s.Metadata
                };
            }).ToList();

            if (fastPathOnly)
            {
                var preferredSuggestions = _personalizationManager.GetPreferredSuggestions(context, 3);
                foreach (var preferred in preferredSuggestions)
                {
                    if (!allSuggestions.Any(s => s.Text.Equals(preferred, StringComparison.OrdinalIgnoreCase)))
                    {
                        allSuggestions.Add(new Suggestion
                        {
                            Text = preferred,
                            Source = "Personalized",
                            Score = 0.9
                        });
                    }
                }
            }
        }

        return RankAndDeduplicate(allSuggestions);
    }

    public IReadOnlyList<Suggestion> Rerank(IEnumerable<Suggestion> suggestions, TextContext context)
    {
        var list = suggestions?.ToList() ?? [];
        if (list.Count == 0 || _personalizationManager == null || !_personalizationManager.IsEnabled)
        {
            return RankAndDeduplicate(list).ToList();
        }

        var scored = list.Select(s => new Suggestion
        {
            Text = s.Text,
            Source = s.Source,
            Score = IsPinned(s) ? Math.Max(s.Score, 0.99) : _personalizationManager.GetPersonalizedScore(s, context),
            Category = s.Category,
            Metadata = s.Metadata
        });
        return RankAndDeduplicate(scored).ToList();
    }

    /// <summary>
    /// Record suggestion interaction for learning
    /// </summary>
    public void RecordInteraction(Suggestion suggestion, TextContext context, InteractionType interactionType)
    {
        _personalizationManager?.RecordInteraction(suggestion, context, interactionType);
    }

    /// <summary>
    /// Learn from user's writing style
    /// </summary>
    public void LearnWritingStyle(string userText, TextContext context, IReadOnlyList<string>? offeredCompletions = null)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            return;
        }

        var words = UnfinishedWordFilter.Apply(
            CompletedWordExtractor.Extract(userText),
            offeredCompletions);

        if (words.Count == 0)
        {
            return;
        }

        _personalizationManager?.LearnWritingStyle(userText, context);

        var snapshot = _providers;
        foreach (var provider in snapshot)
        {
            if (provider is ILearnableSuggestionProvider learnableProvider)
            {
                learnableProvider.LearnWords(words, offeredCompletions);
            }
        }
    }

    public IReadOnlyList<string> GetLearnedWords()
        => FirstLearnable()?.GetLearnedWords() ?? Array.Empty<string>();

    public void AddExplicitLearnedWord(string word)
        => FirstLearnable()?.AddExplicitWord(word);

    public void RemoveLearnedWord(string word)
        => FirstLearnable()?.RemoveLearnedWord(word);

    public void NeverLearnWord(string word)
        => FirstLearnable()?.NeverLearnWord(word);

    public void ClearLearnedWords()
        => FirstLearnable()?.ClearLearnedWords();

    public bool UndoLastLearn(TimeSpan? maxAge = null)
        => FirstLearnable()?.UndoLastLearn(maxAge) ?? false;

    private bool ShouldSkipSlowProvider(ISuggestionProvider provider)
    {
        var policy = _accessPolicy;
        if (policy == null)
        {
            return false;
        }

        if (policy.LocalOnly)
        {
            return true;
        }

        return !policy.SuggestionsWhileTyping
            && CloudAiNames.RequiresTypingConsent(provider.Name, provider.NetworkEndpoint);
    }

    private ILearnableSuggestionProvider? FirstLearnable()
        => _providers.OfType<ILearnableSuggestionProvider>().FirstOrDefault();

    private IEnumerable<Suggestion> RankAndDeduplicate(IEnumerable<Suggestion> suggestions)
    {
        var ranked = suggestions
            .GroupBy(s => s.Text)
            .Select(g => g.OrderByDescending(s => s.Score).First());

        IEnumerable<Suggestion> ordered = ranked
            .OrderByDescending(IsPinned)
            .ThenByDescending(s => s.Score);

        if (string.Equals(_sortMode, "Used", StringComparison.OrdinalIgnoreCase) && _personalizationManager != null)
        {
            ordered = ranked
                .OrderByDescending(IsPinned)
                .ThenByDescending(s => _personalizationManager.GetAcceptanceCount(s.Text))
                .ThenByDescending(s => s.Score);
        }

        return ordered.Take(40);
    }

    private static bool IsPinned(Suggestion suggestion)
        => string.Equals(suggestion.Source, "Grammar", StringComparison.Ordinal)
           || string.Equals(suggestion.Source, "Spelling", StringComparison.Ordinal);
}
