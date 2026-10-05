using System.Windows.Forms;
using Lexon.Core;
using Lexon.Core.Expansion;
using Lexon.Core.Interfaces;
using Lexon.Core.Learning;
using Lexon.Core.Pipeline;
using Lexon.Core.Theming;
using Lexon.SettingsModel;

namespace Lexon.Settings;

internal sealed class AiPolicyPublisher : IAiPolicyPublisher
{
    private readonly AiAccessPolicy? _policy;
    private readonly SuggestionPipeline? _pipeline;

    public AiPolicyPublisher(AiAccessPolicy? policy, SuggestionPipeline? pipeline)
    {
        _policy = policy;
        _pipeline = pipeline;
    }

    public void Publish(bool localOnly, bool typing, bool rewrite, bool prefetch)
    {
        var changed = _policy?.Update(localOnly, typing, rewrite, prefetch) ?? false;
        if (changed)
        {
            _pipeline?.BumpAiEpoch();
        }
    }
}

internal sealed class ProcessPickerAdapter : IProcessPicker
{
    public Func<IWin32Window?>? Owner { get; set; }

    public string? Pick(string prompt)
    {
        using var form = new ProcessPickerForm(prompt);
        var owner = Owner?.Invoke();
        var result = owner != null ? form.ShowDialog(owner) : form.ShowDialog();
        return result == DialogResult.OK ? form.SelectedProcessName : null;
    }
}

internal sealed class CloudAiActivityViewer : ICloudAiActivityViewer
{
    private readonly CloudAiActivityLog? _log;

    public CloudAiActivityViewer(CloudAiActivityLog? log)
    {
        _log = log;
    }

    public Func<IWin32Window?>? Owner { get; set; }

    public void Show()
    {
        using var form = new CloudAiActivityForm(_log);
        var owner = Owner?.Invoke();
        if (owner != null)
        {
            form.ShowDialog(owner);
            return;
        }

        form.ShowDialog();
    }
}

internal sealed class PersonalizationServiceAdapter : IPersonalizationService
{
    private readonly PersonalizationManager? _personalization;

    public PersonalizationServiceAdapter(PersonalizationManager? personalization)
    {
        _personalization = personalization;
    }

    public string GetStyleSummary()
        => _personalization?.GetStyleSummary() ?? WritingViewModel.UnavailableStyleSummary;

    public IReadOnlyList<AdaptationItem> GetAdaptations()
    {
        if (_personalization == null)
        {
            return [];
        }

        return _personalization.GetAdaptations()
            .Where(static a => !a.Undone)
            .Select(static a => new AdaptationItem { Id = a.Id, DisplayText = a.ToDisplay() })
            .ToList();
    }

    public void UndoAdaptation(string id) => _personalization?.UndoAdaptation(id);

    public void ResetWritingStyle() => _personalization?.ResetWritingStyle();

    public string ExportLearningData()
        => _personalization?.ExportLearningData() ?? string.Empty;

    public bool ImportLearningData(string json)
        => _personalization?.ImportLearningData(json) ?? false;
}

internal sealed class WritingDialogsAdapter : IWritingDialogs
{
    private readonly PersonalizationManager? _personalization;
    private readonly TextExpansionManager? _expansions;
    private readonly SuggestionPipeline? _pipeline;
    private readonly ThemeManager? _themes;

    public WritingDialogsAdapter(
        PersonalizationManager? personalization,
        TextExpansionManager? expansions,
        SuggestionPipeline? pipeline,
        ThemeManager? themes)
    {
        _personalization = personalization;
        _expansions = expansions;
        _pipeline = pipeline;
        _themes = themes;
    }

    public Func<IWin32Window?>? Owner { get; set; }

    public void ShowWritingStats()
    {
        using var form = new WritingStatsForm(_personalization, _expansions, _themes);
        Show(form);
    }

    public void ShowLearnedWords()
    {
        using var form = new LearnedWordsForm(_pipeline, _themes);
        Show(form);
    }

    private void Show(Form form)
    {
        var owner = Owner?.Invoke();
        if (owner != null)
        {
            form.ShowDialog(owner);
            return;
        }

        form.ShowDialog();
    }
}
