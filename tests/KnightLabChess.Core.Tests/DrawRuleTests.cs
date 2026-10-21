using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Models;
using Xunit;

namespace KnightLabChess.Core.Tests;

public class DrawRuleTests
{
    [Fact]
    public void ThreefoldRepetition_IsClaimableAndDoesNotEndGameUntilClaimed()
    {
        var game = new GeraChessGameAdapter();

        // Cycle knights back and forth 2 times to reach initial position 3 times
        game.MakeMove("Nf3"); game.MakeMove("Nf6");
        game.MakeMove("Ng1"); game.MakeMove("Ng8"); // Repetition 2

        Assert.Equal(ClaimableDrawType.None, game.AvailableClaimableDraw);
        Assert.False(game.CanClaimDraw);
        Assert.False(game.IsGameOver);

        game.MakeMove("Nf3"); game.MakeMove("Nf6");
        game.MakeMove("Ng1"); game.MakeMove("Ng8"); // Repetition 3!

        // Draw is now CLAIMABLE, but game is NOT terminated automatically!
        Assert.Equal(ClaimableDrawType.ThreefoldRepetition, game.AvailableClaimableDraw);
        Assert.True(game.CanClaimDraw);
        Assert.False(game.IsGameOver);
        Assert.Equal(GameResult.InProgress, game.Result);

        // Player claims draw
        bool claimed = game.ClaimDraw();

        Assert.True(claimed);
        Assert.True(game.IsGameOver);
        Assert.Equal(GameResult.Draw, game.Result);
        Assert.Equal(GameEndReason.ThreefoldRepetition, game.EndReason);
    }

    [Fact]
    public void FivefoldRepetition_ImmediatelyAndAutomaticallyDeclaresDraw()
    {
        var game = new GeraChessGameAdapter();

        // 4 cycles = 5 occurrences of initial position
        for (int i = 0; i < 4; i++)
        {
            game.MakeMove("Nf3"); game.MakeMove("Nf6");
            game.MakeMove("Ng1"); game.MakeMove("Ng8");
        }

        // Automatic draw triggered on 5th occurrence!
        Assert.True(game.IsGameOver);
        Assert.Equal(GameResult.Draw, game.Result);
        Assert.Equal(AutomaticDrawType.FivefoldRepetition, game.ActiveAutomaticDraw);
    }

    [Fact]
    public void FiftyMoveRule_IsClaimableAt100HalfMovesWithoutPawnMoveOrCapture()
    {
        // Setup position with halfmove clock at 99: White King on e1, Rook on h1, Black King on h8
        const string fen99 = "7k/8/8/8/8/8/8/4K2R w - - 99 50";
        var game = new GeraChessGameAdapter(fen99);

        Assert.Equal(ClaimableDrawType.None, game.AvailableClaimableDraw);

        // White moves rook from h1 to h2 (halfmove becomes 100)
        var result = game.MakeMove(BoardSquare.FromAlgebraic("h1"), BoardSquare.FromAlgebraic("h2"));

        Assert.True(result.Success);
        Assert.Equal(ClaimableDrawType.FiftyMoveRule, game.AvailableClaimableDraw);
        Assert.True(game.CanClaimDraw);
        Assert.False(game.IsGameOver);

        // Claim the draw
        game.ClaimDraw();
        Assert.True(game.IsGameOver);
        Assert.Equal(GameResult.Draw, game.Result);
        Assert.Equal(GameEndReason.FiftyMoveRule, game.EndReason);
    }

    [Fact]
    public void SeventyFiveMoveRule_AutomaticallyEndsGameAt150HalfMoves()
    {
        // Setup position with halfmove clock at 149: White King on e1, Rook on h1, Black King on h8
        const string fen149 = "7k/8/8/8/8/8/8/4K2R w - - 149 75";
        var game = new GeraChessGameAdapter(fen149);

        // White moves rook from h1 to h2 (halfmove becomes 150)
        var result = game.MakeMove(BoardSquare.FromAlgebraic("h1"), BoardSquare.FromAlgebraic("h2"));

        Assert.True(result.Success);
        Assert.True(game.IsGameOver);
        Assert.Equal(GameResult.Draw, game.Result);
        Assert.Equal(AutomaticDrawType.SeventyFiveMoveRule, game.ActiveAutomaticDraw);
    }

    [Fact]
    public void Stalemate_EndsGameImmediatelyAsDraw()
    {
        // Stalemate: Black King on h8, White King on f7, White Pawn on g6. Black to move, not in check, no legal moves.
        const string blackStalemate = "7k/5K2/6P1/8/8/8/8/8 b - - 0 1";
        var gameStalemate = new GeraChessGameAdapter(blackStalemate);

        Assert.True(gameStalemate.IsGameOver);
        Assert.Equal(GameResult.Draw, gameStalemate.Result);
        Assert.Equal(GameEndReason.Stalemate, gameStalemate.EndReason);
    }

    [Fact]
    public void InsufficientMaterial_EndsGameImmediatelyAsDraw()
    {
        // King vs King
        const string kvkFen = "8/8/8/4k3/8/8/8/4K3 w - - 0 1";
        var game = new GeraChessGameAdapter(kvkFen);

        Assert.True(game.IsGameOver);
        Assert.Equal(GameResult.Draw, game.Result);
        Assert.Equal(GameEndReason.InsufficientMaterial, game.EndReason);
    }
}
