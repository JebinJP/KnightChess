using KnightLabChess.App.ViewModels;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Models;
using Xunit;

namespace KnightLabChess.Core.Tests;

public class PawnPromotionTests
{
    // =========================================================================
    // Domain & Adapter Level Tests (IChessGame / GeraChessGameAdapter)
    // =========================================================================

    [Fact]
    public void IsPromotionMove_PawnOnStartingSquare_MovingToEighthRank_ReturnsFalse()
    {
        // Standard starting position: white pawn is on e2
        var game = new GeraChessGameAdapter();
        var from = BoardSquare.FromAlgebraic("e2");
        var to = BoardSquare.FromAlgebraic("e8");

        Assert.False(game.IsPromotionMove(from, to));
        Assert.False(game.IsLegalMove(from, to, PieceType.Queen));
    }

    [Fact]
    public void IsPromotionMove_WhitePawnOnE7_MovingToE8_ReturnsTrue()
    {
        // White pawn on e7, 8th rank empty
        const string fen = "7k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("e7");
        var to = BoardSquare.FromAlgebraic("e8");

        Assert.True(game.IsPromotionMove(from, to));
        Assert.True(game.IsLegalMove(from, to, PieceType.Queen));
        Assert.True(game.IsLegalMove(from, to, PieceType.Rook));
        Assert.True(game.IsLegalMove(from, to, PieceType.Bishop));
        Assert.True(game.IsLegalMove(from, to, PieceType.Knight));
    }

    [Fact]
    public void IsPromotionMove_BlackPawnOnD2_MovingToD1_ReturnsTrue()
    {
        // Black pawn on d2, 1st rank empty, Black to move
        const string fen = "7k/8/8/8/8/8/3p4/7K b - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("d2");
        var to = BoardSquare.FromAlgebraic("d1");

        Assert.True(game.IsPromotionMove(from, to));
        Assert.True(game.IsLegalMove(from, to, PieceType.Queen));
        Assert.True(game.IsLegalMove(from, to, PieceType.Knight));
    }

    [Fact]
    public void IsPromotionMove_WhitePawnCapturingPromotion_ReturnsTrue()
    {
        // White pawn on e7, Black rook on d8
        const string fen = "3r3k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("e7");
        var captureTo = BoardSquare.FromAlgebraic("d8");

        Assert.True(game.IsPromotionMove(from, captureTo));
        Assert.True(game.IsLegalMove(from, captureTo, PieceType.Queen));
    }

    [Fact]
    public void IsPromotionMove_WhitePawnDiagonalToEmptySquare_ReturnsFalse()
    {
        // White pawn on e7, d8 is empty (cannot move diagonally without capture)
        const string fen = "7k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("e7");
        var invalidDiag = BoardSquare.FromAlgebraic("d8");

        Assert.False(game.IsPromotionMove(from, invalidDiag));
        Assert.False(game.IsLegalMove(from, invalidDiag, PieceType.Queen));
    }

    [Fact]
    public void IsPromotionMove_BlockedPawn_ReturnsFalse()
    {
        // White pawn on e7, friendly rook on e8
        const string fen = "4R2k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("e7");
        var blockedSquare = BoardSquare.FromAlgebraic("e8");

        Assert.False(game.IsPromotionMove(from, blockedSquare));
        Assert.False(game.IsLegalMove(from, blockedSquare, PieceType.Queen));
    }

    [Fact]
    public void IsPromotionMove_NonPawnToEighthRank_ReturnsFalse()
    {
        // White rook on a1 moving to a8 (8th rank, but is not a pawn!)
        const string fen = "7k/8/8/8/8/8/8/R6K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("a1");
        var to = BoardSquare.FromAlgebraic("a8");

        Assert.False(game.IsPromotionMove(from, to));
        Assert.True(game.IsLegalMove(from, to)); // Standard move is legal
    }

    [Fact]
    public void IsPromotionMove_WhenNotPlayersTurn_ReturnsFalse()
    {
        // White pawn on e7, but Black to move
        const string fen = "7k/4P3/8/8/8/8/8/7K b - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("e7");
        var to = BoardSquare.FromAlgebraic("e8");

        Assert.False(game.IsPromotionMove(from, to));
    }

