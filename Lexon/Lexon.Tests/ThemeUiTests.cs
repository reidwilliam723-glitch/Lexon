using System.Drawing;
using Lexon.Core.Theming;
using Lexon.Storage;
using Lexon.Ui;
using Xunit;

namespace Lexon.Tests;

public class ThemeUiTests
{
    [Fact]
    public void ThemeUi_ButtonContrast_IsAtLeastFourPointFive()
    {
        using var store = new TempStore();
        var themes = new ThemeManager(store.Storage);
        foreach (var theme in themes.AvailableThemes)
        {
            using var button = new Button();
            ThemeUi.ApplyToTree(button, theme);
            AssertContrast($"{theme.Name} rest", button.ForeColor, button.BackColor);
            AssertContrast($"{theme.Name} hover", button.ForeColor, button.FlatAppearance.MouseOverBackColor);
            AssertContrast($"{theme.Name} pressed", button.ForeColor, button.FlatAppearance.MouseDownBackColor);
        }
    }

    private static void AssertContrast(string label, Color fg, Color bg)
    {
        var ratio = Ratio(fg, bg);
        Assert.True(ratio + 0.001 >= 4.5, $"{label}: {ratio:0.00}:1 (#{fg.R:X2}{fg.G:X2}{fg.B:X2} on #{bg.R:X2}{bg.G:X2}{bg.B:X2})");
    }

    private static double Ratio(Color a, Color b)
    {
        var l1 = Luminance(a);
        var l2 = Luminance(b);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color color)
        => 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private sealed class TempStore : IDisposable
    {
        public EncryptedStorage Storage { get; }
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "lexon-themeui-" + Guid.NewGuid().ToString("N"));

        public TempStore()
        {
            Directory.CreateDirectory(_dir);
            Storage = new EncryptedStorage(_dir);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch
            {
                // temp
            }
        }
    }
}
