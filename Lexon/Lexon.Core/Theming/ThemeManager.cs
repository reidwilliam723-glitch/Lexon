using System.Text.Json;
using Lexon.Core.Interfaces;

namespace Lexon.Core.Theming;

/// <summary>
/// Manages application themes and appearance settings
/// </summary>
public class ThemeManager
{
    private readonly IStorage _storage;
    private Theme _currentTheme;
    private readonly Dictionary<string, Theme> _availableThemes;
    private readonly object _lock = new();
    private const string ThemeConfigKey = "theme_config";
    
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public Theme CurrentTheme => _currentTheme;
    public IEnumerable<Theme> AvailableThemes
    {
        get { lock (_lock) { return _availableThemes.Values.ToList(); } }
    }

    public ThemeManager(IStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _availableThemes = new Dictionary<string, Theme>();
        InitializeBuiltInThemes();
        _currentTheme = GetDefaultTheme();
    }

    /// <summary>
    /// Loads the persisted theme selection and any custom themes. Must be
    /// awaited by the caller before relying on <see cref="CurrentTheme"/>
    /// reflecting the user's saved choice.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var configData = await _storage.LoadAsync<string>(ThemeConfigKey);
            if (string.IsNullOrEmpty(configData))
            {
                return;
            }

            var config = JsonSerializer.Deserialize<ThemeConfiguration>(configData);
            if (config == null)
            {
                return;
            }

