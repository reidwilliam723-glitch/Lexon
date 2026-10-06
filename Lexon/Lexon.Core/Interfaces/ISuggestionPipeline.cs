using Lexon.Core.Learning;

namespace Lexon.Core.Interfaces;

/// <summary>
/// Interface for the multi-stage suggestion pipeline
/// </summary>
public interface ISuggestionPipeline
{
    bool IsEnabled { get; }
    void SetEnabled(bool isEnabled);

    Task<IEnumerable<Models.Suggestion>> GetSuggestionsAsync(Models.TextContext context, CancellationToken cancellationToken = default);
    Task<IEnumerable<Models.Suggestion>> GetSupplementalSuggestionsAsync(Models.TextContext context, CancellationToken cancellationToken = default);
    IReadOnlyList<Models.Suggestion> Rerank(IEnumerable<Models.Suggestion> suggestions, Models.TextContext context);
    void AddProvider(ISuggestionProvider provider);
    void RemoveProvider(string providerName);

    /// <summary>
    /// Report how the user responded to a suggestion (accepted, rejected,
    /// modified, ignored) so personalization can learn from it. Safe to call
    /// even when no personalization manager is configured — becomes a no-op.
    /// </summary>
    void RecordInteraction(Models.Suggestion suggestion, Models.TextContext context, InteractionType interactionType, string? reason = null);

    /// <summary>
    /// Feed free-typed text back into personalization so it can learn the
    /// user's writing style. Safe to call even when no personalization
    /// manager is configured — becomes a no-op.
    /// </summary>
    void LearnWritingStyle(string userText, Models.TextContext context, IReadOnlyList<string>? offeredCompletions = null);

    IReadOnlyList<string> GetLearnedWords();

    void AddExplicitLearnedWord(string word);

    void RemoveLearnedWord(string word);

    void NeverLearnWord(string word);

    void ClearLearnedWords();

    bool UndoLastLearn(TimeSpan? maxAge = null);
}
