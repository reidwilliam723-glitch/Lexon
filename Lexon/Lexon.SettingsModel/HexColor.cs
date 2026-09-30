namespace Lexon.SettingsModel;

/// <summary>
/// Parses theme hex colours. Accepts optional <c>#</c>, 6-digit RRGGBB and 8-digit AARRGGBB.
/// </summary>
public static class HexColor
{
    public readonly record struct Rgba(byte A, byte R, byte G, byte B);

    public static Rgba Parse(string hex)
    {
        if (TryParse(hex, out var color))
        {
            return color;
        }

#if DEBUG
        throw new FormatException($"Invalid colour '{hex}'.");
#else
        return new Rgba(255, 0, 0, 0);
#endif
    }

    public static bool TryParse(string? hex, out Rgba color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var value = hex.Trim();
        if (value[0] == '#')
        {
            value = value[1..];
        }

        if (value.Length != 6 && value.Length != 8)
        {
            return false;
        }

        foreach (var c in value)
        {
            var ok = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!ok)
            {
                return false;
            }
        }

        try
        {
            if (value.Length == 6)
            {
                color = new Rgba(
                    255,
                    Convert.ToByte(value[..2], 16),
                    Convert.ToByte(value[2..4], 16),
                    Convert.ToByte(value[4..6], 16));
                return true;
            }

            color = new Rgba(
                Convert.ToByte(value[..2], 16),
                Convert.ToByte(value[2..4], 16),
                Convert.ToByte(value[4..6], 16),
                Convert.ToByte(value[6..8], 16));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
