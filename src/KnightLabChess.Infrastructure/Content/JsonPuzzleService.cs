using System.Text.Json;
using KnightLabChess.Core.Progress;
using KnightLabChess.Core.Puzzles;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Content;

/// <summary>
/// Loads tactical puzzle collections from JSON files on disk.
/// </summary>
public class JsonPuzzleService : IPuzzleService
{
    private readonly string _contentDirectory;
    private readonly IProgressService _progressService;
    private readonly ILogger<JsonPuzzleService> _logger;
    private List<Puzzle>? _cachedPuzzles;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public JsonPuzzleService(string contentDirectory, IProgressService progressService,
        ILogger<JsonPuzzleService> logger)
    {
        _contentDirectory = contentDirectory;
        _progressService = progressService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Puzzle>> GetAllPuzzlesAsync()
    {
        if (_cachedPuzzles is not null)
            return _cachedPuzzles;

        _cachedPuzzles = [];

        string? puzzlesDir = null;
        var current = new DirectoryInfo(_contentDirectory);
        while (current is not null)
        {
            var p1 = Path.Combine(current.FullName, "puzzles");
            if (Directory.Exists(p1)) { puzzlesDir = p1; break; }

            var p2 = Path.Combine(current.FullName, "content", "puzzles");
            if (Directory.Exists(p2)) { puzzlesDir = p2; break; }

            current = current.Parent;
        }

        if (puzzlesDir is null)
        {
            _logger.LogWarning("Puzzles directory not found in any probed paths from: {Dir}", _contentDirectory);
            return _cachedPuzzles;
        }

        foreach (var file in Directory.GetFiles(puzzlesDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var puzzles = JsonSerializer.Deserialize<List<Puzzle>>(json, JsonOptions);
                if (puzzles is not null)
                {
                    _cachedPuzzles.AddRange(puzzles);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load puzzle file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} puzzles from disk", _cachedPuzzles.Count);
        return _cachedPuzzles;
    }

    public async Task<IReadOnlyList<Puzzle>> GetPuzzlesByThemeAsync(PuzzleTheme theme)
    {
        var all = await GetAllPuzzlesAsync();
        return all.Where(p => p.Theme == theme).ToList();
    }

    public async Task<IReadOnlyList<Puzzle>> GetPuzzlesByDifficultyAsync(PuzzleDifficulty difficulty)
    {
        var all = await GetAllPuzzlesAsync();
        return all.Where(p => p.Difficulty == difficulty).ToList();
    }

    public async Task<Puzzle?> GetPuzzleByIdAsync(string id)
    {
        var all = await GetAllPuzzlesAsync();
        return all.FirstOrDefault(p => p.Id == id);
    }

    public async Task<Puzzle?> GetNextUnsolvedPuzzleAsync(PuzzleDifficulty? difficulty = null, PuzzleTheme? theme = null)
    {
        var all = await GetAllPuzzlesAsync();
        var allProgress = await _progressService.GetAllPuzzleProgressAsync();
        var solvedIds = allProgress.Where(p => p.IsSolved).Select(p => p.PuzzleId).ToHashSet();

        var query = all.AsEnumerable();
        if (difficulty.HasValue)
            query = query.Where(p => p.Difficulty == difficulty.Value);
        if (theme.HasValue)
            query = query.Where(p => p.Theme == theme.Value);

        var list = query.ToList();
        if (list.Count == 0) return null;

        // First unsolved in query
        var unsolved = list.FirstOrDefault(p => !solvedIds.Contains(p.Id));
        return unsolved ?? list[0]; // or first if all solved
    }

    public void InvalidateCache()
    {
        _cachedPuzzles = null;
    }
}
