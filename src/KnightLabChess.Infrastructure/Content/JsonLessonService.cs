using System.Text.Json;
using KnightLabChess.Core.Lessons;
using KnightLabChess.Core.Progress;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Content;

/// <summary>
/// Loads lesson content from JSON files on disk.
/// </summary>
public class JsonLessonService : ILessonService
{
    private readonly string _contentDirectory;
    private readonly IProgressService _progressService;
    private readonly ILogger<JsonLessonService> _logger;
    private List<Lesson>? _cachedLessons;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public JsonLessonService(string contentDirectory, IProgressService progressService,
        ILogger<JsonLessonService> logger)
    {
        _contentDirectory = contentDirectory;
        _progressService = progressService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Lesson>> GetAllLessonsAsync()
    {
        if (_cachedLessons is not null)
            return _cachedLessons;

        _cachedLessons = [];

        string? lessonsDir = null;
        var current = new DirectoryInfo(_contentDirectory);
        while (current is not null)
        {
            var p1 = Path.Combine(current.FullName, "lessons");
            if (Directory.Exists(p1)) { lessonsDir = p1; break; }

            var p2 = Path.Combine(current.FullName, "content", "lessons");
            if (Directory.Exists(p2)) { lessonsDir = p2; break; }

            current = current.Parent;
        }

        if (lessonsDir is null)
        {
            _logger.LogWarning("Lessons directory not found in any probed paths from: {Dir}", _contentDirectory);
            return _cachedLessons;
        }

        foreach (var file in Directory.GetFiles(lessonsDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var lessons = JsonSerializer.Deserialize<List<Lesson>>(json, JsonOptions);
                if (lessons is not null)
                {
                    _cachedLessons.AddRange(lessons);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load lesson file: {File}", file);
            }
        }

        _cachedLessons = [.. _cachedLessons.OrderBy(l => l.Stage).ThenBy(l => l.Order)];
        _logger.LogInformation("Loaded {Count} lessons", _cachedLessons.Count);
        return _cachedLessons;
    }

    public async Task<IReadOnlyList<Lesson>> GetLessonsByStageAsync(LessonStage stage)
    {
        var all = await GetAllLessonsAsync();
        return all.Where(l => l.Stage == stage).ToList();
    }

    public async Task<Lesson?> GetLessonByIdAsync(string lessonId)
    {
        var all = await GetAllLessonsAsync();
        return all.FirstOrDefault(l => l.Id == lessonId);
    }

    public async Task<Lesson?> GetNextRecommendedLessonAsync()
    {
        var all = await GetAllLessonsAsync();
        var allProgress = await _progressService.GetAllLessonProgressAsync();
        var completedIds = allProgress.Where(p => p.IsCompleted).Select(p => p.LessonId).ToHashSet();

        // Find the first incomplete lesson in order
        foreach (var lesson in all)
        {
            if (!completedIds.Contains(lesson.Id))
                return lesson;
        }

        // All completed — suggest revisiting
        return all.FirstOrDefault();
    }

    /// <summary>
    /// Invalidates the cached lessons to force a reload.
    /// </summary>
    public void InvalidateCache()
    {
        _cachedLessons = null;
    }
}
