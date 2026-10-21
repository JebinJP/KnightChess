using KnightLabChess.Core.Progress;
using KnightLabChess.Core.Puzzles;
using KnightLabChess.Infrastructure.Content;
using KnightLabChess.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnightLabChess.Infrastructure.Tests;

public class JsonPuzzleServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseManager _dbManager;
    private readonly SqliteProgressService _progressService;
    private readonly JsonPuzzleService _puzzleService;

    public JsonPuzzleServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"knightlab_puzzle_test_{Guid.NewGuid():N}.db");
        _dbManager = new DatabaseManager(_dbPath, NullLogger<DatabaseManager>.Instance);
        _dbManager.InitializeAsync().GetAwaiter().GetResult();
        _progressService = new SqliteProgressService(_dbManager, NullLogger<SqliteProgressService>.Instance);

        // AppDomain.CurrentDomain.BaseDirectory probes back up to solution content/puzzles
        _puzzleService = new JsonPuzzleService(
            AppDomain.CurrentDomain.BaseDirectory,
            _progressService,
            NullLogger<JsonPuzzleService>.Instance);
    }

    [Fact]
    public async Task GetAllPuzzlesAsync_LoadsPuzzlesFromDisk()
    {
        var puzzles = await _puzzleService.GetAllPuzzlesAsync();

        Assert.NotEmpty(puzzles);
        Assert.True(puzzles.Count >= 8);
        Assert.Contains(puzzles, p => p.Id == "puz_01_back_rank");
        Assert.Contains(puzzles, p => p.Id == "puz_02_royal_fork");
    }

    [Fact]
    public async Task GetPuzzlesByDifficultyAsync_FiltersCorrectly()
    {
        var easyPuzzles = await _puzzleService.GetPuzzlesByDifficultyAsync(PuzzleDifficulty.Easy);
        Assert.NotEmpty(easyPuzzles);
        Assert.All(easyPuzzles, p => Assert.Equal(PuzzleDifficulty.Easy, p.Difficulty));

        var mediumPuzzles = await _puzzleService.GetPuzzlesByDifficultyAsync(PuzzleDifficulty.Medium);
        Assert.NotEmpty(mediumPuzzles);
        Assert.All(mediumPuzzles, p => Assert.Equal(PuzzleDifficulty.Medium, p.Difficulty));
    }

    [Fact]
    public async Task GetPuzzlesByThemeAsync_FiltersCorrectly()
    {
        var forkPuzzles = await _puzzleService.GetPuzzlesByThemeAsync(PuzzleTheme.Fork);
        Assert.NotEmpty(forkPuzzles);
        Assert.All(forkPuzzles, p => Assert.Equal(PuzzleTheme.Fork, p.Theme));
    }

    [Fact]
    public async Task GetPuzzleByIdAsync_ReturnsCorrectPuzzle()
    {
        var puzzle = await _puzzleService.GetPuzzleByIdAsync("puz_01_back_rank");

        Assert.NotNull(puzzle);
        Assert.Equal("Back Rank Destruction", puzzle.Title);
        Assert.Equal(PuzzleTheme.BackRankMate, puzzle.Theme);
        Assert.Single(puzzle.SolutionMoves);
        Assert.Equal("d1d8", puzzle.SolutionMoves[0]);
    }

    [Fact]
    public async Task GetNextUnsolvedPuzzleAsync_RespectsSavedProgress()
    {
        var first = await _puzzleService.GetNextUnsolvedPuzzleAsync();
        Assert.NotNull(first);

        // Mark the first puzzle as solved in the database
        await _progressService.SavePuzzleProgressAsync(new PuzzleProgress
        {
            PuzzleId = first.Id,
            IsSolved = true,
            SolvedAt = DateTime.UtcNow,
            Attempts = 1
        });

        var next = await _puzzleService.GetNextUnsolvedPuzzleAsync();
        Assert.NotNull(next);
        Assert.NotEqual(first.Id, next.Id);
    }

    public void Dispose()
    {
        _dbManager.Dispose();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }
}
