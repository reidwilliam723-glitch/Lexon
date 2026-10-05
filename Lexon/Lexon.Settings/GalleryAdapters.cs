using System.Windows.Forms;
using Lexon.Core;
using Lexon.Core.Interfaces;
using Lexon.Core.Pipeline;
using Lexon.SettingsModel;
using Lexon.SettingsUi;

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
