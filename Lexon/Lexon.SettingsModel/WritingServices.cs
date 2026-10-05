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

public interface IWritingDialogs
{
    void ShowWritingStats();

    void ShowLearnedWords();
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
