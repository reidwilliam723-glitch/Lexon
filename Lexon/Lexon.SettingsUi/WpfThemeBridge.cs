using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Lexon.Core.Theming;
using Lexon.SettingsModel;
using MediaColor = System.Windows.Media.Color;

namespace Lexon.SettingsUi;

public static class WpfThemeBridge
{
    public const string DictionaryKey = "LexonThemeBrushes";

    public static ResourceDictionary Create(Theme theme, bool windowsHighContrast)
    {
        var colors = windowsHighContrast ? SystemHighContrast() : FromTheme(theme);
        var dict = new ResourceDictionary();
        foreach (var (key, color) in colors)
        {
            dict[key] = color;
            dict[key + "Brush"] = new SolidColorBrush(color);
        }

        dict["MotionEnabled"] = SystemParameters.ClientAreaAnimation;
        dict["PopupAnimation"] = SystemParameters.ClientAreaAnimation
            ? PopupAnimation.Slide
            : PopupAnimation.None;
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

    private static Dictionary<string, MediaColor> FromTheme(Theme theme)
    {
        var c = theme.Colors;
        return new Dictionary<string, MediaColor>
        {
            ["Background"] = ToMedia(c.Background),
            ["Surface"] = ToMedia(c.Surface),
            ["SurfaceRaised"] = ToMedia(c.SurfaceRaised),
            ["Border"] = ToMedia(c.Border),
            ["BorderStrong"] = ToMedia(c.BorderStrong),
            ["Text"] = ToMedia(c.Text),
            ["TextSecondary"] = ToMedia(c.TextSecondary),
            ["Primary"] = ToMedia(c.Primary),
            ["PrimaryHover"] = ToMedia(c.PrimaryHover),
            ["PrimaryPressed"] = ToMedia(c.PrimaryPressed),
            ["PrimaryTint"] = ToMedia(c.PrimaryTint),
            ["OnPrimary"] = ToMedia(c.OnPrimary),
            ["OnPrimaryHover"] = ToMedia(c.OnPrimary),
            ["Success"] = ToMedia(c.Success),
            ["Warning"] = ToMedia(c.Warning),
            ["Error"] = ToMedia(c.Error),
            ["Info"] = ToMedia(c.Primary)
        };
    }

    private static Dictionary<string, MediaColor> SystemHighContrast()
        => new()
        {
            ["Background"] = SystemColors.WindowColor,
            ["Surface"] = SystemColors.WindowColor,
            ["SurfaceRaised"] = SystemColors.WindowColor,
            ["Border"] = SystemColors.WindowTextColor,
            ["BorderStrong"] = SystemColors.WindowTextColor,
            ["Text"] = SystemColors.WindowTextColor,
            ["TextSecondary"] = SystemColors.GrayTextColor,
            ["Primary"] = SystemColors.HighlightColor,
            ["PrimaryHover"] = SystemColors.HighlightTextColor,
            ["PrimaryPressed"] = SystemColors.HighlightColor,
            ["PrimaryTint"] = SystemColors.WindowColor,
            ["OnPrimary"] = SystemColors.HighlightTextColor,
            ["OnPrimaryHover"] = SystemColors.HighlightColor,
            ["Success"] = SystemColors.WindowTextColor,
            ["Warning"] = SystemColors.GrayTextColor,
            ["Error"] = SystemColors.WindowTextColor,
            ["Info"] = SystemColors.HighlightColor
        };

    private static MediaColor ToMedia(string hex)
    {
        var parsed = HexColor.Parse(hex);
        return MediaColor.FromArgb(parsed.A, parsed.R, parsed.G, parsed.B);
    }
}
