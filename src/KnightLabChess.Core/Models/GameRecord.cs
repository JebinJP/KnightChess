namespace KnightLabChess.Core.Models;

/// <summary>
/// Represents the result of a chess game.
/// </summary>
public enum GameResult
{
    InProgress,
    WhiteWins,
    BlackWins,
    Draw,
    Abandoned
}

/// <summary>
/// Represents why a game ended.
/// </summary>
public enum GameEndReason
{
    None,
    Checkmate,
    Stalemate,
    Resignation,
    ThreefoldRepetition,
    FiftyMoveRule,
    InsufficientMaterial,
    Agreement,
    Timeout,
    Abandoned
}

/// <summary>
/// Represents a saved chess game record.
/// </summary>
public class GameRecord
{
    public int Id { get; set; }
    public string WhitePlayer { get; set; } = "Player";
    public string BlackPlayer { get; set; } = "Computer";
    public GameResult Result { get; set; } = GameResult.InProgress;
    public GameEndReason EndReason { get; set; } = GameEndReason.None;
    public string Pgn { get; set; } = string.Empty;
    public string StartFen { get; set; } = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    public string CurrentFen { get; set; } = string.Empty;
    public string MoveHistoryJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int MoveCount { get; set; }
    public string? DifficultyLevel { get; set; }
}
