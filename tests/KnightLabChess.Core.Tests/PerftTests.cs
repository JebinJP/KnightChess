using KnightLabChess.Core.Chess;
using Xunit;
using ExternalChess = global::Chess;

namespace KnightLabChess.Core.Tests;

public class PerftTests
{
    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 400)]
    [InlineData(3, 8902)]
    public void StartingPosition_PerftNodeCountsMatchAuthoritativeStandards(int depth, long expectedNodes)
    {
        var board = new ExternalChess.ChessBoard();
        long actualNodes = ComputePerft(board, depth);
        Assert.Equal(expectedNodes, actualNodes);
    }

    [Theory]
    [InlineData(1, 48)]
    [InlineData(2, 2039)]
    [InlineData(3, 97862)]
    public void KiwipetePosition_PerftNodeCountsMatchComplexPositionStandards(int depth, long expectedNodes)
    {
        // Kiwipete position stresses pins, castling, en passant, and double checks
        const string kiwipeteFen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        var board = ExternalChess.ChessBoard.LoadFromFen(kiwipeteFen);
        long actualNodes = ComputePerft(board, depth);
        Assert.Equal(expectedNodes, actualNodes);
    }

    private static long ComputePerft(ExternalChess.ChessBoard board, int depth)
    {
        if (depth == 0) return 1;
        var moves = board.Moves();
        if (depth == 1) return moves.Length;

        long nodes = 0;
        foreach (var move in moves)
        {
            board.Move(move);
            nodes += ComputePerft(board, depth - 1);
            board.Cancel();
        }
        return nodes;
    }
}
