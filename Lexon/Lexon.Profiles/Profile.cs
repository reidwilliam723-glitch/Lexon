using Lexon.Core.Interfaces;
using System.Text.Json;

namespace Lexon.Profiles;

/// <summary>
/// User profile for managing settings and preferences
/// </summary>
public class Profile : IProfile
{
    public const string DefaultProfileId = "default";

    public string Id { get; set; } = DefaultProfileId;
    public string Name { get; set; } = "Default";
    public Dictionary<string, object> Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    private IStorage? _storage;

    public Profile()
    {
        // Parameterless constructor for JSON deserialization
    }

    public Profile(IStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public void SetStorage(IStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Gets the storage key for a profile ID.
    /// This centralizes the key format to avoid coupling issues.
    /// </summary>
    public static string GetStorageKey(string profileId)
    {
        return $"profile_{profileId}";
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        LastModified = DateTime.UtcNow;
        await _storage.SaveAsync(GetStorageKey(Id), this, cancellationToken);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await _storage.LoadAsync<Profile>(GetStorageKey(Id), cancellationToken);
        if (loaded != null)
        {
            // Copy properties from loaded profile to this instance
            Name = loaded.Name;
            Settings = loaded.Settings;
            CreatedAt = loaded.CreatedAt;
            LastModified = loaded.LastModified;
        }
    }

    public T GetSetting<T>(string key, T defaultValue = default!)
    {
        if (Settings.TryGetValue(key, out var value))
        {
            // If value is already the correct type, return it
            if (value is T typedValue)
            {
                return typedValue;
            }

            // If value is a JsonElement (from JSON deserialization), deserialize it
            if (value is JsonElement jsonElement)
            {
                try
                {
                    return jsonElement.Deserialize<T>() ?? defaultValue;
                }
                catch (JsonException)
                {
                    // If JSON deserialization fails, fall back to default
                }
            }

            // If value is a string and T is not string, try JSON deserialization
            if (value is string jsonString && typeof(T) != typeof(string))
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(jsonString) ?? defaultValue;
                }
                catch (JsonException)
                {
                    // If JSON deserialization fails, fall back to Convert.ChangeType
                }
            }

            // Try Convert.ChangeType for simple types
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (InvalidCastException)
            {
                // If conversion fails, return default value
            }
            catch (FormatException)
            {
                // If format is invalid, return default value
            }
        }
        return defaultValue;
    }

    public virtual void SetSetting<T>(string key, T value)
    {
        Settings[key] = value!;
    }

    /// <summary>
    /// Check if a setting exists in the profile
    /// </summary>
    public bool HasSetting(string key)
    {
        return Settings.ContainsKey(key);
    }
}
