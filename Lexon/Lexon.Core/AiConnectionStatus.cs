namespace Lexon.Core;

public enum AiConnectionState
{
    NotConfigured,
    KeySaved,
    Checking,
    Connected,
    Failed,
    LocalOnlyOff
}

/// <summary>
/// Drives the Settings AI status line. "Connected" and "Still using X" only
/// refer to a provider that is actually installed right now.
/// </summary>
public sealed class AiConnectionStatus
{
    public AiConnectionState State { get; private set; } = AiConnectionState.NotConfigured;
    public string? ConnectedProvider { get; private set; }
    public string? ConnectedModel { get; private set; }
    public string Detail { get; private set; } = "Paste a key to connect. Lexon will check it automatically.";

    public string ActiveLine
    {
        get
        {
            if (State == AiConnectionState.LocalOnlyOff
                || State == AiConnectionState.NotConfigured
                || State == AiConnectionState.KeySaved
                || string.IsNullOrEmpty(ConnectedProvider)
                || ConnectedProvider.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return "AI is off";
            }

            return string.IsNullOrEmpty(ConnectedModel)
                ? $"Active: {ConnectedProvider}"
                : $"Active: {ConnectedProvider} · {ConnectedModel}";
        }
    }

    public void EnterLocalOnly()
    {
        State = AiConnectionState.LocalOnlyOff;
        ConnectedProvider = null;
        ConnectedModel = null;
        Detail = "Local-only is on. No text is sent to any AI provider.";
    }

    public void EnterNotConfigured()
    {
        State = AiConnectionState.NotConfigured;
        ConnectedProvider = null;
        ConnectedModel = null;
        Detail = "Paste a key to connect. Lexon will check it automatically.";
    }

    public void EnterKeySaved()
    {
        State = AiConnectionState.KeySaved;
        Detail = "Key saved. Checking…";
    }

    public void EnterChecking(string provider)
    {
        State = AiConnectionState.Checking;
        Detail = provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
            ? "Checking for Ollama running locally…"
            : "Checking key…";
    }

    public void EnterConnected(string provider, string? model)
    {
        State = AiConnectionState.Connected;
        ConnectedProvider = provider;
        ConnectedModel = model;
        Detail = provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
            ? "Ollama found."
            : "Connected.";
    }

    /// <summary>
    /// Failure may keep a previously installed provider. Pass the provider that
    /// is actually installed now; do not pass a saved-but-uninstalled name.
    /// </summary>
    public void EnterFailed(string error, string? installedProvider = null)
    {
        State = AiConnectionState.Failed;
        var active = string.IsNullOrWhiteSpace(installedProvider) ? ConnectedProvider : installedProvider;
        if (!string.IsNullOrEmpty(active)
            && !active.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            ConnectedProvider = active;
            Detail = $"{error} Still using {active} (previous key).";
            return;
        }

        ConnectedProvider = null;
        ConnectedModel = null;
        Detail = $"{error} AI is off until a key works.";
    }

    public void EnterDisconnected()
    {
        State = AiConnectionState.NotConfigured;
        ConnectedProvider = null;
        ConnectedModel = null;
        Detail = "AI provider disconnected.";
    }

    /// <summary>
    /// Seeds UI from the provider that is installed at process start, not from
    /// a saved "key once worked" flag.
    /// </summary>
    public void SeedFromInstalled(string? installedProvider, string? model, bool keyPresent)
    {
        if (!string.IsNullOrWhiteSpace(installedProvider)
            && !installedProvider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            EnterConnected(installedProvider, model);
            return;
        }

        ConnectedProvider = null;
        ConnectedModel = null;
        if (keyPresent)
        {
            EnterKeySaved();
            return;
        }

        EnterNotConfigured();
    }
}
