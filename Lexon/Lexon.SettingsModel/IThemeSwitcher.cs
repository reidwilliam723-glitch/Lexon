using Lexon.Core.Theming;

namespace Lexon.SettingsModel;

public interface IThemeSwitcher
{
    IReadOnlyList<string> Names { get; }

    string Current { get; }

    void Apply(string name);

    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
}
