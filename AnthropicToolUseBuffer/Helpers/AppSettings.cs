using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnthropicToolUseBuffer.Helpers
{
    /// <summary>
    /// Application settings loaded from appsettings.json
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Anthropic API configuration
        /// </summary>
        public AnthropicSettings Anthropic { get; set; } = new();

        /// <summary>
        /// General application settings
        /// </summary>
        public GeneralSettings General { get; set; } = new();

        /// <summary>
        /// Database settings
        /// </summary>
        public DatabaseSettings Database { get; set; } = new();
    }

    public class AnthropicSettings
    {
        /// <summary>
        /// Anthropic API Key (keep this secure!)
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Default model to use
        /// </summary>
        public string DefaultModel { get; set; } = "claude-haiku-4-5-20251001";

        /// <summary>
        /// Cache alive timer interval in minutes
        /// </summary>
        public double CacheAliveIntervalMinutes { get; set; } = 4.75;
    }
 
 
    public class GeneralSettings
    {
 
        /// <summary>
        /// Enable tool usage
        /// </summary>
        public bool UseTools { get; set; } = true;
 
        /// <summary>
        /// Tool pair timeout in minutes
        /// </summary>
        public int ToolPairTimeoutMinutes { get; set; } = 5;
    }

    public class DatabaseSettings
    {
        /// <summary>
        /// Default database name
        /// </summary>
        public string DefaultDatabaseName { get; set; } = "ToolBufferDemoMessageDatabase.db";
    }

    /// <summary>
    /// Settings manager for loading and saving configuration
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "appsettings.json"
        );

        private static readonly object _lock = new object();
 
        /// <summary>
        /// Loads settings from appsettings.json or creates default if not found
        /// </summary>
        public static AppSettings LoadSettings()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        string json = File.ReadAllText(SettingsFilePath);
                        var settings = JsonSerializer.Deserialize<AppSettings>(json, GetJsonOptions());

                        if (settings != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SettingsManager] Settings loaded from {SettingsFilePath}");
                            return settings;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[SettingsManager] Settings file not found, creating default settings");
                    
                    var defaultSettings = CreateDefaultSettings();
                    SaveSettings(defaultSettings);
                    return defaultSettings;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsManager] Error loading settings: {ex.Message}");
       
                    return CreateDefaultSettings();
                }
            }
        }

        /// <summary>
        /// Saves settings to appsettings.json
        /// </summary>
        public static void SaveSettings(AppSettings settings)
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(settings, GetJsonOptions());
                    File.WriteAllText(SettingsFilePath, json);
                    System.Diagnostics.Debug.WriteLine($"[SettingsManager] Settings saved to {SettingsFilePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsManager] Error saving settings: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Creates default settings with secure placeholders
        /// </summary>
        private static AppSettings CreateDefaultSettings()
        {
            return new AppSettings
            {
                Anthropic = new AnthropicSettings
                {
                    ApiKey = "YOUR_API_KEY_HERE",
                    DefaultModel = "claude-sonnet-4-5",
                    CacheAliveIntervalMinutes = 4.75
                },
                General = new GeneralSettings
                { 
                    UseTools = true, 
                    ToolPairTimeoutMinutes = 5
                },
                Database = new DatabaseSettings
                {
                    DefaultDatabaseName = "ToolBufferDemoMessageDatabase"
                }
            };
        }

        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        /// <summary>
        /// Gets the path to the settings file
        /// </summary>
        public static string GetSettingsPath() => SettingsFilePath;
    }
}
