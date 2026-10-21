using System.Text.Json;
using KnightLabChess.Core.Openings;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Content;

/// <summary>
/// Service that loads and queries the master chess opening repertoire database.
/// </summary>
public class JsonOpeningService : IOpeningService
{
    private readonly string _contentDirectory;
    private readonly ILogger<JsonOpeningService> _logger;
    private List<OpeningEntry>? _cachedOpenings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public JsonOpeningService(string contentDirectory, ILogger<JsonOpeningService> logger)
    {
        _contentDirectory = contentDirectory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OpeningEntry>> GetAllOpeningsAsync()
    {
        if (_cachedOpenings is not null)
            return _cachedOpenings;

        _cachedOpenings = [];

        string? openingsDir = null;
        var current = new DirectoryInfo(_contentDirectory);
        while (current is not null)
        {
            var p1 = Path.Combine(current.FullName, "openings");
            if (Directory.Exists(p1)) { openingsDir = p1; break; }

            var p2 = Path.Combine(current.FullName, "content", "openings");
            if (Directory.Exists(p2)) { openingsDir = p2; break; }

            current = current.Parent;
        }

        if (openingsDir is null)
        {
            _logger.LogWarning("Openings directory not found in probed paths from: {Dir}", _contentDirectory);
            return _cachedOpenings;
        }

        foreach (var file in Directory.GetFiles(openingsDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var entries = JsonSerializer.Deserialize<List<OpeningEntry>>(json, JsonOptions);
                if (entries is not null)
                {
                    _cachedOpenings.AddRange(entries);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load opening file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} chess openings from disk", _cachedOpenings.Count);
        return _cachedOpenings;
    }

    public async Task<IReadOnlyList<OpeningEntry>> SearchOpeningsAsync(string query)
    {
        var all = await GetAllOpeningsAsync();
        if (string.IsNullOrWhiteSpace(query))
            return all;

        var q = query.Trim().ToLowerInvariant();
        return all.Where(o =>
            o.Name.ToLowerInvariant().Contains(q) ||
            o.Variation.ToLowerInvariant().Contains(q) ||
            o.Eco.ToLowerInvariant().Contains(q) ||
            o.MovesSan.ToLowerInvariant().Contains(q)).ToList();
    }

    public async Task<OpeningEntry?> FindOpeningByFenAsync(string fen)
    {
        var all = await GetAllOpeningsAsync();
        var normalized = NormalizeFen(fen);
        return all.FirstOrDefault(o => NormalizeFen(o.Fen) == normalized);
    }

    public async Task<OpeningEntry?> FindOpeningByMovesAsync(IEnumerable<string> uciMoves)
    {
        var all = await GetAllOpeningsAsync();
        var moves = uciMoves.ToList();
        if (moves.Count == 0) return null;

        // Find best match with longest prefix match
        OpeningEntry? bestMatch = null;
        int longestMatch = 0;

        foreach (var opening in all)
        {
            int matchCount = 0;
            for (int i = 0; i < Math.Min(moves.Count, opening.MoveListUci.Count); i++)
            {
                if (string.Equals(moves[i], opening.MoveListUci[i], StringComparison.OrdinalIgnoreCase))
                {
                    matchCount++;
                }
                else
                {
                    break;
                }
            }

            // An opening matches if all its defined moves match the played prefix
            if (matchCount == opening.MoveListUci.Count && matchCount > longestMatch)
            {
                longestMatch = matchCount;
                bestMatch = opening;
            }
        }

        // If no complete opening matched yet (e.g. only 1 move played), show most popular parent opening
        if (bestMatch is null && moves.Count > 0)
        {
            bestMatch = all
                .Where(o => o.MoveListUci.Count >= moves.Count &&
                            moves.Select((m, i) => string.Equals(m, o.MoveListUci[i], StringComparison.OrdinalIgnoreCase)).All(x => x))
                .OrderByDescending(o => o.MasterGamesCount)
                .FirstOrDefault();
        }

        return bestMatch;
    }

    public async Task<IReadOnlyList<OpeningMoveOption>> GetCandidateMovesAsync(IEnumerable<string> uciMoves)
    {
        var all = await GetAllOpeningsAsync();
        var moves = uciMoves.ToList();
        var candidates = new Dictionary<string, List<OpeningEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var opening in all)
        {
            // Verify prefix match
            bool matches = true;
            for (int i = 0; i < moves.Count; i++)
            {
                if (i >= opening.MoveListUci.Count ||
                    !string.Equals(moves[i], opening.MoveListUci[i], StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                    break;
                }
            }

            if (matches && opening.MoveListUci.Count > moves.Count)
            {
                var nextMove = opening.MoveListUci[moves.Count];
                if (!candidates.TryGetValue(nextMove, out var list))
                {
                    list = [];
                    candidates[nextMove] = list;
                }
                list.Add(opening);
            }
        }

        var results = new List<OpeningMoveOption>();
        foreach (var (nextMove, entries) in candidates)
        {
            var topEntry = entries.OrderByDescending(e => e.MasterGamesCount).First();
            int totalGames = entries.Sum(e => e.MasterGamesCount);
            double avgWhiteWin = entries.Average(e => e.WhiteWinPercent);
            double avgDraw = entries.Average(e => e.DrawPercent);
            double avgBlackWin = entries.Average(e => e.BlackWinPercent);

            string sanMove = ExtractSanMove(topEntry.MovesSan, moves.Count) ?? FormatUciToSanHint(nextMove);

            results.Add(new OpeningMoveOption
            {
                UciMove = nextMove,
                SanMove = sanMove,
                ContinuationName = topEntry.FullName,
                GamesCount = totalGames,
                WhiteWinPercent = Math.Round(avgWhiteWin, 1),
                DrawPercent = Math.Round(avgDraw, 1),
                BlackWinPercent = Math.Round(avgBlackWin, 1)
            });
        }

        return results.OrderByDescending(r => r.GamesCount).ToList();
    }

    private static string? ExtractSanMove(string movesSan, int moveIndex)
    {
        if (string.IsNullOrWhiteSpace(movesSan)) return null;
        var tokens = movesSan.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !char.IsDigit(t[0])) // filter out move numbers like "1.", "2."
            .ToList();

        return moveIndex >= 0 && moveIndex < tokens.Count ? tokens[moveIndex] : null;
    }

    private static string NormalizeFen(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen)) return string.Empty;
        var parts = fen.Trim().Split(' ');
        // Normalizes pieces + active color + castling
        return parts.Length >= 4 ? $"{parts[0]} {parts[1]} {parts[2]}" : fen.Trim();
    }

    private static string FormatUciToSanHint(string uci)
    {
        if (uci.Length >= 4)
        {
            return uci[2..4];
        }
        return uci;
    }
}
