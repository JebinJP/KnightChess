namespace KnightLabChess.Core.Lessons;

/// <summary>
/// Service interface for loading and managing lessons.
/// </summary>
public interface ILessonService
{
    /// <summary>
    /// Gets all available lessons.
    /// </summary>
    Task<IReadOnlyList<Lesson>> GetAllLessonsAsync();

    /// <summary>
    /// Gets lessons for a specific stage.
    /// </summary>
    Task<IReadOnlyList<Lesson>> GetLessonsByStageAsync(LessonStage stage);

    /// <summary>
    /// Gets a specific lesson by ID.
    /// </summary>
    Task<Lesson?> GetLessonByIdAsync(string lessonId);

    /// <summary>
    /// Gets the recommended next lesson for the user.
    /// </summary>
    Task<Lesson?> GetNextRecommendedLessonAsync();
}