    [Theory]
    [InlineData(PieceType.Queen)]
    [InlineData(PieceType.Rook)]
    [InlineData(PieceType.Bishop)]
    [InlineData(PieceType.Knight)]
    public void MakeMove_BlackPawnPromotion_AllFourPieces_Succeeds(PieceType pieceType)
    {
        const string fen = "7k/8/8/8/8/8/3p4/7K b - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("d2");
        var to = BoardSquare.FromAlgebraic("d1");

        var result = game.MakeMove(from, to, pieceType);
        Assert.True(result.Success);
        Assert.True(result.Move!.IsPromotion);
        Assert.Equal(pieceType, result.Move.Promotion);

        var pieceOnD1 = game.GetPieceAt(to);
        Assert.NotNull(pieceOnD1);
        Assert.Equal(pieceType, pieceOnD1.Type);
        Assert.Equal(PieceColor.Black, pieceOnD1.Color);
    }

    [Fact]
    public void MakeMove_PawnStartingSquareToEighthRank_FailsWithoutStateMutation()
    {
        var game = new GeraChessGameAdapter();
        var initialFen = game.CurrentFen;
        var from = BoardSquare.FromAlgebraic("e2");
        var to = BoardSquare.FromAlgebraic("e8");

        var result = game.MakeMove(from, to, PieceType.Queen);
        Assert.False(result.Success);
        Assert.Equal(initialFen, game.CurrentFen);
        Assert.Empty(game.MoveHistory);
    }

    [Fact]
    public void MakeMove_PromotionWithoutPromotionPiece_Fails()
    {
        const string fen = "7k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("e7");
        var to = BoardSquare.FromAlgebraic("e8");

        var result = game.MakeMove(from, to, null);
        Assert.False(result.Success);
        Assert.Equal(fen, game.CurrentFen);
    }

    [Fact]
    public void MakeMove_PromotionWithInvalidPiece_Fails()
    {
        const string fen = "7k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("e7");
        var to = BoardSquare.FromAlgebraic("e8");

        // Pawns and Kings are not legal promotion pieces
        Assert.False(game.IsLegalMove(from, to, PieceType.Pawn));
        Assert.False(game.IsLegalMove(from, to, PieceType.King));

        var pawnResult = game.MakeMove(from, to, PieceType.Pawn);
        Assert.False(pawnResult.Success);

        var kingResult = game.MakeMove(from, to, PieceType.King);
        Assert.False(kingResult.Success);
    }

    [Fact]
    public void UndoMove_AfterPromotion_RestoresPawnAndPreviousState()
    {
        const string fen = "7k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var from = BoardSquare.FromAlgebraic("e7");
        var to = BoardSquare.FromAlgebraic("e8");

        var moveResult = game.MakeMove(from, to, PieceType.Queen);
        Assert.True(moveResult.Success);
        Assert.Equal(PieceColor.Black, game.ActiveTurn);

        var undone = game.UndoLastMove();
        Assert.True(undone);
        Assert.Equal(PieceColor.White, game.ActiveTurn);

        var restoredPawn = game.GetPieceAt(from);
        Assert.NotNull(restoredPawn);
        Assert.Equal(PieceType.Pawn, restoredPawn.Type);
        Assert.Equal(PieceColor.White, restoredPawn.Color);

        var clearedE8 = game.GetPieceAt(to);
        Assert.Null(clearedE8);
    }

    // =========================================================================
    // UI ViewModel Level Tests (ChessBoardViewModel & Square Selection Flow)
    // =========================================================================

    [Fact]
    public void ChessBoardViewModel_SelectingPawnOnStartingSquare_DoesNotTriggerPromotionDialog()
    {
        var game = new GeraChessGameAdapter();
        var vm = new ChessBoardViewModel(game);

        int promptCalls = 0;
        vm.PromptPromotion = (color) =>
        {
            promptCalls++;
            return PieceType.Queen;
        };

        // Select pawn on e2
        var e2Square = vm.Squares.First(s => s.File == 4 && s.Rank == 1);
        vm.SelectSquareCommand.Execute(e2Square);

        // Verification: Pawn is selected, no promotion dialog was called
        Assert.NotNull(vm.SelectedSquare);
        Assert.Equal(4, vm.SelectedSquare.File);
        Assert.Equal(1, vm.SelectedSquare.Rank);
        Assert.Equal(0, promptCalls);
    }

