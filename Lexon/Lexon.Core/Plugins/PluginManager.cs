using Lexon.Core.Interfaces;
using Lexon.Core.Models;
using System.Reflection;
using System.Text.Json;

namespace Lexon.Core.Plugins;

/// <summary>
/// Manages loading and lifecycle of custom suggestion provider plugins
/// </summary>
public class PluginManager
{
    private readonly IStorage _storage;
    private readonly Dictionary<string, PluginInfo> _loadedPlugins;
    private readonly Dictionary<string, ISuggestionProvider> _pluginProviders;
    private readonly string _pluginsDirectory;
    private readonly object _lock = new();
    private Dictionary<string, bool> _pendingPluginStates = new();
    private const string PluginsConfigKey = "plugins_config";
    
    public bool IsEnabled { get; set; }
    public event EventHandler<PluginEventArgs>? PluginLoaded;
    public event EventHandler<PluginEventArgs>? PluginUnloaded;
    public event EventHandler<PluginErrorEventArgs>? PluginError;

    public PluginManager(IStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _loadedPlugins = new Dictionary<string, PluginInfo>();
        _pluginProviders = new Dictionary<string, ISuggestionProvider>();
        
        _pluginsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lexon",
            "Plugins"
        );
        
        Directory.CreateDirectory(_pluginsDirectory);
        LoadPluginConfiguration();
    }

    /// <summary>
    /// Load all plugins from the plugins directory
    /// </summary>
    public async Task LoadAllPluginsAsync()
    {
        if (!IsEnabled) return;

        try
        {
            var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll");
            
            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    await LoadPluginAsync(pluginFile);
                }
                catch (Exception ex)
                {
                    OnPluginError(pluginFile, $"Failed to load plugin: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            OnPluginError("PluginManager", $"Failed to scan plugins directory: {ex.Message}");
        }
    }

    /// <summary>
    /// Load a specific plugin from a DLL file
    /// </summary>
    public async Task<ISuggestionProvider?> LoadPluginAsync(string pluginPath)
    {
        if (!IsEnabled) return null;
        if (!File.Exists(pluginPath)) return null;

        try
        {
            var assembly = Assembly.LoadFrom(pluginPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(ISuggestionProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    var plugin = (ISuggestionProvider)Activator.CreateInstance(pluginType)!;
                    var pluginInfo = ExtractPluginInfo(assembly, pluginType);
                    ApplyPendingState(pluginInfo);

                    lock (_lock)
                    {
                        _loadedPlugins[plugin.Name] = pluginInfo;
                        _pluginProviders[plugin.Name] = plugin;
                    }

                    OnPluginLoaded(pluginInfo);
                    SavePluginConfiguration();
                    
                    return plugin;
                }
                catch (Exception ex)
                {
                    OnPluginError(pluginType.Name, $"Failed to instantiate plugin: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            OnPluginError(pluginPath, $"Failed to load assembly: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Unload a plugin by name
    /// </summary>
    public void UnloadPlugin(string pluginName)
    {
        PluginInfo? pluginInfo;
        lock (_lock)
        {
            if (!_loadedPlugins.TryGetValue(pluginName, out pluginInfo))
            {
                return;
            }

            _loadedPlugins.Remove(pluginName);
            _pluginProviders.Remove(pluginName);
        }

        OnPluginUnloaded(pluginInfo);
        SavePluginConfiguration();
    }

    /// <summary>
    /// Get all loaded plugin providers
    /// </summary>
    public IEnumerable<ISuggestionProvider> GetPluginProviders()
    {
        lock (_lock)
        {
            return _pluginProviders.Values.ToList();
        }
    }

    /// <summary>
    /// Get information about all loaded plugins
    /// </summary>
    public IEnumerable<PluginInfo> GetLoadedPlugins()
    {
        lock (_lock)
        {
            return _loadedPlugins.Values.ToList();
        }
    }

    /// <summary>
    /// Get a specific plugin provider by name
    /// </summary>
    public ISuggestionProvider? GetPluginProvider(string pluginName)
    {
        lock (_lock)
        {
            return _pluginProviders.TryGetValue(pluginName, out var provider) ? provider : null;
        }
    }

    /// <summary>
    /// Enable or disable a specific plugin
    /// </summary>
    public void SetPluginEnabled(string pluginName, bool enabled)
    {
        bool found;
        lock (_lock)
        {
            found = _loadedPlugins.TryGetValue(pluginName, out var pluginInfo);
            if (found)
            {
                pluginInfo!.IsEnabled = enabled;
            }
        }

        if (found)
        {
            SavePluginConfiguration();
        }
    }

    /// <summary>
    /// Check if a plugin is enabled
    /// </summary>
    public bool IsPluginEnabled(string pluginName)
    {
        lock (_lock)
        {
            return _loadedPlugins.TryGetValue(pluginName, out var pluginInfo) && pluginInfo.IsEnabled;
        }
    }

    /// <summary>
    /// Install a plugin from a file path
    /// </summary>
    public async Task<bool> InstallPluginAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath)) return false;

        try
        {
            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(_pluginsDirectory, fileName);
            
            // Copy plugin to plugins directory
            File.Copy(sourcePath, destPath, true);
            
            // Load the plugin
            var plugin = await LoadPluginAsync(destPath);
            return plugin != null;
        }
        catch (Exception ex)
        {
            OnPluginError(sourcePath, $"Failed to install plugin: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Uninstall and remove a plugin
    /// </summary>
    public bool UninstallPlugin(string pluginName)
    {
        PluginInfo? pluginInfo;
        lock (_lock)
        {
            _loadedPlugins.TryGetValue(pluginName, out pluginInfo);
        }

        if (pluginInfo != null)
        {
            try
            {
                UnloadPlugin(pluginName);
                
                if (File.Exists(pluginInfo.FilePath))
                {
                    File.Delete(pluginInfo.FilePath);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                OnPluginError(pluginName, $"Failed to uninstall plugin: {ex.Message}");
                return false;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Extract plugin metadata from assembly
    /// </summary>
    private PluginInfo ExtractPluginInfo(Assembly assembly, Type pluginType)
    {
        var pluginAttribute = pluginType.GetCustomAttribute<PluginAttribute>();
        
        return new PluginInfo
        {
            Name = pluginAttribute?.Name ?? pluginType.Name,
            Version = pluginAttribute?.Version ?? assembly.GetName().Version?.ToString() ?? "1.0.0",
            Description = pluginAttribute?.Description ?? "Custom suggestion provider",
            Author = pluginAttribute?.Author ?? "Unknown",
            FilePath = assembly.Location,
            TypeName = pluginType.FullName ?? pluginType.Name,
            IsEnabled = true,
            LoadedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Load saved plugin enable/disable state from storage into a pending-state
    /// buffer. This runs from the constructor, before any plugin DLL has been
    /// loaded into <see cref="_loadedPlugins"/>, so the states can't be applied
    /// yet — <see cref="ApplyPendingState"/> applies them as each plugin loads.
    /// </summary>
    private void LoadPluginConfiguration()
    {
        try
        {
            var configData = _storage.LoadAsync<string>(PluginsConfigKey).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(configData))
            {
                var config = JsonSerializer.Deserialize<PluginConfiguration>(configData);
                if (config != null)
                {
                    _pendingPluginStates = new Dictionary<string, bool>(config.PluginStates);
                }
            }
        }
        catch
        {
            // Start with default configuration
        }
    }

    /// <summary>
    /// Apply a previously-loaded saved enable/disable state to a plugin that
    /// has just been loaded, if one was saved for it.
    /// </summary>
    private void ApplyPendingState(PluginInfo pluginInfo)
    {
        if (_pendingPluginStates.TryGetValue(pluginInfo.Name, out var savedEnabled))
        {
            pluginInfo.IsEnabled = savedEnabled;
        }
    }

    private void SavePluginConfiguration()
    {
        try
        {
            Dictionary<string, bool> states;
            lock (_lock)
            {
                states = _loadedPlugins.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.IsEnabled);
            }

            var config = new PluginConfiguration { PluginStates = states };
            var configData = JsonSerializer.Serialize(config);
            _storage.SaveAsync(PluginsConfigKey, configData).GetAwaiter().GetResult();
        }
        catch
        {
            // Silently fail if save fails
        }
    }

    protected virtual void OnPluginLoaded(PluginInfo pluginInfo)
    {
        PluginLoaded?.Invoke(this, new PluginEventArgs { PluginInfo = pluginInfo });
    }

    protected virtual void OnPluginUnloaded(PluginInfo pluginInfo)
    {
        PluginUnloaded?.Invoke(this, new PluginEventArgs { PluginInfo = pluginInfo });
    }

    protected virtual void OnPluginError(string source, string message)
    {
        PluginError?.Invoke(this, new PluginErrorEventArgs { Source = source, Message = message });
    }
}

/// <summary>
/// Attribute for marking plugin classes with metadata
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PluginAttribute : Attribute
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

/// <summary>
/// Information about a loaded plugin
/// </summary>
public class PluginInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime LoadedAt { get; set; }
}

/// <summary>
/// Plugin configuration data
/// </summary>
public class PluginConfiguration
{
    public Dictionary<string, bool> PluginStates { get; set; } = new();
}

/// <summary>
/// Event args for plugin operations
/// </summary>
public class PluginEventArgs : EventArgs
{
    public PluginInfo PluginInfo { get; set; } = null!;
}

/// <summary>
/// Event args for plugin errors
/// </summary>
public class PluginErrorEventArgs : EventArgs
{
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}