using Lexon.AI;
using Lexon.AI.Interfaces;
using Lexon.Core;

namespace Lexon.SettingsModel;

public sealed class AiProbeSession
{
    public readonly record struct ProbePaint(string Kind);

    public AiProbeGate Gate { get; } = new();
    public AiConnectionStatus Connection { get; } = new();

    public string ActiveProvider { get; set; } = "None";
    public string ActiveApiKey { get; set; } = string.Empty;
    public bool AiValidated { get; set; }
    public bool AdvancedVisible { get; set; }

    public Func<bool> IsAlive { get; set; } = static () => true;
    public Func<string?> InstalledProviderName { get; set; } = static () => null;
    public Func<string, string, CancellationToken, string?, Task<AiProbeResult>> ProbeAsync { get; set; }
        = static (provider, key, token, model) => AiProviderCatalog.ProbeAsync(provider, key, token, model);
    public Func<string, string, string, IAIProvider> CreateProvider { get; set; }
        = static (provider, key, model) => AiProviderCatalog.Create(provider, key, model: model);
    public Action<IAIProvider?> ApplyProvider { get; set; } = static _ => { };
    public Action Persist { get; set; } = static () => { };
    public Action OnTurnAiOffImmediately { get; set; } = static () => { };

    public bool ProbeStillCurrent(AiProbeGate.Ticket ticket, bool localMode)
        => IsAlive() && Gate.IsCurrent(ticket) && !localMode;

    public void EnterLocalOnly()
    {
        Gate.Invalidate();
        Persist();
        ApplyProvider(null);
        Connection.EnterLocalOnly();
        OnTurnAiOffImmediately();
    }

    public async Task ProbeAsyncCore(bool localMode, string uiProvider, string key, string model, bool watchingClipboard)
    {
        if (!IsAlive())
        {
            return;
        }

        if (localMode)
        {
            Connection.EnterLocalOnly();
            return;
        }

        var ticket = Gate.Begin();
        var token = Gate.Token;

        if (uiProvider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            if (!ProbeStillCurrent(ticket, localMode))
            {
                return;
            }

            ActiveProvider = "None";
            ActiveApiKey = string.Empty;
            AiValidated = true;
            Connection.EnterDisconnected();
            Persist();
            ApplyProvider(null);
            return;
        }

        if (AiProviderCatalog.UsesApiKey(uiProvider) && string.IsNullOrEmpty(key))
        {
            if (!watchingClipboard)
            {
                Connection.EnterNotConfigured();
            }

            return;
        }

        Connection.EnterChecking(uiProvider);

        AiProbeResult result;
        try
        {
            result = await ProbeAsync(uiProvider, key, token, model);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ProbeStillCurrent(ticket, localMode) || token.IsCancellationRequested)
        {
            return;
        }

        if (!result.Succeeded)
        {
            var installed = InstalledProviderName();
            Connection.EnterFailed(result.Message, installed);
            if (string.IsNullOrEmpty(installed))
            {
                ApplyProvider(null);
            }

            return;
        }

        if (!ProbeStillCurrent(ticket, localMode))
        {
            return;
        }

        ActiveProvider = uiProvider;
        ActiveApiKey = AiProviderCatalog.UsesApiKey(uiProvider) ? key : string.Empty;
        AiValidated = true;
        var resolvedModel = string.IsNullOrWhiteSpace(model)
            ? AiProviderCatalog.DefaultModel(uiProvider)
            : model;
        Connection.EnterConnected(uiProvider, resolvedModel);
        Persist();
        if (!ProbeStillCurrent(ticket, localMode))
        {
            return;
        }

        try
        {
            ApplyProvider(CreateProvider(uiProvider, key, resolvedModel));
        }
        catch (Exception)
        {
            Connection.EnterFailed("Lexon could not switch providers.", InstalledProviderName());
        }
    }
}
