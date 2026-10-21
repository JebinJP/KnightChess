namespace KnightLabChess.Core.Openings;

/// <summary>
/// Represents a named chess opening or variation.
/// </summary>
public class OpeningEntry
{
    public string Eco { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Variation { get; set; } = string.Empty;
    public string MovesSan { get; set; } = string.Empty;
    public List<string> MoveListUci { get; set; } = [];
    public string Fen { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string KeyIdeas { get; set; } = string.Empty;
    public string TargetColor { get; set; } = "White"; // "White", "Black", "Both"
    public int MasterGamesCount { get; set; }
    public double WhiteWinPercent { get; set; }
    public double DrawPercent { get; set; }
    public double BlackWinPercent { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(Variation)
        ? Name
        : $"{Name}: {Variation}";
}

/// <summary>
/// A candidate move continuation from a given board position.
/// </summary>
public class OpeningMoveOption
{
    public string UciMove { get; set; } = string.Empty;
    public string SanMove { get; set; } = string.Empty;
    public string ContinuationName { get; set; } = string.Empty;
    public int GamesCount { get; set; }
    public double WhiteWinPercent { get; set; }
    public double DrawPercent { get; set; }
    public double BlackWinPercent { get; set; }
}

/// <summary>
/// Service interface for querying the chess openings database.
/// </summary>
public interface IOpeningService
{
    Task<IReadOnlyList<OpeningEntry>> GetAllOpeningsAsync();
    Task<IReadOnlyList<OpeningEntry>> SearchOpeningsAsync(string query);
    Task<OpeningEntry?> FindOpeningByFenAsync(string fen);
    Task<OpeningEntry?> FindOpeningByMovesAsync(IEnumerable<string> uciMoves);
    Task<IReadOnlyList<OpeningMoveOption>> GetCandidateMovesAsync(IEnumerable<string> uciMoves);
}
