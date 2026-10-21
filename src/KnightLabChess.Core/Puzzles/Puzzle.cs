using System.Text.Json.Serialization;

namespace KnightLabChess.Core.Puzzles;

/// <summary>
/// Represents a tactical puzzle theme.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PuzzleTheme
{
    Fork,
    Pin,
    Skewer,
    DiscoveredAttack,
    DoubleCheck,
    RemoveTheDefender,
    Deflection,
    Decoy,
    MatingPattern,
    BackRankMate,
    SmotheredMate,
    Sacrifice,
    Promotion,
    Endgame,
    Opening,
    Middlegame,
    Mixed
}

/// <summary>
/// Represents a puzzle difficulty level.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PuzzleDifficulty
{
    Easy,
    Medium,
    Hard,
    Expert
}

/// <summary>
/// Represents a single tactical puzzle.
/// </summary>
public class Puzzle
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PuzzleTheme Theme { get; set; }
    public PuzzleDifficulty Difficulty { get; set; }

    /// <summary>The starting FEN position.</summary>
    public string Fen { get; set; } = string.Empty;

    /// <summary>The solution moves in UCI notation (e.g., "e2e4 d7d5 e4d5").</summary>
    public List<string> SolutionMoves { get; set; } = [];

    /// <summary>The side to move first (typically the side presenting the puzzle).</summary>
    public string PlayerColor { get; set; } = "White";

    /// <summary>Hint text.</summary>
    public string? Hint { get; set; }

    /// <summary>Explanation of the solution.</summary>
    public string? Explanation { get; set; }

    /// <summary>Rating/difficulty score (optional Elo-like rating).</summary>
    public int? Rating { get; set; }
}
