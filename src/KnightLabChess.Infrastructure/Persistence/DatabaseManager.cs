using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Persistence;

/// <summary>
/// Manages the SQLite database lifecycle and schema migrations.
/// </summary>
public class DatabaseManager : IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseManager> _logger;
    private SqliteConnection? _connection;

    private const int CurrentSchemaVersion = 2;

    public DatabaseManager(string databasePath, ILogger<DatabaseManager> logger)
    {
        _connectionString = $"Data Source={databasePath}";
        _logger = logger;
    }

    public SqliteConnection GetConnection()
    {
        if (_connection is null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
        }
        return _connection;
    }

    /// <summary>
    /// Initializes the database, creating tables and running migrations.
    /// </summary>
    public async Task InitializeAsync()
    {
        var conn = GetConnection();
        _logger.LogInformation("Initializing database...");

        // Enable WAL mode for better concurrent read performance
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            await cmd.ExecuteNonQueryAsync();
        }

        var currentVersion = await GetSchemaVersionAsync(conn);
        if (currentVersion < CurrentSchemaVersion)
        {
            await RunMigrationsAsync(conn, currentVersion);
        }

        _logger.LogInformation("Database initialized at schema version {Version}", CurrentSchemaVersion);
    }

    private static async Task<int> GetSchemaVersionAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task RunMigrationsAsync(SqliteConnection conn, int fromVersion)
    {
        _logger.LogInformation("Running migrations from version {From} to {To}", fromVersion, CurrentSchemaVersion);

        if (fromVersion < 1)
        {
            await RunMigrationV1Async(conn);
        }

        if (fromVersion < 2)
        {
            await RunMigrationV2Async(conn);
        }
    }

    private static async Task RunMigrationV1Async(SqliteConnection conn)
    {
        var sql = """
            CREATE TABLE IF NOT EXISTS LessonProgress (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LessonId TEXT NOT NULL UNIQUE,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                CurrentStepIndex INTEGER NOT NULL DEFAULT 0,
                TotalSteps INTEGER NOT NULL DEFAULT 0,
                CorrectAnswers INTEGER NOT NULL DEFAULT 0,
                TotalAttempts INTEGER NOT NULL DEFAULT 0,
                MasteryScore REAL NOT NULL DEFAULT 0.0,
                StartedAt TEXT,
                CompletedAt TEXT,
                LastAccessedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PuzzleProgress (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PuzzleId TEXT NOT NULL UNIQUE,
                IsSolved INTEGER NOT NULL DEFAULT 0,
                Attempts INTEGER NOT NULL DEFAULT 0,
                UsedHint INTEGER NOT NULL DEFAULT 0,
                SolvedAt TEXT,
                LastAttemptAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS GameRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                WhitePlayer TEXT NOT NULL,
                BlackPlayer TEXT NOT NULL,
                Result INTEGER NOT NULL DEFAULT 0,
                EndReason INTEGER NOT NULL DEFAULT 0,
                Pgn TEXT NOT NULL DEFAULT '',
                StartFen TEXT NOT NULL,
                CurrentFen TEXT NOT NULL DEFAULT '',
                MoveHistoryJson TEXT NOT NULL DEFAULT '[]',
                CreatedAt TEXT NOT NULL,
                CompletedAt TEXT,
                MoveCount INTEGER NOT NULL DEFAULT 0,
                DifficultyLevel TEXT
            );

            CREATE INDEX IF NOT EXISTS IX_LessonProgress_LessonId ON LessonProgress(LessonId);
            CREATE INDEX IF NOT EXISTS IX_PuzzleProgress_PuzzleId ON PuzzleProgress(PuzzleId);
            CREATE INDEX IF NOT EXISTS IX_GameRecords_CreatedAt ON GameRecords(CreatedAt);

            PRAGMA user_version = 1;
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task RunMigrationV2Async(SqliteConnection conn)
    {
        var sql = """
            ALTER TABLE GameRecords ADD COLUMN CurrentFen TEXT NOT NULL DEFAULT '';
            ALTER TABLE GameRecords ADD COLUMN MoveHistoryJson TEXT NOT NULL DEFAULT '[]';
            PRAGMA user_version = 2;
            """;

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column"))
        {
            // Columns already exist if created fresh via V1 with CurrentFen
            await using var updateVerCmd = conn.CreateCommand();
            updateVerCmd.CommandText = "PRAGMA user_version = 2;";
            await updateVerCmd.ExecuteNonQueryAsync();
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
