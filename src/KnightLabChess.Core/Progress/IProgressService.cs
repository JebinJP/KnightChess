using KnightLabChess.Core.Models;

namespace KnightLabChess.Core.Progress;

/// <summary>
/// Service interface for managing user progress data.
/// </summary>
public interface IProgressService
{
    // Lesson progress
    Task<LessonProgress?> GetLessonProgressAsync(string lessonId);
    Task<IReadOnlyList<LessonProgress>> GetAllLessonProgressAsync();
    Task SaveLessonProgressAsync(LessonProgress progress);

    // Puzzle progress
    Task<PuzzleProgress?> GetPuzzleProgressAsync(string puzzleId);
    Task<IReadOnlyList<PuzzleProgress>> GetAllPuzzleProgressAsync();
    Task SavePuzzleProgressAsync(PuzzleProgress progress);

    // Game records
    Task SaveGameRecordAsync(GameRecord record);
    Task<IReadOnlyList<GameRecord>> GetRecentGamesAsync(int count = 10);

    // Statistics
    Task<UserStats> GetUserStatsAsync();
}
