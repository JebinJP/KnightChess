namespace KnightLabChess.Core.Audio;

/// <summary>
/// Service contract for managing and triggering audio effects across the application.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// Master toggle for enabling or muting all sound effects.
    /// </summary>
    bool IsSoundEnabled { get; set; }

    /// <summary>
    /// Playback volume level from 0 (silent) to 100 (full).
    /// </summary>
    int Volume { get; set; }

    /// <summary>
    /// Plays the requested sound effect asynchronously if audio is enabled.
    /// </summary>
    void Play(ChessSound sound);
}
