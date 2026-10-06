using Lexon.Core.Interfaces;
using Lexon.Core.Models;

namespace Lexon.Core.Grammar;

/// <summary>
/// Offers spelling corrections as you type when the current token is a known typo.
/// </summary>
public sealed class TypoSuggestionProvider : ISuggestionProvider
{
    public string Name => "Spelling";
    public bool IsFastPath => true;

    public Task<IEnumerable<Suggestion>> GetSuggestionsAsync(TextContext context, CancellationToken cancellationToken = default)
    {
        var word = context.CurrentWord ?? string.Empty;
        if (word.Length < 2
            || ScriptLanguageGuard.ShouldSkipSpelling(word, context.FullText)
            || !CommonMisspellings.TryCorrect(word, out var correction))
        {
            return Task.FromResult(Enumerable.Empty<Suggestion>());
        }

        if (correction.Equals(word, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Enumerable.Empty<Suggestion>());
        }

        IEnumerable<Suggestion> result =
        [
            new Suggestion
            {
                Text = PreserveShape(word, correction),
                Source = "Spelling",
                Score = 0.98
            }
        ];
        return Task.FromResult(result);
    }

    private static string PreserveShape(string typed, string correction)
        => TypoAutoCorrect.PreserveShape(typed, correction);
}