    [Fact]
    public void ChessBoardViewModel_ClickingDistantEighthRankSquare_FromStartingPawn_DoesNotTriggerDialog()
    {
        var game = new GeraChessGameAdapter();
        var vm = new ChessBoardViewModel(game);

        int promptCalls = 0;
        vm.PromptPromotion = (color) =>
        {
            promptCalls++;
            return PieceType.Queen;
        };

        // 1. Select white pawn on e2
        var e2Square = vm.Squares.First(s => s.File == 4 && s.Rank == 1);
        vm.SelectSquareCommand.Execute(e2Square);
        Assert.Equal(0, promptCalls);

        // 2. Click e8 (distant 8th rank square)
        var e8Square = vm.Squares.First(s => s.File == 4 && s.Rank == 7);
        vm.SelectSquareCommand.Execute(e8Square);

        // Verification:
        // Move is illegal and rejected.
        // Dialog MUST NOT have been triggered.
        // Board remains in starting state.
        Assert.Equal(0, promptCalls);
        Assert.Null(vm.SelectedSquare);
        Assert.Equal(game.StartFen, vm.CurrentFen);
        Assert.Empty(game.MoveHistory);

        var pieceOnE2 = game.GetPieceAt(4, 1);
        Assert.NotNull(pieceOnE2);
        Assert.Equal(PieceType.Pawn, pieceOnE2.Type);

        var pieceOnE8 = game.GetPieceAt(4, 7);
        Assert.NotNull(pieceOnE8);
        Assert.Equal(PieceType.King, pieceOnE8.Type);
        Assert.Equal(PieceColor.Black, pieceOnE8.Color);
    }

    [Fact]
    public void ChessBoardViewModel_LegalWhitePromotion_PromptsDialogExactlyOnce_AndExecutesPromotion()
    {
        const string fen = "7k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var vm = new ChessBoardViewModel(game);

        int promptCalls = 0;
        PieceColor? promptedColor = null;
        vm.PromptPromotion = (color) =>
        {
            promptCalls++;
            promptedColor = color;
            return PieceType.Knight; // Underpromote to Knight
        };

        // 1. Select pawn on e7
        var e7Square = vm.Squares.First(s => s.File == 4 && s.Rank == 6);
        vm.SelectSquareCommand.Execute(e7Square);
        Assert.Equal(0, promptCalls);

        // 2. Click destination e8
        var e8Square = vm.Squares.First(s => s.File == 4 && s.Rank == 7);
        vm.SelectSquareCommand.Execute(e8Square);

        // Verification:
        // Dialog called exactly once for White
        Assert.Equal(1, promptCalls);
        Assert.Equal(PieceColor.White, promptedColor);

        // Move executed as Knight promotion
        Assert.Single(game.MoveHistory);
        Assert.Equal(PieceType.Knight, game.MoveHistory[0].Promotion);

        var pieceOnE8 = game.GetPieceAt(4, 7);
        Assert.NotNull(pieceOnE8);
        Assert.Equal(PieceType.Knight, pieceOnE8.Type);
        Assert.Equal(PieceColor.White, pieceOnE8.Color);
    }

