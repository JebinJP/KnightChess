using System.Text.Json;
using KnightLabChess.Core.Settings;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Persistence;

/// <summary>
/// Persists application settings to a JSON file in LocalApplicationData.
/// </summary>
public class JsonSettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly string _databasePath;
    private readonly ILogger<JsonSettingsService> _logger;
    private UserSettings? _cachedSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonSettingsService(string baseFolder, string databasePath, ILogger<JsonSettingsService> logger)
    {
        Directory.CreateDirectory(baseFolder);
        _settingsFilePath = Path.Combine(baseFolder, "settings.json");
        _databasePath = databasePath;
        _logger = logger;
    }

    public async Task<UserSettings> GetSettingsAsync()
    {
        if (_cachedSettings is not null)
            return _cachedSettings;

        if (!File.Exists(_settingsFilePath))
        {
            _cachedSettings = new UserSettings();
            await SaveSettingsAsync(_cachedSettings);
            return _cachedSettings;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            _cachedSettings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? new UserSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings file from {Path}, returning defaults", _settingsFilePath);
            _cachedSettings = new UserSettings();
        }

        return _cachedSettings;
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        _cachedSettings = settings;
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json);
            _logger.LogInformation("Saved application settings to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write settings to {Path}", _settingsFilePath);
        }
    }

    public string GetDatabasePath() => _databasePath;
}