            lock (_lock)
            {
                foreach (var customTheme in config.CustomThemes)
                {
                    _availableThemes[customTheme.Name] = customTheme;
                }

                if (!string.IsNullOrEmpty(config.CurrentThemeName)
                    && _availableThemes.TryGetValue(config.CurrentThemeName, out var theme))
                {
                    _currentTheme = theme;
                }
            }
        }
        catch
        {
            // Use default theme if loading fails
        }
    }

    /// <summary>
    /// Set the current theme
    /// </summary>
    public void SetTheme(string themeName)
    {
        Theme? theme;
        lock (_lock)
        {
            if (!_availableThemes.TryGetValue(themeName, out theme))
            {
                return;
            }
            _currentTheme = theme;
        }

        SaveThemeConfiguration();
        OnThemeChanged(theme);
    }

    /// <summary>
    /// Set theme by theme object
    /// </summary>
    public void SetTheme(Theme theme)
    {
        if (theme != null)
        {
            lock (_lock)
            {
                _currentTheme = theme;
                _availableThemes[theme.Name] = theme;
            }
            SaveThemeConfiguration();
            OnThemeChanged(theme);
        }
    }

    /// <summary>
    /// Add a custom theme
    /// </summary>
    public void AddCustomTheme(Theme theme)
    {
        if (theme != null && !string.IsNullOrEmpty(theme.Name))
        {
            lock (_lock)
            {
                _availableThemes[theme.Name] = theme;
            }
            SaveThemeConfiguration();
        }
    }

    /// <summary>
    /// Remove a custom theme
    /// </summary>
    public void RemoveTheme(string themeName)
    {
        bool removed;
        lock (_lock)
        {
            removed = _availableThemes.ContainsKey(themeName)
                && !_currentTheme.Name.Equals(themeName, StringComparison.OrdinalIgnoreCase);
            if (removed)
            {
                _availableThemes.Remove(themeName);
            }
        }

        if (removed)
        {
            SaveThemeConfiguration();
        }
    }

    /// <summary>
    /// Get a specific theme by name
    /// </summary>
    public Theme? GetTheme(string themeName)
    {
        lock (_lock)
        {
            return _availableThemes.TryGetValue(themeName, out var theme) ? theme : null;
        }
    }

    /// <summary>
    /// Create a custom theme based on current theme
    /// </summary>
    public Theme CreateCustomTheme(string name, string description = "")
    {
        return new Theme
        {
            Name = name,
            Description = description,
            IsCustom = true,
            Colors = new ThemeColors
            {
                Primary = _currentTheme.Colors.Primary,
                Secondary = _currentTheme.Colors.Secondary,
                Background = _currentTheme.Colors.Background,
                Surface = _currentTheme.Colors.Surface,
                SurfaceRaised = _currentTheme.Colors.SurfaceRaised,
                Text = _currentTheme.Colors.Text,
                TextSecondary = _currentTheme.Colors.TextSecondary,
                Accent = _currentTheme.Colors.Accent,
                Success = _currentTheme.Colors.Success,
                Warning = _currentTheme.Colors.Warning,
                Error = _currentTheme.Colors.Error,
                Border = _currentTheme.Colors.Border,
                BorderStrong = _currentTheme.Colors.BorderStrong,
                PrimaryHover = _currentTheme.Colors.PrimaryHover,
                PrimaryPressed = _currentTheme.Colors.PrimaryPressed,
                PrimaryTint = _currentTheme.Colors.PrimaryTint,
                OnPrimary = _currentTheme.Colors.OnPrimary
            },
            Fonts = new ThemeFonts
            {
                Primary = _currentTheme.Fonts.Primary,
                Monospace = _currentTheme.Fonts.Monospace,
                Size = _currentTheme.Fonts.Size
            },
            Appearance = new ThemeAppearance
            {
                WindowOpacity = _currentTheme.Appearance.WindowOpacity,
                BorderRadius = _currentTheme.Appearance.BorderRadius,
                Spacing = _currentTheme.Appearance.Spacing,
                Shadow = _currentTheme.Appearance.Shadow
            }
        };
    }

    /// <summary>
    /// Export current theme to JSON
    /// </summary>
    public string ExportTheme()
    {
        return JsonSerializer.Serialize(_currentTheme, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Import theme from JSON
    /// </summary>
    public bool ImportTheme(string jsonData)
    {
        try
        {
            var theme = JsonSerializer.Deserialize<Theme>(jsonData);
            if (theme != null && !string.IsNullOrEmpty(theme.Name))
            {
                AddCustomTheme(theme);
                return true;
            }
        }
        catch
        {
            // Invalid JSON format
        }
        return false;
    }

    /// <summary>
    /// Reset to default theme
    /// </summary>
    public void ResetToDefault()
    {
        SetTheme(GetDefaultTheme());
    }

    private void InitializeBuiltInThemes()
    {
        // Light Theme
        _availableThemes["Light"] = new Theme
        {
            Name = "Light",
            Description = "Default light theme",
            IsCustom = false,
            Colors = new ThemeColors
            {
                Primary = "#0A3BA6",
                Secondary = "#0D47C9",
                Background = "#FFFFFF",
                Surface = "#F4F6FB",
                SurfaceRaised = "#FFFFFF",
                Text = "#10172A",
                TextSecondary = "#4A5468",
                Accent = "#0A3BA6",
                Success = "#0F7B4B",
                Warning = "#9A5B00",
                Error = "#B42318",
                Border = "#D9DFEB",
                BorderStrong = "#7B869C",
                PrimaryHover = "#0D47C9",
                PrimaryPressed = "#082F85",
                PrimaryTint = "#E7EEFE",
                OnPrimary = "#FFFFFF"
            },
            Fonts = new ThemeFonts
            {
                Primary = "Segoe UI Variable Text",
                Monospace = "Consolas",
                Size = 9
            },
            Appearance = new ThemeAppearance
            {
                WindowOpacity = 1.0,
                BorderRadius = 4,
                Spacing = 8,
                Shadow = true
            }
        };

        // Dark Theme
        _availableThemes["Dark"] = new Theme
        {
            Name = "Dark",
            Description = "Dark theme for low-light environments",
            IsCustom = false,
            Colors = new ThemeColors
            {
                Primary = "#6B9BFF",
                Secondary = "#8AB4FF",
                Background = "#16181D",
                Surface = "#1E2128",
                SurfaceRaised = "#262A33",
                Text = "#F2F4F8",
                TextSecondary = "#A9B1C1",
                Accent = "#6B9BFF",
                Success = "#4CC38A",
                Warning = "#F2B24A",
                Error = "#FF8A7A",
                Border = "#4A5162",
                BorderStrong = "#6B7590",
                PrimaryHover = "#8AB4FF",
                PrimaryPressed = "#5B8CFF",
                PrimaryTint = "#1F2A44",
                OnPrimary = "#0B1020"
            },
            Fonts = new ThemeFonts
            {
                Primary = "Segoe UI Variable Text",
                Monospace = "Consolas",
                Size = 9
            },
            Appearance = new ThemeAppearance
            {
                WindowOpacity = 0.95,
                BorderRadius = 4,
                Spacing = 8,
                Shadow = true
            }
        };

        // High Contrast Theme
        _availableThemes["High Contrast"] = new Theme
        {
            Name = "High Contrast",
            Description = "High contrast theme for accessibility",
            IsCustom = false,
            Colors = new ThemeColors
            {
                Primary = "#FFFF00",
                Secondary = "#FFFFFF",
                Background = "#000000",
                Surface = "#000000",
                SurfaceRaised = "#000000",
                Text = "#FFFFFF",
                TextSecondary = "#FFFF00",
                Accent = "#FFFF00",
                Success = "#00FF00",
                Warning = "#FFFF00",
                Error = "#FF0000",
                Border = "#FFFFFF",
                BorderStrong = "#FFFFFF",
                PrimaryHover = "#FFFFFF",
                PrimaryPressed = "#FFFF00",
                PrimaryTint = "#1A1A1A",
                OnPrimary = "#000000"
            },
            Fonts = new ThemeFonts
            {
                Primary = "Segoe UI",
                Monospace = "Consolas",
                Size = 10
            },
            Appearance = new ThemeAppearance
            {
                WindowOpacity = 1.0,
                BorderRadius = 0,
                Spacing = 12,
                Shadow = false
            }
        };
    }

    private Theme GetDefaultTheme()
    {
        return _availableThemes.TryGetValue("Light", out var theme) ? theme : CreateDefaultTheme();
    }

    private Theme CreateDefaultTheme()
    {
        return new Theme
        {
            Name = "Default",
            Description = "Default theme",
            IsCustom = false,
            Colors = new ThemeColors(),
            Fonts = new ThemeFonts(),
            Appearance = new ThemeAppearance()
        };
    }

    private void SaveThemeConfiguration()
    {
        // Fire-and-forget is intentional (a save shouldn't block the caller),
        // but failures are caught rather than left unobserved.
        _ = SaveThemeConfigurationAsync();
    }

    private async Task SaveThemeConfigurationAsync()
    {
        try
        {
            ThemeConfiguration config;
            lock (_lock)
            {
                config = new ThemeConfiguration
                {
                    CurrentThemeName = _currentTheme.Name,
                    CustomThemes = _availableThemes.Values.Where(t => t.IsCustom).ToList()
                };
            }

            var configData = JsonSerializer.Serialize(config);
            await _storage.SaveAsync(ThemeConfigKey, configData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save theme configuration: {ex.Message}");
        }
    }

    protected virtual void OnThemeChanged(Theme newTheme)
    {
        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs { NewTheme = newTheme });
    }
}

/// <summary>
/// Represents a complete theme definition
/// </summary>
public class Theme
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
    public ThemeColors Colors { get; set; } = new();
    public ThemeFonts Fonts { get; set; } = new();
    public ThemeAppearance Appearance { get; set; } = new();
}

