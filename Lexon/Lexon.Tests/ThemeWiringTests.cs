using System.Drawing;
using Lexon.Core.Interfaces;
using Lexon.Core.Theming;
using Lexon.Overlay;
using Xunit;

namespace Lexon.Tests;

public class ThemeWiringTests
{
    [Fact]
    public void SetTheme_RaisesThemeChanged()
    {
        var storage = new MemoryStorage();
        var themeManager = new ThemeManager(storage);
        Theme? received = null;
        themeManager.ThemeChanged += (_, e) => received = e.NewTheme;

        themeManager.SetTheme("Dark");

        Assert.NotNull(received);
        Assert.Equal("Dark", received!.Name);
    }

    [Fact]
    public void OverlayThemePalette_ReadsCurrentThemeColors()
    {
        var palette = new OverlayThemePalette();
        var theme = new Theme
        {
            Name = "Test",
            Colors = new ThemeColors
            {
                Primary = "#0078D4",
                Secondary = "#106EBE",
                Background = "#101010",
                Surface = "#2D2D2D",
                Text = "#FFFFFF",
                TextSecondary = "#CCCCCC",
                Accent = "#8844AA",
                Success = "#107C10",
                Warning = "#FF8C00",
                Error = "#A80000",
                Border = "#505050"
            }
        };

        palette.Apply(theme);

        Assert.Equal(ColorTranslator.FromHtml("#2D2D2D"), palette.Background);
        Assert.Equal(ColorTranslator.FromHtml("#0078D4"), palette.SourceColor("Dictionary"));
        Assert.Equal(ColorTranslator.FromHtml("#8844AA"), palette.SourceColor("AI"));
        Assert.Equal(ColorTranslator.FromHtml("#FFFFFF"), palette.SelectedText);
    }

    [Fact]
    public void OverlayThemePalette_SelectedTextUsesOnPrimary()
    {
        var palette = new OverlayThemePalette();
        palette.Apply(new Theme
        {
            Name = "DarkBrand",
            Colors = new ThemeColors
            {
                Primary = "#6B9BFF",
                OnPrimary = "#0B1020",
                Surface = "#1E2128",
                Background = "#16181D",
                Text = "#F2F4F8",
                TextSecondary = "#A9B1C1",
                Border = "#4A5162",
                Success = "#4CC38A",
                Warning = "#F2B24A",
                Error = "#FF8A7A",
                Accent = "#6B9BFF"
            }
        });

        Assert.Equal(ColorTranslator.FromHtml("#0B1020"), palette.SelectedText);
        Assert.Equal(ColorTranslator.FromHtml("#6B9BFF"), palette.SelectedBackground);
    }

    [Theory]
    [InlineData("Light", "#0A3BA6", "#FFFFFF")]
    [InlineData("Dark", "#6B9BFF", "#0B1020")]
    [InlineData("High Contrast", "#FFFF00", "#000000")]
    public void BuiltInTheme_SelectedRowUsesOnPrimary(string themeName, string primary, string onPrimary)
    {
        var themeManager = new ThemeManager(new MemoryStorage());
        var theme = themeManager.GetTheme(themeName);
        Assert.NotNull(theme);
        Assert.Equal(primary, theme!.Colors.Primary, ignoreCase: true);
        Assert.Equal(onPrimary, theme.Colors.OnPrimary, ignoreCase: true);

        var palette = new OverlayThemePalette();
        palette.Apply(theme);
        Assert.Equal(ColorTranslator.FromHtml(onPrimary), palette.SelectedText);
        Assert.Equal(ColorTranslator.FromHtml(primary), palette.SelectedBackground);
    }

    private sealed class MemoryStorage : IStorage
    {
        private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);

        public Task SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
        {
            _data[key] = System.Text.Json.JsonSerializer.Serialize(data);
            return Task.CompletedTask;
        }

        public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (!_data.TryGetValue(key, out var json))
            {
                return Task.FromResult<T?>(default);
            }

            return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(json));
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _data.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_data.ContainsKey(key));
    }
}
