using KnightLabChess.Core.Settings;
using KnightLabChess.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnightLabChess.Infrastructure.Tests;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly string _tempDbPath;
    private readonly JsonSettingsService _settingsService;

    public JsonSettingsServiceTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), $"knightlab_settings_test_{Guid.NewGuid():N}");
        _tempDbPath = Path.Combine(_tempFolder, "knightlab.db");
        _settingsService = new JsonSettingsService(_tempFolder, _tempDbPath, NullLogger<JsonSettingsService>.Instance);
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaultsWhenFileMissing()
    {
        var settings = await _settingsService.GetSettingsAsync();

        Assert.NotNull(settings);
        Assert.Equal(15, settings.EngineDepth);
        Assert.Equal("Emerald", settings.BoardTheme);
        Assert.True(settings.HighlightLegalMoves);
        Assert.Equal(_tempDbPath, _settingsService.GetDatabasePath());
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsAndReloadsSuccessfully()
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.EngineDepth = 22;
        settings.BoardTheme = "Midnight";
        settings.SoundVolume = 65;

        await _settingsService.SaveSettingsAsync(settings);

        // Create a new instance to bypass in-memory caching
        var newService = new JsonSettingsService(_tempFolder, _tempDbPath, NullLogger<JsonSettingsService>.Instance);
        var loaded = await newService.GetSettingsAsync();

        Assert.Equal(22, loaded.EngineDepth);
        Assert.Equal("Midnight", loaded.BoardTheme);
        Assert.Equal(65, loaded.SoundVolume);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
        {
            try { Directory.Delete(_tempFolder, true); } catch { }
        }
    }
}