/// <summary>
/// Theme color definitions
/// </summary>
public class ThemeColors
{
    public string Primary { get; set; } = "#0A3BA6";
    public string Secondary { get; set; } = "#0D47C9";
    public string Background { get; set; } = "#FFFFFF";
    public string Surface { get; set; } = "#F4F6FB";
    public string SurfaceRaised { get; set; } = "#FFFFFF";
    public string Text { get; set; } = "#10172A";
    public string TextSecondary { get; set; } = "#4A5468";
    public string Accent { get; set; } = "#0A3BA6";
    public string Success { get; set; } = "#0F7B4B";
    public string Warning { get; set; } = "#9A5B00";
    public string Error { get; set; } = "#B42318";
    public string Border { get; set; } = "#D9DFEB";
    public string BorderStrong { get; set; } = "#7B869C";
    public string PrimaryHover { get; set; } = "#0D47C9";
    public string PrimaryPressed { get; set; } = "#082F85";
    public string PrimaryTint { get; set; } = "#E7EEFE";
    public string OnPrimary { get; set; } = "#FFFFFF";
}

/// <summary>
/// Theme font definitions
/// </summary>
public class ThemeFonts
{
    public string Primary { get; set; } = "Segoe UI";
    public string Monospace { get; set; } = "Consolas";
    public int Size { get; set; } = 9;
}

/// <summary>
/// Theme appearance settings
/// </summary>
public class ThemeAppearance
{
    public double WindowOpacity { get; set; } = 1.0;
    public int BorderRadius { get; set; } = 4;
    public int Spacing { get; set; } = 8;
    public bool Shadow { get; set; } = true;
}

/// <summary>
/// Theme configuration for persistence
/// </summary>
public class ThemeConfiguration
{
    public string CurrentThemeName { get; set; } = string.Empty;
    public List<Theme> CustomThemes { get; set; } = new();
}

/// <summary>
/// Event args for theme changes
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public Theme NewTheme { get; set; } = null!;
}