namespace KnightLabChess.Core.Engine;

/// <summary>
/// Abstraction for chess engine interactions (UCI engines like Stockfish or fallback engines).
/// </summary>
public interface IEngineService : IDisposable
{
    /// <summary>Whether a valid UCI engine process (e.g., Stockfish) was detected and initialized.</summary>
    bool IsEngineAvailable { get; }

    /// <summary>Human-readable name or version of the active engine.</summary>
    string EngineName { get; }

    /// <summary>
    /// Initializes or reloads the engine process from the specified executable path or system default.
    /// </summary>
    Task<bool> InitializeAsync(string? executablePath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the engine for the best move in UCI format (e.g. "e2e4") for a given position.
    /// </summary>
    Task<string?> GetBestMoveAsync(string fen, EngineDifficulty difficulty = EngineDifficulty.Casual,
        int moveTimeMs = 800, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates the position at the given FEN and returns calculation metrics and principal variation.
    /// </summary>
    Task<EngineEvaluation?> EvaluatePositionAsync(string fen, int depth = 12, int timeoutMs = 1500,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Halts any current search immediately.
    /// </summary>
    Task StopAsync();
}
