using KnightLabChess.Core.Engine;
using KnightLabChess.Infrastructure.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnightLabChess.Infrastructure.Tests;

public class UciEngineTests : IDisposable
{
    private readonly UciStockfishEngine _engine;

    public UciEngineTests()
    {
        _engine = new UciStockfishEngine(NullLogger<UciStockfishEngine>.Instance);
        _engine.InitializeAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetBestMoveAsync_StartingPosition_ReturnsValidUciMove()
    {
        const string startFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var move = await _engine.GetBestMoveAsync(startFen, EngineDifficulty.Casual);

        Assert.NotNull(move);
        Assert.InRange(move.Length, 4, 5);
        // Should be valid squares: e.g., e2e4, d2d4, g1f3, etc.
        Assert.InRange(move[0], 'a', 'h');
        Assert.InRange(move[1], '1', '8');
        Assert.InRange(move[2], 'a', 'h');
        Assert.InRange(move[3], '1', '8');
    }

    [Fact]
    public async Task GetBestMoveAsync_BlackToMove_ReturnsValidBlackMove()
    {
        const string fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";

        var move = await _engine.GetBestMoveAsync(fen, EngineDifficulty.Intermediate);

        Assert.NotNull(move);
        Assert.InRange(move.Length, 4, 5);
        Assert.InRange(move[0], 'a', 'h');
        Assert.InRange(move[1], '1', '8');
    }

    [Fact]
    public async Task EvaluatePositionAsync_StartingPosition_ReturnsSensibleEvaluation()
    {
        const string startFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var eval = await _engine.EvaluatePositionAsync(startFen);

        Assert.NotNull(eval);
        // Start position material balance is 0
        Assert.True(eval.ScoreCentipawns.HasValue || eval.MateInMoves.HasValue);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
