using KnightLabChess.Core.Models;
using KnightLabChess.Core.Progress;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Persistence;

/// <summary>
/// SQLite implementation of the progress service.
/// </summary>
public class SqliteProgressService : IProgressService
{
    private readonly DatabaseManager _db;
    private readonly ILogger<SqliteProgressService> _logger;

    public SqliteProgressService(DatabaseManager db, ILogger<SqliteProgressService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LessonProgress?> GetLessonProgressAsync(string lessonId)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM LessonProgress WHERE LessonId = @LessonId";
        cmd.Parameters.AddWithValue("@LessonId", lessonId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadLessonProgress(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<LessonProgress>> GetAllLessonProgressAsync()
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM LessonProgress ORDER BY LastAccessedAt DESC";

        var results = new List<LessonProgress>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadLessonProgress(reader));
        }
        return results;
    }

    public async Task SaveLessonProgressAsync(LessonProgress progress)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO LessonProgress (LessonId, IsCompleted, CurrentStepIndex, TotalSteps, 
                CorrectAnswers, TotalAttempts, MasteryScore, StartedAt, CompletedAt, LastAccessedAt)
            VALUES (@LessonId, @IsCompleted, @CurrentStepIndex, @TotalSteps, 
                @CorrectAnswers, @TotalAttempts, @MasteryScore, @StartedAt, @CompletedAt, @LastAccessedAt)
            ON CONFLICT(LessonId) DO UPDATE SET
                IsCompleted = @IsCompleted,
                CurrentStepIndex = @CurrentStepIndex,
                TotalSteps = @TotalSteps,
                CorrectAnswers = @CorrectAnswers,
                TotalAttempts = @TotalAttempts,
                MasteryScore = @MasteryScore,
                StartedAt = COALESCE(LessonProgress.StartedAt, @StartedAt),
                CompletedAt = @CompletedAt,
                LastAccessedAt = @LastAccessedAt
            """;

        cmd.Parameters.AddWithValue("@LessonId", progress.LessonId);
        cmd.Parameters.AddWithValue("@IsCompleted", progress.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@CurrentStepIndex", progress.CurrentStepIndex);
        cmd.Parameters.AddWithValue("@TotalSteps", progress.TotalSteps);
        cmd.Parameters.AddWithValue("@CorrectAnswers", progress.CorrectAnswers);
        cmd.Parameters.AddWithValue("@TotalAttempts", progress.TotalAttempts);
        cmd.Parameters.AddWithValue("@MasteryScore", progress.MasteryScore);
        cmd.Parameters.AddWithValue("@StartedAt", progress.StartedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedAt", progress.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@LastAccessedAt", progress.LastAccessedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
        _logger.LogDebug("Saved lesson progress for {LessonId}", progress.LessonId);
    }

    public async Task<PuzzleProgress?> GetPuzzleProgressAsync(string puzzleId)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM PuzzleProgress WHERE PuzzleId = @PuzzleId";
        cmd.Parameters.AddWithValue("@PuzzleId", puzzleId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadPuzzleProgress(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<PuzzleProgress>> GetAllPuzzleProgressAsync()
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM PuzzleProgress ORDER BY LastAttemptAt DESC";

        var results = new List<PuzzleProgress>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadPuzzleProgress(reader));
        }
        return results;
    }

    public async Task SavePuzzleProgressAsync(PuzzleProgress progress)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO PuzzleProgress (PuzzleId, IsSolved, Attempts, UsedHint, SolvedAt, LastAttemptAt)
            VALUES (@PuzzleId, @IsSolved, @Attempts, @UsedHint, @SolvedAt, @LastAttemptAt)
            ON CONFLICT(PuzzleId) DO UPDATE SET
                IsSolved = @IsSolved,
                Attempts = @Attempts,
                UsedHint = @UsedHint,
                SolvedAt = @SolvedAt,
                LastAttemptAt = @LastAttemptAt
            """;

