using Microsoft.Win32;

namespace Lexon.Core;

/// <summary>
/// Manages Group Policy settings for enterprise deployments
/// </summary>
public class GroupPolicyManager
{
    private const string PolicyRegistryPath = @"SOFTWARE\Policies\Lexon";
    private const string AIPolicyPath = @"SOFTWARE\Policies\Lexon\AI";
    private const string PrivacyPolicyPath = @"SOFTWARE\Policies\Lexon\Privacy";
    private const string BlockedAppsPath = @"SOFTWARE\Policies\Lexon\BlockedApps";
    private const string LicensingPath = @"SOFTWARE\Lexon\Licensing";

    private readonly Dictionary<string, object> _policyCache = new();
    private bool _isInitialized = false;

    /// <summary>
    /// Initialize Group Policy manager and load all policies
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        LoadAllPolicies();
        _isInitialized = true;
    }

    /// <summary>
    /// Check if Group Policy is being used
    /// </summary>
    public bool IsGroupPolicyEnabled
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(PolicyRegistryPath);
                return key != null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Get a boolean policy value
    /// </summary>
    public bool GetBooleanPolicy(string policyName, bool defaultValue = false)
    {
        EnsureInitialized();

        if (_policyCache.TryGetValue(policyName, out var value))
        {
            if (value is int intValue)
            {
                return intValue != 0;
            }
            if (value is bool boolValue)
            {
                return boolValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Get a string policy value
    /// </summary>
    public string GetStringPolicy(string policyName, string defaultValue = "")
    {
        EnsureInitialized();

        if (_policyCache.TryGetValue(policyName, out var value) && value is string stringValue)
        {
            return stringValue;
        }

        return defaultValue;
    }

    /// <summary>
    /// Get an integer policy value
    /// </summary>
    public int GetIntegerPolicy(string policyName, int defaultValue = 0)
    {
        EnsureInitialized();

        if (_policyCache.TryGetValue(policyName, out var value))
        {
            if (value is int intValue)
            {
                return intValue;
            }
            if (value is string stringValue && int.TryParse(stringValue, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Get a string array policy value
    /// </summary>
    public string[] GetStringArrayPolicy(string policyName, string[] defaultValue = null!)
    {
        EnsureInitialized();

        if (_policyCache.TryGetValue(policyName, out var value))
        {
            if (value is string[] stringArray)
            {
                return stringArray;
            }
            if (value is string stringValue)
            {
                return stringValue.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        return defaultValue ?? Array.Empty<string>();
    }

    /// <summary>
    /// General Settings
    /// </summary>
    public GeneralPolicySettings GeneralSettings => new()
    {
        AutoStart = GetBooleanPolicy("AutoStart", true),
        MinimizeToTray = GetBooleanPolicy("MinimizeToTray", true),
        LocalMode = GetBooleanPolicy("LocalMode", false)
    };

    /// <summary>
    /// AI Settings
    /// </summary>
    public AIPolicySettings AISettings => new()
    {
        Provider = GetStringPolicy("Provider", "None"),
        APIKey = GetStringPolicy("APIKey", ""),
        BaseUrl = GetStringPolicy("BaseUrl", ""),
        Model = GetStringPolicy("Model", "")
    };

    /// <summary>
    /// Privacy Settings
    /// </summary>
    public PrivacyPolicySettings PrivacySettings => new()
    {
        EnableTelemetry = GetBooleanPolicy("EnableTelemetry", false),
        EnableCrashReporting = GetBooleanPolicy("EnableCrashReporting", true),
        DataRetentionDays = GetIntegerPolicy("DataRetentionDays", 30)
    };

    /// <summary>
    /// Blocked Applications
    /// </summary>
    public string[] BlockedApplications => GetStringArrayPolicy("BlockedApps", Array.Empty<string>());

    /// <summary>
    /// Licensing Information
    /// </summary>
    public LicensingSettings Licensing => new()
    {
        LicenseKey = GetStringPolicy("LicenseKey", ""),
        LicenseServer = GetStringPolicy("LicenseServer", ""),
        SeatCount = GetIntegerPolicy("SeatCount", 0)
    };

    /// <summary>
    /// Check if a specific setting is managed by Group Policy
    /// </summary>
    public bool IsPolicyManaged(string policyName)
    {
        EnsureInitialized();
        return _policyCache.ContainsKey(policyName);
    }

    /// <summary>
    /// Reload policies from registry
    /// </summary>
    public void ReloadPolicies()
    {
        _policyCache.Clear();
        LoadAllPolicies();
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            Initialize();
        }
    }

    private void LoadAllPolicies()
    {
        try
        {
            LoadPolicySection(PolicyRegistryPath);
            LoadPolicySection(AIPolicyPath);
            LoadPolicySection(PrivacyPolicyPath);
            LoadPolicySection(BlockedAppsPath);
            LoadPolicySection(LicensingPath);
        }
        catch
        {
            // If policy loading fails, continue with empty cache
        }
    }

    private void LoadPolicySection(string registryPath)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(registryPath);
            if (key == null)
            {
                return;
            }

            foreach (var valueName in key.GetValueNames())
            {
                var value = key.GetValue(valueName);
                if (value != null)
                {
                    _policyCache[valueName] = value;
                }
            }
        }
        catch
        {
            // Skip sections we can't access
        }
    }
}

/// <summary>
/// General policy settings
/// </summary>
public class GeneralPolicySettings
{
    public bool AutoStart { get; set; }
    public bool MinimizeToTray { get; set; }
    public bool LocalMode { get; set; }
}

/// <summary>
/// AI policy settings
/// </summary>
public class AIPolicySettings
{
    public string Provider { get; set; } = string.Empty;
    public string APIKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

/// <summary>
/// Privacy policy settings
/// </summary>
public class PrivacyPolicySettings
{
    public bool EnableTelemetry { get; set; }
    public bool EnableCrashReporting { get; set; }
    public int DataRetentionDays { get; set; }
}

/// <summary>
/// Licensing settings
/// </summary>
public class LicensingSettings
{
    public string LicenseKey { get; set; } = string.Empty;
    public string LicenseServer { get; set; } = string.Empty;
    public int SeatCount { get; set; }
}