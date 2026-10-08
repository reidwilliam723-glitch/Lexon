namespace Lexon.Core.Interfaces;

/// <summary>
/// Implemented by suggestion providers that can grow their vocabulary from
/// words the user actually writes, rather than staying fixed at whatever
/// they started with.
/// </summary>
public interface ILearnableSuggestionProvider
{
    void LearnWords(IEnumerable<string> words, IReadOnlyList<string>? offeredCompletions = null);

    IReadOnlyList<string> GetLearnedWords();

    void AddExplicitWord(string word);

    void RemoveLearnedWord(string word);

    void NeverLearnWord(string word);

    void ClearLearnedWords();

    bool UndoLastLearn(TimeSpan? maxAge = null);

    /// <summary>True when the word is in the user's vocabulary and not in the built-in lexicon.</summary>
    bool IsLearnedOnly(string word);
}
