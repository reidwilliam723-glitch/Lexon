using Lexon.Core.Models;

namespace Lexon.SettingsModel;

public sealed class AdaptationItem
{
    public required string Id { get; init; }

    public required string DisplayText { get; init; }

    public override string ToString() => DisplayText;
}

public interface IPersonalizationService
{
    string GetStyleSummary();

    IReadOnlyList<AdaptationItem> GetAdaptations();

    void UndoAdaptation(string id);

    void ResetWritingStyle();

    string ExportLearningData();

    bool ImportLearningData(string json);
}

public interface IWritingStatsReader
{
    /// <summary>Null when stats are not available because Lexon is not running.</summary>
    WritingStatsSnapshot? Read();
}

public interface ILearnedWordCatalog
{
    IReadOnlyList<string> Words { get; }

    void Add(string word);

    void Remove(IReadOnlyList<string> words);

    void NeverLearn(string word);

    void UndoLast();

    void Clear();
}

public interface IWritingDialogs
{
    void ShowWritingStats();

    void ShowLearnedWords();

    void ShowTerminology(
        IReadOnlyList<string> globalTerms,
        IReadOnlyList<string> appOverrideRows,
        Action<IReadOnlyList<string>, IReadOnlyList<string>> onApply);
}

public interface IFileDialogService
{
    string? PickSavePath(string filter, string suggestedName);

    string? PickOpenPath(string filter);
}

public interface IMessageService
{
    void Info(string text, string caption);

    bool Confirm(string text, string caption);
}