        cmd.Parameters.AddWithValue("@PuzzleId", progress.PuzzleId);
        cmd.Parameters.AddWithValue("@IsSolved", progress.IsSolved ? 1 : 0);
        cmd.Parameters.AddWithValue("@Attempts", progress.Attempts);
        cmd.Parameters.AddWithValue("@UsedHint", progress.UsedHint ? 1 : 0);
        cmd.Parameters.AddWithValue("@SolvedAt", progress.SolvedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@LastAttemptAt", progress.LastAttemptAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveGameRecordAsync(GameRecord record)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO GameRecords (WhitePlayer, BlackPlayer, Result, EndReason, Pgn, 
                StartFen, CreatedAt, CompletedAt, MoveCount, DifficultyLevel)
            VALUES (@WhitePlayer, @BlackPlayer, @Result, @EndReason, @Pgn,
                @StartFen, @CreatedAt, @CompletedAt, @MoveCount, @DifficultyLevel)
            """;

        cmd.Parameters.AddWithValue("@WhitePlayer", record.WhitePlayer);
        cmd.Parameters.AddWithValue("@BlackPlayer", record.BlackPlayer);
        cmd.Parameters.AddWithValue("@Result", (int)record.Result);
        cmd.Parameters.AddWithValue("@EndReason", (int)record.EndReason);
        cmd.Parameters.AddWithValue("@Pgn", record.Pgn);
        cmd.Parameters.AddWithValue("@StartFen", record.StartFen);
        cmd.Parameters.AddWithValue("@CreatedAt", record.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CompletedAt", record.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@MoveCount", record.MoveCount);
        cmd.Parameters.AddWithValue("@DifficultyLevel", record.DifficultyLevel ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
        _logger.LogDebug("Saved game record");
    }

    public async Task<IReadOnlyList<GameRecord>> GetRecentGamesAsync(int count = 10)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM GameRecords ORDER BY CreatedAt DESC LIMIT @Count";
        cmd.Parameters.AddWithValue("@Count", count);

        var results = new List<GameRecord>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new GameRecord
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                WhitePlayer = reader.GetString(reader.GetOrdinal("WhitePlayer")),
                BlackPlayer = reader.GetString(reader.GetOrdinal("BlackPlayer")),
                Result = (GameResult)reader.GetInt32(reader.GetOrdinal("Result")),
                EndReason = (GameEndReason)reader.GetInt32(reader.GetOrdinal("EndReason")),
                Pgn = reader.GetString(reader.GetOrdinal("Pgn")),
                StartFen = reader.GetString(reader.GetOrdinal("StartFen")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt"))
                    ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletedAt"))),
                MoveCount = reader.GetInt32(reader.GetOrdinal("MoveCount")),
                DifficultyLevel = reader.IsDBNull(reader.GetOrdinal("DifficultyLevel"))
                    ? null : reader.GetString(reader.GetOrdinal("DifficultyLevel"))
            });
        }
        return results;
    }

    public async Task<UserStats> GetUserStatsAsync()
    {
        var conn = _db.GetConnection();
        var stats = new UserStats();

        // Lesson stats
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM LessonProgress WHERE IsCompleted = 1";
            stats.TotalLessonsCompleted = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // Puzzle stats
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT 
                    COUNT(*) as Total,
                    COALESCE(SUM(CASE WHEN IsSolved = 1 THEN 1 ELSE 0 END), 0) as Solved
                FROM PuzzleProgress
                """;
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                stats.TotalPuzzlesAttempted = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                stats.TotalPuzzlesSolved = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                stats.PuzzleAccuracy = stats.TotalPuzzlesAttempted > 0
                    ? (double)stats.TotalPuzzlesSolved / stats.TotalPuzzlesAttempted * 100
                    : 0;
            }
        }

        // Game stats
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT 
                    COUNT(*) as Total,
                    COALESCE(SUM(CASE WHEN Result = 1 THEN 1 ELSE 0 END), 0) as Won,
                    COALESCE(SUM(CASE WHEN Result = 2 THEN 1 ELSE 0 END), 0) as Lost,
                    COALESCE(SUM(CASE WHEN Result = 3 THEN 1 ELSE 0 END), 0) as Drawn
                FROM GameRecords
                """;
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                stats.TotalGamesPlayed = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                stats.GamesWon = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                stats.GamesLost = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                stats.GamesDrawn = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            }
        }

        return stats;
    }

    private static LessonProgress ReadLessonProgress(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        LessonId = reader.GetString(reader.GetOrdinal("LessonId")),
        IsCompleted = reader.GetInt32(reader.GetOrdinal("IsCompleted")) == 1,
        CurrentStepIndex = reader.GetInt32(reader.GetOrdinal("CurrentStepIndex")),
        TotalSteps = reader.GetInt32(reader.GetOrdinal("TotalSteps")),
        CorrectAnswers = reader.GetInt32(reader.GetOrdinal("CorrectAnswers")),
        TotalAttempts = reader.GetInt32(reader.GetOrdinal("TotalAttempts")),
        MasteryScore = reader.GetDouble(reader.GetOrdinal("MasteryScore")),
        StartedAt = reader.IsDBNull(reader.GetOrdinal("StartedAt"))
            ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("StartedAt"))),
        CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt"))
            ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletedAt"))),
        LastAccessedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastAccessedAt")))
    };

    private static PuzzleProgress ReadPuzzleProgress(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        PuzzleId = reader.GetString(reader.GetOrdinal("PuzzleId")),
        IsSolved = reader.GetInt32(reader.GetOrdinal("IsSolved")) == 1,
        Attempts = reader.GetInt32(reader.GetOrdinal("Attempts")),
        UsedHint = reader.GetInt32(reader.GetOrdinal("UsedHint")) == 1,
        SolvedAt = reader.IsDBNull(reader.GetOrdinal("SolvedAt"))
            ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("SolvedAt"))),
        LastAttemptAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastAttemptAt")))
    };
}
