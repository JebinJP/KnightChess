using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Models;
using Xunit;

namespace KnightLabChess.Core.Tests;

public class FenPgnTests
{
    [Fact]
    public void StandardFen_RoundtripPreservesPositionAndTurn()
    {
        var game = new GeraChessGameAdapter();
        Assert.Equal(GeraChessGameAdapter.DefaultStartingFen, game.CurrentFen);

        game.MakeMove("e4");
        Assert.Equal("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", game.CurrentFen);

        // Load into new adapter and verify identical
        var reloaded = new GeraChessGameAdapter(game.CurrentFen);
        Assert.Equal(game.CurrentFen, reloaded.CurrentFen);
        Assert.Equal(PieceColor.Black, reloaded.ActiveTurn);
    }

    [Fact]
    public void PgnGeneration_ProducesStandardNotationWithHeaders()
    {
        var game = new GeraChessGameAdapter();
        game.MakeMove("e4");
        game.MakeMove("e5");
        game.MakeMove("Nf3");
        game.MakeMove("Nc6");

        var pgn = game.GetPgn("Alice", "Bob");

        Assert.Contains("[White \"Alice\"]", pgn);
        Assert.Contains("[Black \"Bob\"]", pgn);
        Assert.Contains("1. e4 e5 2. Nf3 Nc6", pgn);
    }

    [Fact]
    public void LoadFromPosition_RestoresFullMoveHistoryAndUndoStack()
    {
        var originalGame = new GeraChessGameAdapter();
        originalGame.MakeMove("e4");
        originalGame.MakeMove("e5");
        originalGame.MakeMove("Nf3");

        var uciMoves = originalGame.MoveHistoryUci.ToList();
        var startFen = originalGame.StartFen;

        var reloadedGame = new GeraChessGameAdapter();
        reloadedGame.LoadFromPosition(startFen, uciMoves);

        Assert.Equal(originalGame.CurrentFen, reloadedGame.CurrentFen);
        Assert.Equal(originalGame.ActiveTurn, reloadedGame.ActiveTurn);
        Assert.Equal(3, reloadedGame.MoveHistory.Count);
        Assert.Equal("Nf3", reloadedGame.MoveHistory[2].San);

        // Verify undo stack works on reloaded game!
        bool undone = reloadedGame.UndoLastMove();
        Assert.True(undone);
        Assert.Equal(2, reloadedGame.MoveHistory.Count);
        Assert.Equal(PieceColor.White, reloadedGame.ActiveTurn);
    }
}
