namespace Lexon.Core;

/// <summary>
/// Decision helper so Local-only cannot install an AI provider.
/// </summary>
public static class AiProviderGuard
{
    public static bool ShouldInstall(bool localMode, bool providerRequested)
        => !localMode && providerRequested;
}
