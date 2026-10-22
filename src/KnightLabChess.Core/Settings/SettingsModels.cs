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

    // AI Coach Settings
    public string GeminiApiKey { get; set; } = string.Empty;
    public string GeminiModel { get; set; } = "gemini-2.5-flash";
    public bool EnableAiCoach { get; set; } = true;
    public string CoachPersona { get; set; } = "Encouraging Grandmaster";
    public string CoachSkillLevel { get; set; } = "Intermediate";
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
