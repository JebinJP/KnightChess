using KnightLabChess.Core.Engine;
using KnightLabChess.Infrastructure.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnightLabChess.Infrastructure.Tests;

public class FallbackTacticalEngineTests
{
    [Fact]
    public async Task GetBestMoveAsync_InStartingPosition_ReturnsLegalMove()
    {
        using var engine = new UciStockfishEngine(NullLogger<UciStockfishEngine>.Instance);
        const string startFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var move = await engine.GetBestMoveAsync(startFen, EngineDifficulty.Intermediate);

        Assert.NotNull(move);
        Assert.True(move.Length is 4 or 5);
        // Starting move should be a pawn or knight move (e.g. e2e4, d2d4, g1f3, b1c3)
        Assert.Contains(move, new[] { "e2e4", "d2d4", "g1f3", "b1c3", "c2c4", "e2e3", "d2d3", "f2f4" });
    }

    [Fact]
    public async Task GetBestMoveAsync_WhenHangingQueenAvailable_CapturesQueen()
    {
        using var engine = new UciStockfishEngine(NullLogger<UciStockfishEngine>.Instance);
        // White pawn on d4 can capture Black Queen on e5
        const string fen = "rnb1kbnr/pppp1ppp/8/4q3/3P4/8/PPP1PPPP/RNBQKBNR w KQkq - 0 1";

        var move = await engine.GetBestMoveAsync(fen, EngineDifficulty.Master);

        Assert.NotNull(move);
        // The master engine must capture the Queen: d4e5
        Assert.Equal("d4e5", move);
    }

    [Fact]
    public async Task EvaluatePositionAsync_Fallback_ReturnsReasonableEvaluation()
    {
        using var engine = new UciStockfishEngine(NullLogger<UciStockfishEngine>.Instance);
        const string startFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var eval = await engine.EvaluatePositionAsync(startFen);

        Assert.NotNull(eval);
        Assert.NotNull(eval.ScoreCentipawns);
        // Starting position should be balanced (near 0)
        Assert.InRange(eval.ScoreCentipawns.Value, -50, 50);
        Assert.False(string.IsNullOrEmpty(eval.BestMoveUci));
    }
}
