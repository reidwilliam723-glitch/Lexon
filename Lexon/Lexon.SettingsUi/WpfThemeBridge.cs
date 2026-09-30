using System.Windows;
using System.Windows.Media;
using Lexon.Core.Theming;
using MediaColor = System.Windows.Media.Color;

namespace Lexon.SettingsUi;

public static class WpfThemeBridge
{
    public const string DictionaryKey = "LexonThemeBrushes";

    public static ResourceDictionary Create(Theme theme, bool windowsHighContrast)
    {
        var colors = windowsHighContrast ? SystemHighContrast() : FromTheme(theme);
        var dict = new ResourceDictionary();
        foreach (var (key, hex) in colors)
        {
            var color = Parse(hex);
            dict[key] = color;
            dict[key + "Brush"] = new SolidColorBrush(color);
        }

        dict["MotionEnabled"] = SystemParameters.ClientAreaAnimation;
        dict["HoverDuration"] = new Duration(SystemParameters.ClientAreaAnimation
            ? TimeSpan.FromMilliseconds(120)
            : TimeSpan.Zero);
        dict["ToggleDuration"] = new Duration(SystemParameters.ClientAreaAnimation
            ? TimeSpan.FromMilliseconds(180)
            : TimeSpan.Zero);
        return dict;
    }

    public static void ApplyTo(Application app, Theme theme)
    {
        var next = Create(theme, SystemParameters.HighContrast);
        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => Equals(d["LexonThemeId"], DictionaryKey));
        next["LexonThemeId"] = DictionaryKey;
        if (existing != null)
        {
            var index = app.Resources.MergedDictionaries.IndexOf(existing);
            app.Resources.MergedDictionaries[index] = next;
        }
        else
        {
            app.Resources.MergedDictionaries.Insert(0, next);
        }
    }

    private static Dictionary<string, string> FromTheme(Theme theme)
    {
        var c = theme.Colors;
        return new Dictionary<string, string>
        {
            ["Background"] = c.Background,
            ["Surface"] = c.Surface,
            ["SurfaceRaised"] = c.SurfaceRaised,
            ["Border"] = c.Border,
            ["BorderStrong"] = c.BorderStrong,
            ["Text"] = c.Text,
            ["TextSecondary"] = c.TextSecondary,
            ["Primary"] = c.Primary,
            ["PrimaryHover"] = c.PrimaryHover,
            ["PrimaryPressed"] = c.PrimaryPressed,
            ["PrimaryTint"] = c.PrimaryTint,
            ["OnPrimary"] = c.OnPrimary,
            ["Success"] = c.Success,
            ["Warning"] = c.Warning,
            ["Error"] = c.Error
        };
    }

    private static Dictionary<string, string> SystemHighContrast()
    {
        string Hex(MediaColor c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        return new Dictionary<string, string>
        {
            ["Background"] = Hex(SystemColors.WindowColor),
            ["Surface"] = Hex(SystemColors.WindowColor),
            ["SurfaceRaised"] = Hex(SystemColors.WindowColor),
            ["Border"] = Hex(SystemColors.WindowTextColor),
            ["BorderStrong"] = Hex(SystemColors.WindowTextColor),
            ["Text"] = Hex(SystemColors.WindowTextColor),
            ["TextSecondary"] = Hex(SystemColors.GrayTextColor),
            ["Primary"] = Hex(SystemColors.HighlightColor),
            ["PrimaryHover"] = Hex(SystemColors.HighlightTextColor),
            ["PrimaryPressed"] = Hex(SystemColors.HighlightColor),
            ["PrimaryTint"] = Hex(SystemColors.WindowColor),
            ["OnPrimary"] = Hex(SystemColors.HighlightTextColor),
            ["Success"] = Hex(SystemColors.WindowTextColor),
            ["Warning"] = Hex(SystemColors.GrayTextColor),
            ["Error"] = Hex(SystemColors.WindowTextColor)
        };
    }

    private static MediaColor Parse(string hex)
    {
        var value = hex.Trim().TrimStart('#');
        if (value.Length == 6)
        {
            return MediaColor.FromRgb(
                Convert.ToByte(value[..2], 16),
                Convert.ToByte(value[2..4], 16),
                Convert.ToByte(value[4..6], 16));
        }

        return MediaColor.FromRgb(0, 0, 0);
    }
}
