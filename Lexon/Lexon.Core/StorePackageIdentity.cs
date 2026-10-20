namespace Lexon.Core;

/// <summary>
/// Partner Center identity values for the Store (MSIX) package.
/// </summary>
public static class StorePackageIdentity
{
    public const string PlaceholderPublisher = "CN=WGR";
    public const string PlaceholderName = "WGR.Lexon";

    /// <summary>
    /// Throws when a Store-mode build still uses the stand-in publisher.
    /// </summary>
    public static void EnsureReadyForStore(string? publisher, bool storeMode)
    {
        if (!storeMode)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(publisher)
            || string.Equals(publisher.Trim(), PlaceholderPublisher, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Store mode requires the Partner Center publisher identity (CN=<GUID>), not CN=WGR.");
        }
    }
}
