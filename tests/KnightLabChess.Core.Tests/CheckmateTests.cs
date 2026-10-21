using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Models;
using Xunit;

namespace KnightLabChess.Core.Tests;

public class CheckmateTests
{
    [Fact]
    public void ScholarsMate_EndsGameWithWhiteVictoryByCheckmate()
    {
        var game = new GeraChessGameAdapter();

        // 1. e4 e5
        game.MakeMove("e4");
        game.MakeMove("e5");

        // 2. Qh5 Nc6
        game.MakeMove("Qh5");
        game.MakeMove("Nc6");

        // 3. Bc4 Nf6
        game.MakeMove("Bc4");
        game.MakeMove("Nf6");

        // 4. Qxf7#
        var mateMove = game.MakeMove("Qxf7#");

        Assert.True(mateMove.Success);
        Assert.True(mateMove.IsCheckmate);
        Assert.True(game.IsGameOver);
        Assert.Equal(GameResult.WhiteWins, game.Result);
        Assert.Equal(GameEndReason.Checkmate, game.EndReason);
        Assert.Equal("White wins by checkmate!", game.GameEndMessage);
    }

    [Fact]
    public void FoolsMate_EndsGameWithBlackVictoryByCheckmate()
    {
        var game = new GeraChessGameAdapter();

        // 1. f3 e5
        game.MakeMove("f3");
        game.MakeMove("e5");

        // 2. g4 Qh4#
        game.MakeMove("g4");
        var mateMove = game.MakeMove("Qh4#");

        Assert.True(mateMove.Success);
        Assert.True(mateMove.IsCheckmate);
        Assert.True(game.IsGameOver);
        Assert.Equal(GameResult.BlackWins, game.Result);
        Assert.Equal(GameEndReason.Checkmate, game.EndReason);
        Assert.Equal("Black wins by checkmate!", game.GameEndMessage);
    }
}
