namespace Lexon.Core.Grammar;

/// <summary>
/// User-chosen English spelling. South African English uses the UK forms.
/// There is no automatic language detection.
/// </summary>
public static class SpellingConventions
{
    public const string US = "US";
    public const string UK = "UK";
    public const string SouthAfrican = "South African";

    public static readonly string[] Labels = [US, UK, SouthAfrican];

    private static readonly (string Us, string Uk)[] Pairs =
    [
        ("color", "colour"),
        ("favorite", "favourite"),
        ("organize", "organise"),
        ("organized", "organised"),
        ("organization", "organisation"),
        ("recognize", "recognise"),
        ("center", "centre"),
        ("meter", "metre"),
        ("liter", "litre"),
        ("defense", "defence"),
        ("traveling", "travelling"),
        ("canceled", "cancelled")
    ];

    public static string NormalizeChoice(string? choice)
    {
        if (string.Equals(choice, UK, StringComparison.OrdinalIgnoreCase))
        {
            return UK;
        }

        if (string.Equals(choice, SouthAfrican, StringComparison.OrdinalIgnoreCase)
            || string.Equals(choice, "SouthAfrica", StringComparison.OrdinalIgnoreCase))
        {
            return SouthAfrican;
        }

        return US;
    }

    public static bool UsesUkSpelling(string? choice)
        => NormalizeChoice(choice) is UK or SouthAfrican;

    /// <summary>
    /// When <paramref name="typed"/> is one side of a known pair, returns the
    /// form for the chosen convention.
    /// </summary>
    public static bool TryPreferred(string? typed, string? choice, out string preferred)
    {
        preferred = string.Empty;
        if (string.IsNullOrWhiteSpace(typed))
        {
            return false;
        }

        var key = typed.Trim();
        foreach (var (us, uk) in Pairs)
        {
            if (!key.Equals(us, StringComparison.OrdinalIgnoreCase)
                && !key.Equals(uk, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            preferred = UsesUkSpelling(choice) ? uk : us;
            return true;
        }

        return false;
    }
}
