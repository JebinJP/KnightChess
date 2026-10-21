namespace KnightLabChess.Core.Progress;

/// <summary>
/// Tracks the user's progress on a specific lesson.
/// </summary>
public class LessonProgress
{
    public int Id { get; set; }
    public string LessonId { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int CurrentStepIndex { get; set; }
    public int TotalSteps { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalAttempts { get; set; }
    public double MasteryScore { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tracks puzzle performance.
/// </summary>
public class PuzzleProgress
{
    public int Id { get; set; }
    public string PuzzleId { get; set; } = string.Empty;
    public bool IsSolved { get; set; }
    public int Attempts { get; set; }
    public bool UsedHint { get; set; }
    public DateTime? SolvedAt { get; set; }
    public DateTime LastAttemptAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Overall user statistics.
/// </summary>
public class UserStats
{
    public int TotalLessonsCompleted { get; set; }
    public int TotalPuzzlesSolved { get; set; }
    public int TotalPuzzlesAttempted { get; set; }
    public double PuzzleAccuracy { get; set; }
    public int TotalGamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesLost { get; set; }
    public int GamesDrawn { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateTime? LastActivityAt { get; set; }
}
