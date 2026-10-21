using KnightLabChess.Core.Models;
using KnightLabChess.Core.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Persistence;

/// <summary>
/// SQLite implementation of IGameRepository for reliable game journal persistence.
/// </summary>
public class SqliteGameRepository : IGameRepository
{
    private readonly DatabaseManager _databaseManager;
    private readonly ILogger<SqliteGameRepository> _logger;

    public SqliteGameRepository(DatabaseManager databaseManager, ILogger<SqliteGameRepository> logger)
    {
        _databaseManager = databaseManager;
        _logger = logger;
    }

    public async Task<int> SaveGameAsync(GameRecord game)
    {
        var conn = _databaseManager.GetConnection();

        if (game.Id > 0)
        {
            const string updateSql = """
                UPDATE GameRecords SET
                    WhitePlayer = @WhitePlayer,
                    BlackPlayer = @BlackPlayer,
                    Result = @Result,
                    EndReason = @EndReason,
                    Pgn = @Pgn,
                    StartFen = @StartFen,
                    CurrentFen = @CurrentFen,
                    MoveHistoryJson = @MoveHistoryJson,
                    CompletedAt = @CompletedAt,
                    MoveCount = @MoveCount,
                    DifficultyLevel = @DifficultyLevel
                WHERE Id = @Id;
                """;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = updateSql;
            cmd.Parameters.AddWithValue("@Id", game.Id);
            BindParameters(cmd, game);
            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Updated game record with ID {Id}", game.Id);
            return game.Id;
        }
        else
        {
            const string insertSql = """
                INSERT INTO GameRecords (
                    WhitePlayer, BlackPlayer, Result, EndReason, Pgn, StartFen,
                    CurrentFen, MoveHistoryJson, CreatedAt, CompletedAt, MoveCount, DifficultyLevel
                ) VALUES (
                    @WhitePlayer, @BlackPlayer, @Result, @EndReason, @Pgn, @StartFen,
                    @CurrentFen, @MoveHistoryJson, @CreatedAt, @CompletedAt, @MoveCount, @DifficultyLevel
                );
                SELECT last_insert_rowid();
                """;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = insertSql;
            BindParameters(cmd, game);
            cmd.Parameters.AddWithValue("@CreatedAt", game.CreatedAt.ToString("o"));

            var rowId = await cmd.ExecuteScalarAsync();
            game.Id = Convert.ToInt32(rowId);
            _logger.LogInformation("Saved new game record with ID {Id}", game.Id);
            return game.Id;
        }
    }

    public async Task<GameRecord?> GetGameAsync(int id)
    {
        var conn = _databaseManager.GetConnection();
        const string sql = """
            SELECT Id, WhitePlayer, BlackPlayer, Result, EndReason, Pgn, StartFen,
                   CurrentFen, MoveHistoryJson, CreatedAt, CompletedAt, MoveCount, DifficultyLevel
            FROM GameRecords
            WHERE Id = @Id;
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapReaderToGameRecord(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<GameRecord>> GetRecentGamesAsync(int limit = 20)
    {
        var conn = _databaseManager.GetConnection();
        const string sql = """
            SELECT Id, WhitePlayer, BlackPlayer, Result, EndReason, Pgn, StartFen,
                   CurrentFen, MoveHistoryJson, CreatedAt, CompletedAt, MoveCount, DifficultyLevel
            FROM GameRecords
            ORDER BY Id DESC
            LIMIT @Limit;
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Limit", limit);

        var list = new List<GameRecord>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapReaderToGameRecord(reader));
        }

        return list.AsReadOnly();
    }

    public async Task DeleteGameAsync(int id)
    {
        var conn = _databaseManager.GetConnection();
        const string sql = "DELETE FROM GameRecords WHERE Id = @Id;";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("Deleted game record with ID {Id}", id);
    }

    private static void BindParameters(SqliteCommand cmd, GameRecord game)
    {
        cmd.Parameters.AddWithValue("@WhitePlayer", game.WhitePlayer);
        cmd.Parameters.AddWithValue("@BlackPlayer", game.BlackPlayer);
        cmd.Parameters.AddWithValue("@Result", (int)game.Result);
        cmd.Parameters.AddWithValue("@EndReason", (int)game.EndReason);
        cmd.Parameters.AddWithValue("@Pgn", game.Pgn);
        cmd.Parameters.AddWithValue("@StartFen", game.StartFen);
        cmd.Parameters.AddWithValue("@CurrentFen", game.CurrentFen);
        cmd.Parameters.AddWithValue("@MoveHistoryJson", game.MoveHistoryJson);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)game.CompletedAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MoveCount", game.MoveCount);
        cmd.Parameters.AddWithValue("@DifficultyLevel", (object?)game.DifficultyLevel ?? DBNull.Value);
    }

    private static GameRecord MapReaderToGameRecord(SqliteDataReader reader)
    {
        return new GameRecord
        {
            Id = reader.GetInt32(0),
            WhitePlayer = reader.GetString(1),
            BlackPlayer = reader.GetString(2),
            Result = (GameResult)reader.GetInt32(3),
            EndReason = (GameEndReason)reader.GetInt32(4),
            Pgn = reader.GetString(5),
            StartFen = reader.GetString(6),
            CurrentFen = reader.GetString(7),
            MoveHistoryJson = reader.GetString(8),
            CreatedAt = DateTime.Parse(reader.GetString(9)),
            CompletedAt = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10)),
            MoveCount = reader.GetInt32(11),
            DifficultyLevel = reader.IsDBNull(12) ? null : reader.GetString(12)
        };
    }
}
