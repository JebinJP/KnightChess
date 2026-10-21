namespace KnightLabChess.Core.Puzzles;

/// <summary>
/// Service contract for loading and querying tactical puzzles.
/// </summary>
public interface IPuzzleService
{
    Task<IReadOnlyList<Puzzle>> GetAllPuzzlesAsync();
    Task<IReadOnlyList<Puzzle>> GetPuzzlesByThemeAsync(PuzzleTheme theme);
    Task<IReadOnlyList<Puzzle>> GetPuzzlesByDifficultyAsync(PuzzleDifficulty difficulty);
    Task<Puzzle?> GetPuzzleByIdAsync(string id);
    Task<Puzzle?> GetNextUnsolvedPuzzleAsync(PuzzleDifficulty? difficulty = null, PuzzleTheme? theme = null);
    void InvalidateCache();
}
