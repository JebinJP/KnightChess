namespace KnightLabChess.Core.Engine;

/// <summary>
/// Difficulty level for the chess engine opponent.
/// </summary>
public enum EngineDifficulty
{
    /// <summary>Beginner (~800 Elo) - Skill level 1, shallow search</summary>
    Beginner,
    /// <summary>Casual (~1200 Elo) - Skill level 5</summary>
    Casual,
    /// <summary>Intermediate (~1600 Elo) - Skill level 10</summary>
    Intermediate,
    /// <summary>Advanced (~2000 Elo) - Skill level 15</summary>
    Advanced,
    /// <summary>Master (Full Strength) - Skill level 20, deep search</summary>
    Master
}

/// <summary>
/// Game opponent type for the play arena.
/// </summary>
public enum GameMode
{
    HumanVsHuman,
    HumanVsComputer
}

/// <summary>
/// Represents real-time position analysis and evaluation metrics from an engine.
/// </summary>
public class EngineEvaluation
{
    /// <summary>Centipawn score relative to side to move (e.g. +150 = +1.5 pawns advantage).</summary>
    public int? ScoreCentipawns { get; set; }

    /// <summary>Forced checkmate in N moves (positive = side to move mates, negative = being mated).</summary>
    public int? MateInMoves { get; set; }

    /// <summary>Best recommended move in UCI notation (e.g. "e2e4").</summary>
    public string? BestMoveUci { get; set; }

    /// <summary>Calculation depth reached.</summary>
    public int Depth { get; set; }

    /// <summary>Nodes searched per second.</summary>
    public long NodesPerSecond { get; set; }

    /// <summary>Principal variation moves.</summary>
    public List<string> PrincipalVariation { get; set; } = [];

    /// <summary>Formatted score string (e.g. "+1.4" or "M3").</summary>
    public string FormattedScore =>
        MateInMoves.HasValue
            ? $"M{Math.Abs(MateInMoves.Value)}"
            : ScoreCentipawns.HasValue
                ? $"{(ScoreCentipawns.Value >= 0 ? "+" : "")}{(ScoreCentipawns.Value / 100.0):F1}"
                : "0.0";
}
