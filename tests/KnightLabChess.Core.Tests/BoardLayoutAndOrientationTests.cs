using KnightLabChess.App.ViewModels;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Models;
using KnightLabChess.Core.Persistence;
using Xunit;

namespace KnightLabChess.Core.Tests;

public class BoardLayoutAndOrientationTests
{
    private class FakeGameRepository : IGameRepository
    {
        public Task<int> SaveGameAsync(GameRecord game) => Task.FromResult(1);
        public Task<GameRecord?> GetGameAsync(int id) => Task.FromResult<GameRecord?>(null);
        public Task<IReadOnlyList<GameRecord>> GetRecentGamesAsync(int limit = 20) => Task.FromResult<IReadOnlyList<GameRecord>>([]);
        public Task DeleteGameAsync(int id) => Task.CompletedTask;
    }

    private class FakeEngineService : IEngineService
    {
        public bool IsEngineAvailable => true;
        public string EngineName => "Stockfish";
        public Task<bool> InitializeAsync(string? executablePath = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string?> GetBestMoveAsync(string fen, EngineDifficulty difficulty = EngineDifficulty.Casual, int moveTimeMs = 800, CancellationToken cancellationToken = default) => Task.FromResult<string?>("e7e5");
        public Task<EngineEvaluation?> EvaluatePositionAsync(string fen, int depth = 12, int timeoutMs = 1500, CancellationToken cancellationToken = default) => Task.FromResult<EngineEvaluation?>(null);
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    [Fact]
    public void DefaultOrientation_WhiteAtBottom_BlackAtTop()
    {
        // Arrange
        var boardVm = new ChessBoardViewModel();

        // Assert
        Assert.False(boardVm.IsFlipped);

        // In 8x8 UniformGrid:
        // Row 0 is the top visual row, Row 7 is the bottom visual row.
        // Index = row * 8 + col.
        // White King on e1 (file 4, rank 0): must be at bottom row (row 7, col 4 -> index 60)
        var e1Square = boardVm.Squares[60];
        Assert.Equal(4, e1Square.File);
        Assert.Equal(0, e1Square.Rank);
        Assert.True(e1Square.HasPiece);
        Assert.True(e1Square.IsWhitePiece);
        Assert.Equal("WhiteKingDrawing", e1Square.PieceKey);

        // Black King on e8 (file 4, rank 7): must be at top row (row 0, col 4 -> index 4)
        var e8Square = boardVm.Squares[4];
        Assert.Equal(4, e8Square.File);
        Assert.Equal(7, e8Square.Rank);
        Assert.True(e8Square.HasPiece);
        Assert.False(e8Square.IsWhitePiece);
        Assert.Equal("BlackKingDrawing", e8Square.PieceKey);
    }

    [Fact]
    public void FlippedOrientation_BlackAtBottom_WhiteAtTop()
    {
        // Arrange
        var boardVm = new ChessBoardViewModel();

        // Act
        boardVm.FlipBoard();

        // Assert
        Assert.True(boardVm.IsFlipped);

        // When flipped:
        // Black King on e8 (file 4, rank 7): row = rank = 7 (bottom row), col = 7 - 4 = 3 -> index 59
        var bottomKingSquare = boardVm.Squares[59];
        Assert.Equal(4, bottomKingSquare.File);
        Assert.Equal(7, bottomKingSquare.Rank);
        Assert.True(bottomKingSquare.HasPiece);
        Assert.False(bottomKingSquare.IsWhitePiece);
        Assert.Equal("BlackKingDrawing", bottomKingSquare.PieceKey);

        // White King on e1 (file 4, rank 0): row = rank = 0 (top row), col = 7 - 4 = 3 -> index 3
        var topKingSquare = boardVm.Squares[3];
        Assert.Equal(4, topKingSquare.File);
        Assert.Equal(0, topKingSquare.Rank);
        Assert.True(topKingSquare.HasPiece);
        Assert.True(topKingSquare.IsWhitePiece);
        Assert.Equal("WhiteKingDrawing", topKingSquare.PieceKey);
    }

    [Fact]
    public void BoardCoordinates_CorrectlyAssignedToEdgeSquares()
    {
        // Arrange
        var boardVm = new ChessBoardViewModel();

        // When not flipped:
        // Leftmost column (col 0): has rank labels "8", "7", ..., "1" from top to bottom
        // Bottom row (row 7): has file labels "a", "b", ..., "h" from left to right

        // Top-left square: a8 (row 0, col 0 -> index 0)
        var a8Square = boardVm.Squares[0];
        Assert.True(a8Square.HasRankLabel);
        Assert.Equal("8", a8Square.RankLabel);
        Assert.False(a8Square.HasFileLabel);

        // Bottom-left square: a1 (row 7, col 0 -> index 56)
        var a1Square = boardVm.Squares[56];
        Assert.True(a1Square.HasRankLabel);
        Assert.Equal("1", a1Square.RankLabel);
        Assert.True(a1Square.HasFileLabel);
        Assert.Equal("a", a1Square.FileLabel);

        // Bottom-right square: h1 (row 7, col 7 -> index 63)
        var h1Square = boardVm.Squares[63];
        Assert.False(h1Square.HasRankLabel);
        Assert.True(h1Square.HasFileLabel);
        Assert.Equal("h", h1Square.FileLabel);

        // Internal square: d4 (row 4, col 3 -> index 35)
        var d4Square = boardVm.Squares[35];
        Assert.False(d4Square.HasRankLabel);
        Assert.False(d4Square.HasFileLabel);
    }

    [Fact]
    public void SquareClick_ExecutesMoveCorrectly_UnderVisualOrientation()
    {
        // Arrange
        var boardVm = new ChessBoardViewModel();

        // White selects e2 (index 52 in non-flipped grid: row 6, col 4)
        var e2 = boardVm.Squares[52];
        Assert.Equal(4, e2.File);
        Assert.Equal(1, e2.Rank);

        boardVm.SelectSquareCommand.Execute(e2);
        Assert.True(e2.IsSelected);

        // e4 is at row 4, col 4 (index 36)
        var e4 = boardVm.Squares[36];
        Assert.Equal(4, e4.File);
        Assert.Equal(3, e4.Rank);
        Assert.True(e4.IsLegalMove);

        // Click e4 to make move
        boardVm.SelectSquareCommand.Execute(e4);

        // Assert move executed
        Assert.False(e2.HasPiece);
        Assert.True(e4.HasPiece);
        Assert.Equal("WhitePawnDrawing", e4.PieceKey);
        Assert.False(boardVm.IsWhiteTurn);
    }

    [Fact]
    public void CapturedPiecesAndMaterialAdvantage_UpdateCorrectlyOnCapture()
    {
        // Arrange
        var boardVm = new ChessBoardViewModel();

        // 1. e4 d5 2. exd5
        boardVm.MakeMove("e2e4");
        boardVm.MakeMove("d7d5");
        boardVm.MakeMove("e4d5");

        // Assert White captured Black pawn
        Assert.Contains("♟", boardVm.CapturedBlackPieces);

        // When not flipped (White at bottom, Black at top):
        // Bottom player (White) has captured Black pawn
        Assert.Contains("♟", boardVm.BottomCapturedPieces);
        Assert.Empty(boardVm.TopCapturedPieces);

        // White material advantage = +1 pawn
        Assert.Equal("+1", boardVm.BottomMaterialAdvantage);
        Assert.Equal(string.Empty, boardVm.TopMaterialAdvantage);

        // Flipping board swaps perspective of top and bottom captured trays
        boardVm.FlipBoard();
        Assert.Contains("♟", boardVm.TopCapturedPieces);
        Assert.Empty(boardVm.BottomCapturedPieces);
        Assert.Equal("+1", boardVm.TopMaterialAdvantage);
        Assert.Equal(string.Empty, boardVm.BottomMaterialAdvantage);
    }

    [Fact]
    public void FlippingBoard_PreservesGameEngineStateAndMoveHistory()
    {
        // Arrange
        var boardVm = new ChessBoardViewModel();
        boardVm.MakeMove("e2e4");
        boardVm.MakeMove("e7e5");
        boardVm.MakeMove("g1f3");

        var fenBefore = boardVm.CurrentFen;
        var movesBefore = boardVm.MoveHistory.Count;

        // Act
        boardVm.FlipBoard();

        // Assert
        Assert.Equal(fenBefore, boardVm.CurrentFen);
        Assert.Equal(movesBefore, boardVm.MoveHistory.Count);
        Assert.False(boardVm.IsWhiteTurn);

        // Flip back
        boardVm.FlipBoard();
        Assert.Equal(fenBefore, boardVm.CurrentFen);
    }

    [Fact]
    public void PlayViewModel_ColorSelection_SetsCorrectStartingOrientation()
    {
        // Arrange
        var repo = new FakeGameRepository();
        var engine = new FakeEngineService();
        var playVm = new PlayViewModel(repo, engine);

        // Act 1: Play as Computer with White
        playVm.SetComputerModeCommand.Execute(null);
        playVm.SetWhiteColorCommand.Execute(null);

        // Assert: White at bottom (not flipped)
        Assert.True(playVm.IsHumanVsComputer);
        Assert.True(playVm.IsHumanWhite);
        Assert.False(playVm.Board.IsFlipped);
        Assert.Equal("You", playVm.BottomPlayerName);

        // Act 2: Play as Computer with Black
        playVm.SetBlackColorCommand.Execute(null);

        // Assert: Black at bottom (flipped)
        Assert.True(playVm.IsHumanBlack);
        Assert.True(playVm.Board.IsFlipped);
        Assert.Equal("You", playVm.BottomPlayerName);

        // Act 3: Start new game as Black preserves orientation
        playVm.StartNewGameCommand.Execute(null);
        Assert.True(playVm.Board.IsFlipped);
    }
}
