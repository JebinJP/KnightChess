using System.Text.Json;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Models;
using KnightLabChess.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnightLabChess.Infrastructure.Tests;

public class SqliteGameRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseManager _dbManager;
    private readonly SqliteGameRepository _repository;

    public SqliteGameRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"knightlab_test_{Guid.NewGuid():N}.db");
        _dbManager = new DatabaseManager(_dbPath, NullLogger<DatabaseManager>.Instance);
        _dbManager.InitializeAsync().GetAwaiter().GetResult();
        _repository = new SqliteGameRepository(_dbManager, NullLogger<SqliteGameRepository>.Instance);
    }

    [Fact]
    public async Task SaveGameAsync_InsertsRecordAndReturnsGeneratedId()
    {
        var record = new GameRecord
        {
            WhitePlayer = "Magnus",
            BlackPlayer = "Hikaru",
            Result = GameResult.InProgress,
            StartFen = GeraChessGameAdapter.DefaultStartingFen,
            CurrentFen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            MoveHistoryJson = JsonSerializer.Serialize(new[] { "e2e4" }),
            MoveCount = 1
        };

        int id = await _repository.SaveGameAsync(record);

        Assert.True(id > 0);
        var retrieved = await _repository.GetGameAsync(id);
        Assert.NotNull(retrieved);
        Assert.Equal("Magnus", retrieved.WhitePlayer);
        Assert.Equal("Hikaru", retrieved.BlackPlayer);
        Assert.Equal(record.CurrentFen, retrieved.CurrentFen);
        Assert.Equal(record.MoveHistoryJson, retrieved.MoveHistoryJson);
    }

    [Fact]
    public async Task SaveAndReloadGame_ReconstitutesReplayableBoardStateAndUndoStack()
    {
        // 1. Play moves on live adapter: 1. e4 e5 2. Nf3 Nc6 3. Bb5
        var game = new GeraChessGameAdapter();
        game.MakeMove("e4");
        game.MakeMove("e5");
        game.MakeMove("Nf3");
        game.MakeMove("Nc6");
        game.MakeMove("Bb5");

        var record = new GameRecord
        {
            WhitePlayer = "Player1",
            BlackPlayer = "Player2",
            Result = game.Result,
            StartFen = game.StartFen,
            CurrentFen = game.CurrentFen,
            Pgn = game.GetPgn("Player1", "Player2"),
            MoveHistoryJson = JsonSerializer.Serialize(game.MoveHistoryUci),
            MoveCount = game.MoveHistory.Count
        };

        // 2. Persist to SQLite
        int gameId = await _repository.SaveGameAsync(record);
        Assert.True(gameId > 0);

        // 3. Load from SQLite
        var savedRecord = await _repository.GetGameAsync(gameId);
        Assert.NotNull(savedRecord);

        // 4. Reconstitute game state using StartFen and MoveHistoryJson
        var moves = JsonSerializer.Deserialize<List<string>>(savedRecord.MoveHistoryJson);
        Assert.NotNull(moves);
        Assert.Equal(5, moves.Count);

        var restoredGame = new GeraChessGameAdapter();
        restoredGame.LoadFromPosition(savedRecord.StartFen, moves);

        // Assert restored game is in identical state
        Assert.Equal(game.CurrentFen, restoredGame.CurrentFen);
        Assert.Equal(game.ActiveTurn, restoredGame.ActiveTurn);
        Assert.Equal(5, restoredGame.MoveHistory.Count);
        Assert.Equal("Bb5", restoredGame.MoveHistory[^1].San);

        // 5. Verify that player can continue playing live from the reloaded position!
        var continuationMove = restoredGame.MakeMove(BoardSquare.FromAlgebraic("a7"), BoardSquare.FromAlgebraic("a6")); // 3... a6
        Assert.True(continuationMove.Success);
        Assert.Equal(6, restoredGame.MoveHistory.Count);
        Assert.Equal("a6", restoredGame.MoveHistory[^1].San);

        // 6. Verify that undo stack works properly on restored game
        bool undone = restoredGame.UndoLastMove(); // undo 3... a6
        Assert.True(undone);
        Assert.Equal(5, restoredGame.MoveHistory.Count);
        Assert.Equal(PieceColor.Black, restoredGame.ActiveTurn);
    }

    [Fact]
    public async Task GetRecentGamesAsync_ReturnsOrderedList()
    {
        for (int i = 1; i <= 5; i++)
        {
            await _repository.SaveGameAsync(new GameRecord
            {
                WhitePlayer = $"White_{i}",
                BlackPlayer = $"Black_{i}",
                StartFen = GeraChessGameAdapter.DefaultStartingFen,
                CurrentFen = GeraChessGameAdapter.DefaultStartingFen
            });
        }

        var list = await _repository.GetRecentGamesAsync(limit: 3);
        Assert.Equal(3, list.Count);
        Assert.Equal("White_5", list[0].WhitePlayer);
        Assert.Equal("White_4", list[1].WhitePlayer);
        Assert.Equal("White_3", list[2].WhitePlayer);
    }

    public void Dispose()
    {
        _dbManager.Dispose();
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore temp file cleanup exceptions
        }
    }
}
