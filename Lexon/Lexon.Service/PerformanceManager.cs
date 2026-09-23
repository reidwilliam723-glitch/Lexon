namespace Lexon.Service;

/// <summary>
/// Performance mode toggle with different profiles
/// </summary>
public class PerformanceManager
{
    private PerformanceMode _currentMode = PerformanceMode.Balanced;
    private PerformanceProfile _activeProfile = PerformanceProfiles.Balanced;

    public PerformanceMode CurrentMode
    {
        get => _currentMode;
        set
        {
            _currentMode = value;
            _activeProfile = GetProfile(value);
            ApplyProfile(_activeProfile);
        }
    }

    public event EventHandler<PerformanceModeChangedEventArgs>? ModeChanged;

    public PerformanceManager()
    {
        ApplyProfile(_activeProfile);
    }

    public void SetMode(PerformanceMode mode)
    {
        CurrentMode = mode;
        ModeChanged?.Invoke(this, new PerformanceModeChangedEventArgs
        {
            PreviousMode = _currentMode,
            NewMode = mode
        });
    }

    public PerformanceProfile GetProfile(PerformanceMode mode)
    {
        return mode switch
        {
            PerformanceMode.BatterySaver => PerformanceProfiles.BatterySaver,
            PerformanceMode.Balanced => PerformanceProfiles.Balanced,
            PerformanceMode.MaximumQuality => PerformanceProfiles.MaximumQuality,
            PerformanceMode.Custom => PerformanceProfiles.Balanced,
            _ => PerformanceProfiles.Balanced
        };
    }

    public void ApplyProfile(PerformanceProfile profile)
    {
        // Apply performance settings based on profile
        // This would affect suggestion generation, caching, AI usage, etc.
    }

    public PerformanceProfile CreateCustomProfile(string name)
    {
        return new PerformanceProfile
        {
            Name = name,
            Mode = PerformanceMode.Custom,
            MaxSuggestions = 5,
            SuggestionUpdateInterval = 500,
            EnableCaching = true,
            CacheSize = 100,
            EnableAI = true,
            AIRequestTimeout = 5000,
            BackgroundProcessing = true,
            MemoryLimitMB = 100
        };
    }

    public void UpdateCustomProfile(PerformanceProfile profile)
    {
        if (profile.Mode == PerformanceMode.Custom)
        {
            ApplyProfile(profile);
        }
    }
}

public class PerformanceProfile
{
    public string Name { get; set; } = string.Empty;
    public PerformanceMode Mode { get; set; }
    public int MaxSuggestions { get; set; } = 5;
    public int SuggestionUpdateInterval { get; set; } = 300; // milliseconds
    public bool EnableCaching { get; set; } = true;
    public int CacheSize { get; set; } = 100;
    public bool EnableAI { get; set; } = true;
    public int AIRequestTimeout { get; set; } = 3000; // milliseconds
    public bool BackgroundProcessing { get; set; } = true;
    public int MemoryLimitMB { get; set; } = 100;
    public bool EnableAnimations { get; set; } = true;
}

public static class PerformanceProfiles
{
    public static PerformanceProfile BatterySaver => new()
    {
        Name = "Battery Saver",
        Mode = PerformanceMode.BatterySaver,
        MaxSuggestions = 3,
        SuggestionUpdateInterval = 1000,
        EnableCaching = true,
        CacheSize = 50,
        EnableAI = false,
        AIRequestTimeout = 0,
        BackgroundProcessing = false,
        MemoryLimitMB = 50,
        EnableAnimations = false
    };

    public static PerformanceProfile Balanced => new()
    {
        Name = "Balanced",
        Mode = PerformanceMode.Balanced,
        MaxSuggestions = 5,
        SuggestionUpdateInterval = 300,
        EnableCaching = true,
        CacheSize = 100,
        EnableAI = true,
        AIRequestTimeout = 3000,
        BackgroundProcessing = true,
        MemoryLimitMB = 100,
        EnableAnimations = true
    };

    public static PerformanceProfile MaximumQuality => new()
    {
        Name = "Maximum Quality",
        Mode = PerformanceMode.MaximumQuality,
        MaxSuggestions = 10,
        SuggestionUpdateInterval = 150,
        EnableCaching = true,
        CacheSize = 200,
        EnableAI = true,
        AIRequestTimeout = 5000,
        BackgroundProcessing = true,
        MemoryLimitMB = 200,
        EnableAnimations = true
    };
}

public enum PerformanceMode
{
    BatterySaver,
    Balanced,
    MaximumQuality,
    Custom
}

public class PerformanceModeChangedEventArgs : EventArgs
{
    public PerformanceMode PreviousMode { get; set; }
    public PerformanceMode NewMode { get; set; }
}