    [Fact]
    public void ChessBoardViewModel_LegalBlackPromotion_PromptsDialogExactlyOnce_AndExecutesPromotion()
    {
        const string fen = "7k/8/8/8/8/8/3p4/7K b - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var vm = new ChessBoardViewModel(game);

        int promptCalls = 0;
        PieceColor? promptedColor = null;
        vm.PromptPromotion = (color) =>
        {
            promptCalls++;
            promptedColor = color;
            return PieceType.Rook; // Promote to Rook
        };

        // 1. Select black pawn on d2
        var d2Square = vm.Squares.First(s => s.File == 3 && s.Rank == 1);
        vm.SelectSquareCommand.Execute(d2Square);
        Assert.Equal(0, promptCalls);

        // 2. Click destination d1
        var d1Square = vm.Squares.First(s => s.File == 3 && s.Rank == 0);
        vm.SelectSquareCommand.Execute(d1Square);

        // Verification:
        Assert.Equal(1, promptCalls);
        Assert.Equal(PieceColor.Black, promptedColor);

        Assert.Single(game.MoveHistory);
        Assert.Equal(PieceType.Rook, game.MoveHistory[0].Promotion);

        var pieceOnD1 = game.GetPieceAt(3, 0);
        Assert.NotNull(pieceOnD1);
        Assert.Equal(PieceType.Rook, pieceOnD1.Type);
        Assert.Equal(PieceColor.Black, pieceOnD1.Color);
    }

    [Fact]
    public void ChessBoardViewModel_PromotionCancelled_LeavesPawnUnchangedAndDoesNotMutateState()
    {
        const string fen = "7k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var vm = new ChessBoardViewModel(game);

        int promptCalls = 0;
        vm.PromptPromotion = (color) =>
        {
            promptCalls++;
            return null; // User cancelled or closed the promotion dialog
        };

        // 1. Select pawn on e7
        var e7Square = vm.Squares.First(s => s.File == 4 && s.Rank == 6);
        vm.SelectSquareCommand.Execute(e7Square);

        // 2. Click e8
        var e8Square = vm.Squares.First(s => s.File == 4 && s.Rank == 7);
        vm.SelectSquareCommand.Execute(e8Square);

        // Verification:
        // Dialog was called
        Assert.Equal(1, promptCalls);

        // Move was NOT executed; pawn is still on e7; turn is still White
        Assert.Empty(game.MoveHistory);
        Assert.Equal(fen, game.CurrentFen);
        Assert.True(game.IsWhiteTurn);

        var pieceOnE7 = game.GetPieceAt(4, 6);
        Assert.NotNull(pieceOnE7);
        Assert.Equal(PieceType.Pawn, pieceOnE7.Type);

        var pieceOnE8 = game.GetPieceAt(4, 7);
        Assert.Null(pieceOnE8);
    }

    [Fact]
    public void ChessBoardViewModel_IllegalMoveFromPromotionRankPawn_DoesNotPromptDialog()
    {
        const string fen = "7k/4P3/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var vm = new ChessBoardViewModel(game);

        int promptCalls = 0;
        vm.PromptPromotion = (color) =>
        {
            promptCalls++;
            return PieceType.Queen;
        };

        // 1. Select pawn on e7
        var e7Square = vm.Squares.First(s => s.File == 4 && s.Rank == 6);
        vm.SelectSquareCommand.Execute(e7Square);

        // 2. Click illegal destination a8 (unreachable diagonally)
        var a8Square = vm.Squares.First(s => s.File == 0 && s.Rank == 7);
        vm.SelectSquareCommand.Execute(a8Square);

        // Verification: No dialog, state unmutated
        Assert.Equal(0, promptCalls);
        Assert.Empty(game.MoveHistory);
        Assert.Equal(fen, game.CurrentFen);
    }

    [Fact]
    public void ChessBoardViewModel_NormalPieceMoves_DoNotTriggerPromotion()
    {
        var game = new GeraChessGameAdapter();
        var vm = new ChessBoardViewModel(game);

        int promptCalls = 0;
        vm.PromptPromotion = (color) =>
        {
            promptCalls++;
            return PieceType.Queen;
        };

        // Move knight from g1 to f3
        var g1 = vm.Squares.First(s => s.File == 6 && s.Rank == 0);
        vm.SelectSquareCommand.Execute(g1);

        var f3 = vm.Squares.First(s => s.File == 5 && s.Rank == 2);
        vm.SelectSquareCommand.Execute(f3);

        Assert.Equal(0, promptCalls);
        Assert.Single(game.MoveHistory);
        Assert.Equal("Nf3", game.MoveHistory[0].San);
    }
}
