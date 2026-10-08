using System.Globalization;
using System.Windows;

namespace Lexon.SettingsUi;

internal static class SettingsWindowPlacement
{
    public const string Key = "SettingsWindowBounds";
    public const double DefaultWidth = 720;
    public const double DefaultHeight = 720;
    public const double MinWidth = 480;
    public const double MinHeight = 400;

    public static string Format(Rect rect)
        => string.Create(CultureInfo.InvariantCulture, $"{rect.X:R}|{rect.Y:R}|{rect.Width:R}|{rect.Height:R}");

    public static bool TryParse(string? text, out Rect rect)
    {
        rect = Rect.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('|');
        if (parts.Length != 4
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
            || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            return false;
        }

        rect = new Rect(x, y, width, height);
        return true;
    }

    public static Rect Clamp(Rect desired, IReadOnlyList<Rect> workAreas)
    {
        var width = Math.Max(MinWidth, desired.Width);
        var height = Math.Max(MinHeight, desired.Height);
        if (workAreas.Count == 0)
        {
            return new Rect(desired.X, desired.Y, width, height);
        }

        width = Math.Min(width, workAreas.Max(area => area.Width));
        height = Math.Min(height, workAreas.Max(area => area.Height));
        var candidate = new Rect(desired.X, desired.Y, width, height);

        Rect? home = null;
        var best = 0d;
        foreach (var area in workAreas)
        {
            var overlap = Rect.Intersect(candidate, area);
            if (overlap.IsEmpty)
            {
                continue;
            }

            var size = overlap.Width * overlap.Height;
            if (size > best)
            {
                best = size;
                home = area;
            }
        }

        if (home == null)
        {
            var primary = workAreas[0];
            return new Rect(
                primary.X + Math.Max(0, (primary.Width - width) / 2),
                primary.Y + Math.Max(0, (primary.Height - height) / 2),
                Math.Min(width, primary.Width),
                Math.Min(height, primary.Height));
        }

        var frame = home.Value;
        var x = Math.Clamp(candidate.X, frame.Left, Math.Max(frame.Left, frame.Right - candidate.Width));
        var y = Math.Clamp(candidate.Y, frame.Top, Math.Max(frame.Top, frame.Bottom - candidate.Height));
        return new Rect(x, y, Math.Min(candidate.Width, frame.Width), Math.Min(candidate.Height, frame.Height));
    }

    public static IReadOnlyList<Rect> VisibleWorkAreas()
    {
        var areas = new List<Rect>();
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var area = screen.WorkingArea;
            areas.Add(new Rect(area.Left, area.Top, area.Width, area.Height));
        }

        return areas;
    }
}
