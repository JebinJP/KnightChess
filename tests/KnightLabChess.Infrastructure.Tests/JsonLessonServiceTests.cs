using KnightLabChess.Core.Lessons;
using KnightLabChess.Core.Progress;
using KnightLabChess.Infrastructure.Content;
using KnightLabChess.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnightLabChess.Infrastructure.Tests;

public class JsonLessonServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseManager _dbManager;
    private readonly SqliteProgressService _progressService;
    private readonly JsonLessonService _lessonService;

    public JsonLessonServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"knightlab_lesson_test_{Guid.NewGuid():N}.db");
        _dbManager = new DatabaseManager(_dbPath, NullLogger<DatabaseManager>.Instance);
        _dbManager.InitializeAsync().GetAwaiter().GetResult();
        _progressService = new SqliteProgressService(_dbManager, NullLogger<SqliteProgressService>.Instance);

        // AppDomain.CurrentDomain.BaseDirectory probes back up to solution's content/lessons
        _lessonService = new JsonLessonService(
            AppDomain.CurrentDomain.BaseDirectory,
            _progressService,
            NullLogger<JsonLessonService>.Instance);
    }

    [Fact]
    public async Task GetAllLessonsAsync_LoadsBeginnerLessonsSuccessfully()
    {
        var lessons = await _lessonService.GetAllLessonsAsync();

        Assert.NotEmpty(lessons);
        Assert.Contains(lessons, l => l.Id == "beg_01_the_board");
        Assert.Contains(lessons, l => l.Id == "beg_02_the_rook");
        Assert.Contains(lessons, l => l.Id == "beg_03_the_knight");

        var rookLesson = lessons.First(l => l.Id == "beg_02_the_rook");
        Assert.Equal("The Mighty Rook", rookLesson.Title);
        Assert.Equal(LessonStage.Beginner, rookLesson.Stage);
        Assert.NotEmpty(rookLesson.Objectives);
        Assert.Equal(3, rookLesson.Steps.Count);
    }

    [Fact]
    public async Task GetLessonsByStageAsync_ReturnsOnlyRequestedStage()
    {
        var beginnerLessons = await _lessonService.GetLessonsByStageAsync(LessonStage.Beginner);
        Assert.Equal(9, beginnerLessons.Count);
        Assert.All(beginnerLessons, l => Assert.Equal(LessonStage.Beginner, l.Stage));

        var fundLessons = await _lessonService.GetLessonsByStageAsync(LessonStage.Fundamentals);
        Assert.Equal(3, fundLessons.Count);
        Assert.All(fundLessons, l => Assert.Equal(LessonStage.Fundamentals, l.Stage));

        var masterLessons = await _lessonService.GetLessonsByStageAsync(LessonStage.Master);
        Assert.Empty(masterLessons);
    }

    [Fact]
    public async Task GetLessonByIdAsync_ReturnsCorrectLessonWithSteps()
    {
        var lesson = await _lessonService.GetLessonByIdAsync("beg_02_the_rook");

        Assert.NotNull(lesson);
        Assert.Equal("The Mighty Rook", lesson.Title);

        // Check step types and content
        Assert.Equal(LessonStepType.Explanation, lesson.Steps[0].Type);
        Assert.Equal(LessonStepType.Exercise, lesson.Steps[1].Type);
        Assert.NotNull(lesson.Steps[1].CorrectMoves);
        Assert.Contains("Rd8", lesson.Steps[1].CorrectMoves!);

        Assert.Equal(LessonStepType.Exercise, lesson.Steps[2].Type);
        Assert.NotNull(lesson.Steps[2].CorrectMoves);
        Assert.Contains("Rxh5", lesson.Steps[2].CorrectMoves!);
    }

    [Fact]
    public async Task GetNextRecommendedLessonAsync_RecommendsFirstIncompleteLesson()
    {
        // Initially, first lesson is recommended
        var next1 = await _lessonService.GetNextRecommendedLessonAsync();
        Assert.NotNull(next1);
        Assert.Equal("beg_01_the_board", next1.Id);

        // Mark first lesson completed
        await _progressService.SaveLessonProgressAsync(new LessonProgress
        {
            LessonId = "beg_01_the_board",
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow
        });

        // Next recommended should now be second lesson
        var next2 = await _lessonService.GetNextRecommendedLessonAsync();
        Assert.NotNull(next2);
        Assert.Equal("beg_02_the_rook", next2.Id);
    }

    public void Dispose()
    {
        _dbManager.Dispose();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* ignore */ }
        }
    }
}
