namespace KnightLabChess.Core.Settings;

/// <summary>
/// User configuration settings for KnightLab Chess.
/// </summary>
public class UserSettings
{
    // Engine Settings
    public string StockfishPath { get; set; } = string.Empty;
    public int EngineDepth { get; set; } = 15;
    public int EngineThreads { get; set; } = 2;
    public int EngineHashMb { get; set; } = 128;
    public bool UseBuiltinFallbackIfNotFound { get; set; } = true;

    // Board & Visuals
    public string BoardTheme { get; set; } = "Emerald";
    public bool HighlightLegalMoves { get; set; } = true;
    public bool ShowMoveAnimations { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public int SoundVolume { get; set; } = 80;

    // Gameplay
    public bool AutoQueenPromotion { get; set; } = false;
    public bool ShowEngineEvalDuringPlay { get; set; } = false;
}

/// <summary>
/// Service interface for reading and writing user application settings.
/// </summary>
public interface ISettingsService
{
    Task<UserSettings> GetSettingsAsync();
    Task SaveSettingsAsync(UserSettings settings);
    string GetDatabasePath();
}
