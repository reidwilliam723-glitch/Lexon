using Lexon.Core.Models;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class LearnedWordsPageModelTests
{
    [Fact]
    public void Search_FindsWordsBeyondTheOldFiveHundredCap()
    {
        var words = Enumerable.Range(0, 600).Select(i => $"word{i:000}").ToList();
        words.Add("zzqneedle");
        var model = new LearnedWordsPageModel(new Catalog(words), messages: null, static () => { });

        model.SearchText = "zzqneedle";

        Assert.Equal(["zzqneedle"], model.VisibleWords);
    }

    [Fact]
    public void Delete_RemovesEverySelectedWord()
    {
        var catalog = new Catalog(["one", "two", "three"]);
        var model = new LearnedWordsPageModel(catalog, messages: null, static () => { });
        model.Refresh();

        model.Delete(["one", "three"]);

        Assert.Equal(["two"], model.VisibleWords);
        Assert.Equal(["two"], catalog.Words);
    }

    [Fact]
    public void NeverLearn_RemovesOnlyTheOneWord()
    {
        var catalog = new Catalog(["one", "two"]);
        var model = new LearnedWordsPageModel(catalog, messages: null, static () => { });

        model.NeverLearn("two");

        Assert.Equal(["one"], catalog.Words);
        Assert.Equal(["two"], catalog.Blocked);
    }

    [Fact]
    public void StatsRefresh_ShowsTheSnapshot()
    {
        var reader = new Reader(new WritingStatsSnapshot
        {
            SuggestionsAccepted = 12,
            ExpansionUses = 3,
            CharactersInsertedByLexon = 40,
            TopExpansions = [("sig", 2)]
        });
        var model = new WritingStatsPageModel(reader, static () => { });

        model.Refresh();

        Assert.True(model.IsAvailable);
        Assert.Equal("12", model.Accepted);
        Assert.Contains("sig", model.TopExpansions.Single());
    }

    private sealed class Catalog(List<string> words) : ILearnedWordCatalog
    {
        public List<string> Blocked { get; } = [];

        public IReadOnlyList<string> Words => words;

        public void Add(string word) => words.Add(word);

        public void Remove(IReadOnlyList<string> selected)
        {
            foreach (var word in selected)
            {
                words.Remove(word);
            }
        }

        public void NeverLearn(string word)
        {
            words.Remove(word);
            Blocked.Add(word);
        }

        public void UndoLast()
        {
        }

        public void Clear() => words.Clear();
    }

    private sealed class Reader(WritingStatsSnapshot snapshot) : IWritingStatsReader
    {
        public WritingStatsSnapshot? Read() => snapshot;
    }
}
