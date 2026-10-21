using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Models;
using Xunit;

namespace KnightLabChess.Core.Tests;

public class SpecialMovesTests
{
    [Fact]
    public void KingsideCastling_ExecutesKingAndRookCorrectly()
    {
        // Position with White King on e1 and Rook on h1, clear squares f1 and g1
        const string fen = "r3k2r/8/8/8/8/8/8/4K2R w KQkq - 0 1";
        var game = new GeraChessGameAdapter(fen);

        var moveResult = game.MakeMove(BoardSquare.FromAlgebraic("e1"), BoardSquare.FromAlgebraic("g1"));

        Assert.True(moveResult.Success);
        Assert.True(moveResult.Move!.IsCastling);
        Assert.Equal("O-O", moveResult.Move.San);

        // Verify King is on g1 and Rook is on f1
        var king = game.GetPieceAt(BoardSquare.FromAlgebraic("g1"));
        var rook = game.GetPieceAt(BoardSquare.FromAlgebraic("f1"));
        var oldKing = game.GetPieceAt(BoardSquare.FromAlgebraic("e1"));
        var oldRook = game.GetPieceAt(BoardSquare.FromAlgebraic("h1"));

        Assert.NotNull(king);
        Assert.Equal(PieceType.King, king.Type);
        Assert.NotNull(rook);
        Assert.Equal(PieceType.Rook, rook.Type);
        Assert.Null(oldKing);
        Assert.Null(oldRook);
    }

    [Fact]
    public void QueensideCastling_ExecutesKingAndRookCorrectly()
    {
        // Position with White King on e1 and Rook on a1, clear squares b1, c1, d1
        const string fen = "r3k2r/8/8/8/8/8/8/R3K3 w KQkq - 0 1";
        var game = new GeraChessGameAdapter(fen);

        var moveResult = game.MakeMove(BoardSquare.FromAlgebraic("e1"), BoardSquare.FromAlgebraic("c1"));

        Assert.True(moveResult.Success);
        Assert.True(moveResult.Move!.IsCastling);
        Assert.Equal("O-O-O", moveResult.Move.San);

        var king = game.GetPieceAt(BoardSquare.FromAlgebraic("c1"));
        var rook = game.GetPieceAt(BoardSquare.FromAlgebraic("d1"));
        Assert.NotNull(king);
        Assert.Equal(PieceType.King, king.Type);
        Assert.NotNull(rook);
        Assert.Equal(PieceType.Rook, rook.Type);
    }

    [Fact]
    public void Castling_DisallowedThroughCheck()
    {
        // White King on e1, Rook on h1. Black Rook on f8 attacks f1 square through which King must pass, Black King on h8
        const string fen = "5r1k/8/8/8/8/8/8/4K2R w K - 0 1";
        var game = new GeraChessGameAdapter(fen);

        var moveResult = game.MakeMove(BoardSquare.FromAlgebraic("e1"), BoardSquare.FromAlgebraic("g1"));
        Assert.False(moveResult.Success);
    }

    [Fact]
    public void CastlingRights_RevokedAfterKingMoves()
    {
        const string fen = "r3k2r/8/8/8/8/8/8/4K2R w KQkq - 0 1";
        var game = new GeraChessGameAdapter(fen);

        // Move king e1 to e2, then black moves a8 to b8, then king e2 back to e1
        game.MakeMove(BoardSquare.FromAlgebraic("e1"), BoardSquare.FromAlgebraic("e2"));
        game.MakeMove(BoardSquare.FromAlgebraic("a8"), BoardSquare.FromAlgebraic("b8"));
        game.MakeMove(BoardSquare.FromAlgebraic("e2"), BoardSquare.FromAlgebraic("e1"));
        game.MakeMove(BoardSquare.FromAlgebraic("b8"), BoardSquare.FromAlgebraic("a8"));

        // Now castling is no longer legal because king has moved
        var castleAttempt = game.MakeMove(BoardSquare.FromAlgebraic("e1"), BoardSquare.FromAlgebraic("g1"));
        Assert.False(castleAttempt.Success);
    }

    [Fact]
    public void EnPassant_ExecutesImmediatelyAndRemovesPassedPawn()
    {
        // White pawn on e5, Black pawn on d7. Black plays d7-d5. White plays e5xd6 (en passant).
        const string fen = "rnbqkbnr/pppppppp/8/4P3/8/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        var game = new GeraChessGameAdapter(fen);

        // Black advances pawn 2 squares: d7 to d5
        var blackMove = game.MakeMove(BoardSquare.FromAlgebraic("d7"), BoardSquare.FromAlgebraic("d5"));
        Assert.True(blackMove.Success);

        // White captures en passant: e5 to d6
        var epMove = game.MakeMove(BoardSquare.FromAlgebraic("e5"), BoardSquare.FromAlgebraic("d6"));
        Assert.True(epMove.Success);
        Assert.True(epMove.Move!.IsEnPassant);

        // Verify white pawn is on d6 and black pawn on d5 is gone
        var whitePawn = game.GetPieceAt(BoardSquare.FromAlgebraic("d6"));
        var capturedSquare = game.GetPieceAt(BoardSquare.FromAlgebraic("d5"));

        Assert.NotNull(whitePawn);
        Assert.Equal(PieceType.Pawn, whitePawn.Type);
        Assert.Equal(PieceColor.White, whitePawn.Color);
        Assert.Null(capturedSquare);
    }

    [Fact]
    public void EnPassant_ExpiresIfDeferred()
    {
        const string fen = "rnbqkbnr/pppppppp/8/4P3/8/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        var game = new GeraChessGameAdapter(fen);

        // Black plays d7-d5
        game.MakeMove(BoardSquare.FromAlgebraic("d7"), BoardSquare.FromAlgebraic("d5"));
        // White plays another move instead of en passant: a2 to a3
        game.MakeMove(BoardSquare.FromAlgebraic("a2"), BoardSquare.FromAlgebraic("a3"));
        // Black plays h7 to h6
        game.MakeMove(BoardSquare.FromAlgebraic("h7"), BoardSquare.FromAlgebraic("h6"));

        // Now e5 to d6 is no longer legal
        var expiredEp = game.MakeMove(BoardSquare.FromAlgebraic("e5"), BoardSquare.FromAlgebraic("d6"));
        Assert.False(expiredEp.Success);
    }

    [Theory]
    [InlineData(PieceType.Queen)]
    [InlineData(PieceType.Rook)]
    [InlineData(PieceType.Bishop)]
    [InlineData(PieceType.Knight)]
    public void PawnPromotion_SupportsAllFourPromotionPieceTypes(PieceType promoPiece)
    {
        // White pawn on e7, ready to advance to e8
        const string fen = "7k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);

        var moveResult = game.MakeMove(BoardSquare.FromAlgebraic("e7"), BoardSquare.FromAlgebraic("e8"), promoPiece);

        Assert.True(moveResult.Success);
        Assert.True(moveResult.Move!.IsPromotion);
        Assert.Equal(promoPiece, moveResult.Move.Promotion);

        var pieceOnE8 = game.GetPieceAt(BoardSquare.FromAlgebraic("e8"));
        Assert.NotNull(pieceOnE8);
        Assert.Equal(promoPiece, pieceOnE8.Type);
        Assert.Equal(PieceColor.White, pieceOnE8.Color);
    }
}
