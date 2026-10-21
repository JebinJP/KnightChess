using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Models;
using Xunit;

namespace KnightLabChess.Core.Tests;

public class MoveValidationTests
{
    [Fact]
    public void StandardOpeningMove_ExecutesSuccessfullyAndUpdatesState()
    {
        var game = new GeraChessGameAdapter();
        Assert.Equal(PieceColor.White, game.ActiveTurn);
        Assert.True(game.IsWhiteTurn);

        var result = game.MakeMove(BoardSquare.FromAlgebraic("e2"), BoardSquare.FromAlgebraic("e4"));

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(PieceColor.Black, game.ActiveTurn);
        Assert.Single(game.MoveHistory);
        Assert.Equal("e4", game.MoveHistory[0].San);
        Assert.Equal("e2e4", game.MoveHistoryUci[0]);
    }

    [Fact]
    public void IllegalMove_RejectedWithoutMutatingGameState()
    {
        var game = new GeraChessGameAdapter();
        var initialFen = game.CurrentFen;
        var initialTurn = game.ActiveTurn;

        // Try illegal pawn move e2 to e5
        var result = game.MakeMove(BoardSquare.FromAlgebraic("e2"), BoardSquare.FromAlgebraic("e5"));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        // Verify state is strictly unmutated
        Assert.Equal(initialFen, game.CurrentFen);
        Assert.Equal(initialTurn, game.ActiveTurn);
        Assert.Empty(game.MoveHistory);
        Assert.Empty(game.MoveHistoryUci);
    }

    [Fact]
    public void PinnedPiece_CannotMoveOffPinLine()
    {
        // White King on e1, White Knight on e2, Black Rook on e8, Black King on h8 -> Knight is absolutely pinned along the e-file
        const string pinFen = "4r2k/8/8/8/8/8/4N3/4K3 w - - 0 1";
        var game = new GeraChessGameAdapter(pinFen);

        var knightSquare = BoardSquare.FromAlgebraic("e2");
        var legalDests = game.GetLegalDestinations(knightSquare);

        // Knight has no legal moves because any move exposes the King to the Rook
        Assert.Empty(legalDests);

        var illegalMove = game.MakeMove(knightSquare, BoardSquare.FromAlgebraic("d4"));
        Assert.False(illegalMove.Success);
        Assert.Equal(pinFen, game.CurrentFen);
    }

    [Fact]
    public void King_CannotWalkIntoCheck()
    {
        // White King on e1, Black Rook on d8, Black King on h8 -> King cannot walk onto d1 or d2
        const string fen = "3r3k/8/8/8/8/8/8/4K3 w - - 0 1";
        var game = new GeraChessGameAdapter(fen);

        var kingSquare = BoardSquare.FromAlgebraic("e1");
        var dests = game.GetLegalDestinations(kingSquare);

        Assert.DoesNotContain(BoardSquare.FromAlgebraic("d1"), dests);
        Assert.DoesNotContain(BoardSquare.FromAlgebraic("d2"), dests);

        var moveResult = game.MakeMove(kingSquare, BoardSquare.FromAlgebraic("d1"));
        Assert.False(moveResult.Success);
        Assert.Equal(fen, game.CurrentFen);
    }

    [Fact]
    public void InCheck_MustResolveCheckByEvadingBlockingOrCapturing()
    {
        // Position: White King on e1, Black Rook on e8, Black King on h8, White Bishop on f1
        const string checkFen = "4r2k/8/8/8/8/8/8/4KB2 w - - 0 1";
        var game = new GeraChessGameAdapter(checkFen);

        Assert.True(game.IsInCheck);

        // Moving King to d2 should resolve check
        var evade = game.MakeMove(BoardSquare.FromAlgebraic("e1"), BoardSquare.FromAlgebraic("d2"));
        Assert.True(evade.Success);
        Assert.False(game.IsInCheck);
    }
}
