using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SysTTS.Services;

/// <summary>
/// Persists user preferences to a JSON file in the application directory.
/// Thread-safe via lock for concurrent Save/Load operations.
/// </summary>
public class UserPreferences
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private ILogger<UserPreferences>? _logger;

    /// <summary>
    /// Last-used voice ID in picker mode. Null if not yet set.
    /// This property is read-only for external code. Use SetLastUsedPickerVoice() to safely
    /// set the value and save to disk within a lock.
    /// </summary>
    public string? LastUsedPickerVoice { get; private set; }

    /// <summary>
    /// Creates a new UserPreferences instance with the default file path.
    /// </summary>
    public UserPreferences()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "user-preferences.json");
        Load();
    }

    /// <summary>
    /// Creates a new UserPreferences instance with a custom file path (for testing).
    /// </summary>
    /// <param name="filePath">Path to the preferences JSON file</param>
    public UserPreferences(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    /// <summary>
    /// Loads preferences from the JSON file if it exists.
    /// Returns defaults if the file is missing or invalid.
    /// </summary>
    public void Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    _logger?.LogDebug("User preferences file does not exist: {FilePath}", _filePath);
                    LastUsedPickerVoice = null;
                    return;
                }

                var json = File.ReadAllText(_filePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Read LastUsedPickerVoice if present
                if (root.TryGetProperty("lastUsedPickerVoice", out var voiceProp) &&
                    voiceProp.ValueKind == JsonValueKind.String)
                {
                    LastUsedPickerVoice = voiceProp.GetString();
                }
                else
                {
                    LastUsedPickerVoice = null;
                }

                _logger?.LogDebug("Loaded user preferences from {FilePath}", _filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load user preferences from {FilePath}; using defaults", _filePath);
                LastUsedPickerVoice = null;
            }
        }
    }

    /// <summary>
    /// Saves current preferences to the JSON file.
    /// Thread-safe via lock.
    /// </summary>
    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var json = new
                {
                    lastUsedPickerVoice = LastUsedPickerVoice
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var jsonString = JsonSerializer.Serialize(json, options);
                File.WriteAllText(_filePath, jsonString);

                _logger?.LogDebug("Saved user preferences to {FilePath}", _filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save user preferences to {FilePath}", _filePath);
            }
        }
    }

    /// <summary>
    /// Sets the last-used voice ID and saves to disk atomically within a lock.
    /// This ensures thread-safe read/write of the property with persistence.
    /// </summary>
    /// <param name="voiceId">The voice ID to save, or null to clear</param>
    public void SetLastUsedPickerVoice(string? voiceId)
    {
        lock (_lock)
        {
            LastUsedPickerVoice = voiceId;
            Save();
        }
    }

    /// <summary>
    /// Internal method to set logger (for dependency injection scenarios).
    /// </summary>
    internal void SetLogger(ILogger<UserPreferences> logger)
    {
        _logger = logger;
    }
}
