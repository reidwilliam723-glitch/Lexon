using Lexon.Core.Theming;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public sealed class ThemeManagerSwitcher : IThemeSwitcher
{
    private readonly ThemeManager _themes;

    public ThemeManagerSwitcher(ThemeManager themes)
    {
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        _themes.ThemeChanged += (_, e) => ThemeChanged?.Invoke(this, e);
    }

    public IReadOnlyList<string> Names
        => _themes.AvailableThemes.Select(theme => theme.Name).ToList();

    public string Current => _themes.CurrentTheme.Name;

    public void Apply(string name) => _themes.SetTheme(name);

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
}
