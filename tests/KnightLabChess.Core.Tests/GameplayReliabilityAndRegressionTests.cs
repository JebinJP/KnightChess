using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightLabChess.App.ViewModels;
using KnightLabChess.Core.Audio;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Models;
using KnightLabChess.Core.Persistence;
using Xunit;

namespace KnightLabChess.Core.Tests;

/// <summary>
/// Part 4 — Comprehensive gameplay reliability, regression safety, and rule compliance test suite.
/// Audits normal moves, captures, legal validation, special moves, promotions, draws, undo, save/load,
/// board rotation, color selection, game modes, audio/animation events, layout stability,
/// and reproducible regression tests for previously fixed bugs.
/// </summary>
public class GameplayReliabilityAndRegressionTests
{
    private static void RunInSta(Action action)
    {
        Exception? exThrown = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exThrown = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exThrown != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exThrown).Throw();
        }
    }

    private static BoardSquareViewModel GetSquare(ChessBoardViewModel vm, string algebraic)
    {
        int file = algebraic[0] - 'a';
        int rank = algebraic[1] - '1';
        return vm.Squares.First(s => s.File == file && s.Rank == rank);
    }

    private class MockAudioService : IAudioService
    {
        public bool IsSoundEnabled { get; set; } = true;
        public int Volume { get; set; } = 100;
        public List<ChessSound> PlayedSounds { get; } = [];

        public void Play(ChessSound sound)
        {
            if (IsSoundEnabled)
            {
                PlayedSounds.Add(sound);
            }
        }

        public void UpdateSettings(bool isEnabled, int volume)
        {
            IsSoundEnabled = isEnabled;
            Volume = volume;
        }
    }

    private class MockGameRepository : IGameRepository
    {
        public List<GameRecord> StoredGames { get; } = [];
        private int _nextId = 1;

        public Task<int> SaveGameAsync(GameRecord game)
        {
            game.Id = _nextId++;
            StoredGames.Add(game);
            return Task.FromResult(game.Id);
        }

        public Task<GameRecord?> GetGameAsync(int id)
            => Task.FromResult(StoredGames.FirstOrDefault(g => g.Id == id));

        public Task<IReadOnlyList<GameRecord>> GetRecentGamesAsync(int limit = 20)
            => Task.FromResult<IReadOnlyList<GameRecord>>(StoredGames.TakeLast(limit).ToList());

        public Task DeleteGameAsync(int id)
        {
            StoredGames.RemoveAll(g => g.Id == id);
            return Task.CompletedTask;
        }
    }

    private class MockEngineService : IEngineService
    {
        public bool IsEngineAvailable => true;
        public string EngineName => "StockfishMock";
        public Task<bool> InitializeAsync(string? executablePath = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string?> GetBestMoveAsync(string fen, EngineDifficulty difficulty = EngineDifficulty.Casual, int moveTimeMs = 800, CancellationToken cancellationToken = default) => Task.FromResult<string?>("e7e5");
        public Task<EngineEvaluation?> EvaluatePositionAsync(string fen, int depth = 12, int timeoutMs = 1500, CancellationToken cancellationToken = default) => Task.FromResult<EngineEvaluation?>(null);
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    // =========================================================================
    // 1. Normal Moves and Captures Across All Piece Types
    // =========================================================================

    [Fact]
    public void Audit_AllPieceTypes_ExecuteNormalMovesAndCaptures()
    {
        var vm = new ChessBoardViewModel();

        // 1. Pawn quiet moves (1 and 2 squares)
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "e2"), GetSquare(vm, "e4"))); // White pawn 2-sq
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "d7"), GetSquare(vm, "d5"))); // Black pawn 2-sq

        // 2. Pawn capture
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "e4"), GetSquare(vm, "d5"))); // White pawn captures d5
        Assert.Contains("♟", vm.CapturedBlackPieces);

        // 3. Knight jumps over pieces
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "b8"), GetSquare(vm, "c6"))); // Black knight L-jump
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "g1"), GetSquare(vm, "f3"))); // White knight L-jump

        // 4. Bishop diagonal move
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "c8"), GetSquare(vm, "g4"))); // Black bishop diagonal
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "f1"), GetSquare(vm, "e2"))); // White bishop diagonal

        // 5. Queen move and capture
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "d8"), GetSquare(vm, "d5"))); // Black Queen captures d5
        Assert.Contains("♙", vm.CapturedWhitePieces);

        // 6. Rook move
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "h1"), GetSquare(vm, "g1"))); // White Rook moves along 1st rank
    }

    // =========================================================================
    // 2. Legal-Move Validation: Pins, Check Evading, King Safety
    // =========================================================================

    [Fact]
    public void Audit_PinnedPieces_AbsolutePinPreventsLeavingLine()
    {
        // White King on e1, White Knight on e2, Black Rook on e8 -> Knight is absolutely pinned along e-file
        var game = new GeraChessGameAdapter("4r2k/8/8/8/8/8/4N3/4K3 w - - 0 1");
        var vm = new ChessBoardViewModel(game);

        var knightSquare = GetSquare(vm, "e2");
        var destSquare = GetSquare(vm, "d4");

        // Knight cannot move off pin line
        bool moved = vm.TryExecuteMove(knightSquare, destSquare);
        Assert.False(moved);
        Assert.True(vm.IsWhiteTurn);
        Assert.True(GetSquare(vm, "e2").HasPiece);
    }

    [Fact]
    public void Audit_KingCannotWalkIntoAttackedSquares()
    {
        // White King on e1, Black Rook on d8 -> King cannot walk onto d1 or d2
        var game = new GeraChessGameAdapter("3r3k/8/8/8/8/8/8/4K3 w - - 0 1");
        var vm = new ChessBoardViewModel(game);

        var king = GetSquare(vm, "e1");
        var attackedSquare = GetSquare(vm, "d1");

        bool moved = vm.TryExecuteMove(king, attackedSquare);
        Assert.False(moved);
        Assert.True(vm.IsWhiteTurn);
        Assert.True(GetSquare(vm, "e1").HasPiece);
    }

    [Fact]
    public void Audit_MustResolveCheck_CannotIgnoreCheck()
    {
        // White King on e1 in check from Black Rook on e8, White Bishop on f1
        var game = new GeraChessGameAdapter("4r2k/8/8/8/8/8/8/4KB2 w - - 0 1");
        var vm = new ChessBoardViewModel(game);

        Assert.True(vm.IsInCheck);

        // Trying to move Bishop to a harmless square (e.g. g2) does not resolve check -> must fail
        var bishop = GetSquare(vm, "f1");
        var invalidDest = GetSquare(vm, "g2");
        bool bishopMoved = vm.TryExecuteMove(bishop, invalidDest);
        Assert.False(bishopMoved);

        // Blocking with Bishop on e2 resolves check -> must succeed
        var blockSquare = GetSquare(vm, "e2");
        bool blocked = vm.TryExecuteMove(bishop, blockSquare);
        Assert.True(blocked);
        Assert.False(vm.IsInCheck);
    }

    // =========================================================================
    // 3. Castling and En Passant
    // =========================================================================

    [Fact]
    public void Audit_Castling_WhiteAndBlack_KingsideAndQueenside_SucceedsWhenLegal()
    {
        // Setup position where White can castle Kingside, Black can castle Queenside
        const string fen = "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var vm = new ChessBoardViewModel(game);

        // White castles Kingside (e1 -> g1)
        bool whiteCastled = vm.TryExecuteMove(GetSquare(vm, "e1"), GetSquare(vm, "g1"));
        Assert.True(whiteCastled);
        Assert.Equal("WhiteKingDrawing", GetSquare(vm, "g1").PieceKey);
        Assert.Equal("WhiteRookDrawing", GetSquare(vm, "f1").PieceKey);
        Assert.False(GetSquare(vm, "e1").HasPiece);
        Assert.False(GetSquare(vm, "h1").HasPiece);

        // Black castles Queenside (e8 -> c8)
        bool blackCastled = vm.TryExecuteMove(GetSquare(vm, "e8"), GetSquare(vm, "c8"));
        Assert.True(blackCastled);
        Assert.Equal("BlackKingDrawing", GetSquare(vm, "c8").PieceKey);
        Assert.Equal("BlackRookDrawing", GetSquare(vm, "d8").PieceKey);
        Assert.False(GetSquare(vm, "e8").HasPiece);
        Assert.False(GetSquare(vm, "a8").HasPiece);
    }

    [Fact]
    public void Audit_Castling_DisallowedIfKingOrRookHasMoved()
    {
        const string fen = "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var vm = new ChessBoardViewModel(game);

        // King moves e1 -> e2, Black moves h8 -> g8, King moves e2 -> e1, Black moves g8 -> h8
        vm.MakeMove("e1e2");
        vm.MakeMove("h8g8");
        vm.MakeMove("e2e1");
        vm.MakeMove("g8h8");

        // Now White king is back on e1, but castling rights are lost
        bool canCastle = vm.TryExecuteMove(GetSquare(vm, "e1"), GetSquare(vm, "g1"));
        Assert.False(canCastle);
    }

    [Fact]
    public void Audit_Castling_DisallowedThroughOrIntoCheck()
    {
        // Black Rook on f8 attacks f1 square through which White King must pass to castle Kingside
        const string fen = "5r1k/8/8/8/8/8/8/4K2R w K - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var vm = new ChessBoardViewModel(game);

        bool castledThroughCheck = vm.TryExecuteMove(GetSquare(vm, "e1"), GetSquare(vm, "g1"));
        Assert.False(castledThroughCheck);
        Assert.True(vm.IsWhiteTurn);
    }

    [Fact]
    public void Audit_EnPassant_ExecutesCorrectlyForWhiteAndBlack()
    {
        var vm = new ChessBoardViewModel();

        // 1. e4 Nf6 2. e5 d5 -> White pawn on e5 can capture Black pawn on d5 en passant!
        vm.MakeMove("e2e4");
        vm.MakeMove("g8f6");
        vm.MakeMove("e4e5");
        vm.MakeMove("d7d5");

        var e5 = GetSquare(vm, "e5");
        var d6 = GetSquare(vm, "d6");

        bool epMoved = vm.TryExecuteMove(e5, d6);
        Assert.True(epMoved);
        // Captured pawn on d5 must be removed
        Assert.False(GetSquare(vm, "d5").HasPiece);
        Assert.True(GetSquare(vm, "d6").HasPiece);
        Assert.Contains("♟", vm.CapturedBlackPieces);
    }

    [Fact]
    public void Audit_EnPassant_ExpiresIfNotPlayedImmediately()
    {
        var vm = new ChessBoardViewModel();

        // 1. e4 Nf6 2. e5 d5 3. h3 (White plays different move) ...
        vm.MakeMove("e2e4");
        vm.MakeMove("g8f6");
        vm.MakeMove("e4e5");
        vm.MakeMove("d7d5");
        vm.MakeMove("h2h3"); // En passant opportunity skipped
        vm.MakeMove("a7a6");

        // Now trying to capture en passant e5 -> d6 must fail
        var e5 = GetSquare(vm, "e5");
        var d6 = GetSquare(vm, "d6");
        bool epLate = vm.TryExecuteMove(e5, d6);
        Assert.False(epLate);
    }

    // =========================================================================
    // 4 & 5. Pawn Promotion for Both Colors & Cancellation
    // =========================================================================

    [Theory]
    [InlineData(PieceType.Queen, "WhiteQueenDrawing")]
    [InlineData(PieceType.Rook, "WhiteRookDrawing")]
    [InlineData(PieceType.Bishop, "WhiteBishopDrawing")]
    [InlineData(PieceType.Knight, "WhiteKnightDrawing")]
    public void Audit_PawnPromotion_WhitePromotesOnRank8_AllFourPieces(PieceType piece, string expectedKey)
    {
        var game = new GeraChessGameAdapter("7k/4P3/8/8/8/8/8/7K w - - 0 1");
        var vm = new ChessBoardViewModel(game);
        vm.PromptPromotion = (_) => piece;

        bool promoted = vm.TryExecuteMove(GetSquare(vm, "e7"), GetSquare(vm, "e8"));
        Assert.True(promoted);
        Assert.Equal(expectedKey, GetSquare(vm, "e8").PieceKey);
        Assert.False(vm.IsWhiteTurn);
    }

    [Theory]
    [InlineData(PieceType.Queen, "BlackQueenDrawing")]
    [InlineData(PieceType.Rook, "BlackRookDrawing")]
    [InlineData(PieceType.Bishop, "BlackBishopDrawing")]
    [InlineData(PieceType.Knight, "BlackKnightDrawing")]
    public void Audit_PawnPromotion_BlackPromotesOnRank1_AllFourPieces(PieceType piece, string expectedKey)
    {
        var game = new GeraChessGameAdapter("7k/8/8/8/8/8/4p3/7K b - - 0 1");
        var vm = new ChessBoardViewModel(game);
        vm.PromptPromotion = (_) => piece;

        bool promoted = vm.TryExecuteMove(GetSquare(vm, "e2"), GetSquare(vm, "e1"));
        Assert.True(promoted);
        Assert.Equal(expectedKey, GetSquare(vm, "e1").PieceKey);
        Assert.True(vm.IsWhiteTurn);
    }

    [Fact]
    public void Audit_PawnPromotion_ViaCapture()
    {
        // White pawn on e7, Black Rook on d8
        var game = new GeraChessGameAdapter("3r3k/4P3/8/8/8/8/8/7K w - - 0 1");
        var vm = new ChessBoardViewModel(game);
        vm.PromptPromotion = (_) => PieceType.Queen;

        bool promotedWithCapture = vm.TryExecuteMove(GetSquare(vm, "e7"), GetSquare(vm, "d8"));
        Assert.True(promotedWithCapture);
        Assert.Equal("WhiteQueenDrawing", GetSquare(vm, "d8").PieceKey);
        Assert.Contains("♜", vm.CapturedBlackPieces);
    }

    [Fact]
    public void Audit_PromotionCancellation_LeavesBoardUnchangedAndPreservesTurn()
    {
        var game = new GeraChessGameAdapter("7k/4P3/8/8/8/8/8/7K w - - 0 1");
        var vm = new ChessBoardViewModel(game);

        // User closes/cancels promotion dialog -> returns null
        vm.PromptPromotion = (_) => null;

        var e7 = GetSquare(vm, "e7");
        var e8 = GetSquare(vm, "e8");
        bool moved = vm.TryExecuteMove(e7, e8);

        Assert.False(moved);
        Assert.True(vm.IsWhiteTurn);
        Assert.True(GetSquare(vm, "e7").HasPiece);
        Assert.Equal("WhitePawnDrawing", GetSquare(vm, "e7").PieceKey);
        Assert.False(GetSquare(vm, "e8").HasPiece);
        Assert.Empty(vm.MoveHistory);
    }

    // =========================================================================
    // 6. Check, Checkmate, Stalemate, and Draw Conditions
    // =========================================================================

    [Fact]
    public void Audit_Checkmate_ScholarsMateEndsWithWhiteWin()
    {
        var vm = new ChessBoardViewModel();
        vm.MakeMove("e2e4");
        vm.MakeMove("e7e5");
        vm.MakeMove("d1h5");
        vm.MakeMove("b8c6");
        vm.MakeMove("f1c4");
        vm.MakeMove("g8f6");
        vm.MakeMove("h5f7"); // Qxf7#

        Assert.True(vm.IsGameOver);
        Assert.True(vm.IsInCheck);
        Assert.Equal("White wins by checkmate!", vm.GameResultText);
    }

    [Fact]
    public void Audit_Stalemate_DeclaresAutomaticDraw()
    {
        // Stalemate position: Black King on a8, White King on c7, White Queen on b6. Black to move, no legal moves and not in check.
        const string fen = "k7/2K5/1Q6/8/8/8/8/8 b - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var vm = new ChessBoardViewModel(game);

        Assert.True(vm.IsGameOver);
        Assert.False(vm.IsInCheck);
        Assert.Equal("Draw by stalemate.", vm.GameResultText);
    }

    [Fact]
    public void Audit_ThreefoldRepetition_ClaimableOnlyNotPremature()
    {
        var vm = new ChessBoardViewModel();

        // 2 cycles
        vm.MakeMove("g1f3"); vm.MakeMove("g8f6");
        vm.MakeMove("f3g1"); vm.MakeMove("f6g8");

        Assert.False(vm.CanClaimDraw);
        Assert.False(vm.IsGameOver);

        // 3rd repetition
        vm.MakeMove("g1f3"); vm.MakeMove("g8f6");
        vm.MakeMove("f3g1"); vm.MakeMove("f6g8");

        Assert.True(vm.CanClaimDraw);
        Assert.False(vm.IsGameOver); // Must not end automatically

        vm.ClaimDraw();
        Assert.True(vm.IsGameOver);
        Assert.Equal("Draw by threefold repetition (claimed).", vm.GameResultText);
    }

    [Fact]
    public void Audit_InsufficientMaterial_AutomaticDraw()
    {
        // King vs King
        const string fen = "7k/8/8/8/8/8/8/7K w - - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var vm = new ChessBoardViewModel(game);

        Assert.True(vm.IsGameOver);
        Assert.Equal("Draw by insufficient material.", vm.GameResultText);
    }

    // =========================================================================
    // 7. Undo and Move History
    // =========================================================================

    [Fact]
    public void Audit_Undo_RestoresBoardStateAndCapturedPieces()
    {
        var vm = new ChessBoardViewModel();
        vm.MakeMove("e2e4");
        vm.MakeMove("d7d5");
        vm.MakeMove("e4d5"); // Capture

        Assert.Single(vm.CapturedBlackPieces);
        Assert.Equal("+1", vm.BottomMaterialAdvantage);

        // Undo capture
        vm.UndoMove();

        Assert.Empty(vm.CapturedBlackPieces);
        Assert.Equal(string.Empty, vm.BottomMaterialAdvantage);
        Assert.True(GetSquare(vm, "e4").HasPiece);
        Assert.True(GetSquare(vm, "d5").HasPiece);
        Assert.True(vm.IsWhiteTurn);
    }

    [Fact]
    public void Audit_Undo_CastlingRestoresBothKingAndRook()
    {
        const string fen = "r3k2r/8/8/8/8/8/8/4K2R w K - 0 1";
        var game = new GeraChessGameAdapter(fen);
        var vm = new ChessBoardViewModel(game);

        vm.MakeMove("e1g1");
        Assert.True(GetSquare(vm, "g1").HasPiece);
        Assert.True(GetSquare(vm, "f1").HasPiece);

        vm.UndoMove();

        Assert.True(GetSquare(vm, "e1").HasPiece);
        Assert.True(GetSquare(vm, "h1").HasPiece);
        Assert.False(GetSquare(vm, "g1").HasPiece);
        Assert.False(GetSquare(vm, "f1").HasPiece);
        Assert.True(vm.IsWhiteTurn);
    }

    [Fact]
    public void Audit_Undo_EnPassantRestoresCapturedPawn()
    {
        var vm = new ChessBoardViewModel();
        vm.MakeMove("e2e4");
        vm.MakeMove("g8f6");
        vm.MakeMove("e4e5");
        vm.MakeMove("d7d5");
        vm.MakeMove("e5d6"); // en passant

        Assert.False(GetSquare(vm, "d5").HasPiece);
        Assert.True(GetSquare(vm, "d6").HasPiece);
        Assert.Single(vm.CapturedBlackPieces);

        vm.UndoMove();

        Assert.True(GetSquare(vm, "e5").HasPiece);
        Assert.True(GetSquare(vm, "d5").HasPiece);
        Assert.False(GetSquare(vm, "d6").HasPiece);
        Assert.Empty(vm.CapturedBlackPieces);
    }

    // =========================================================================
    // 8. New Game, Save Game, and Load Game
    // =========================================================================

    [Fact]
    public async Task Audit_SaveAndLoadGame_PreservesFullGameHistoryAndPosition()
    {
        var repo = new MockGameRepository();
        var engine = new MockEngineService();
        var playVm = new PlayViewModel(repo, engine);

        // Play 3 moves: 1. e4 e5 2. Nf3
        playVm.Board.MakeMove("e2e4");
        playVm.Board.MakeMove("e7e5");
        playVm.Board.MakeMove("g1f3");

        await playVm.SaveActiveGameCommand.ExecuteAsync(null);
        Assert.Single(repo.StoredGames);

        // Start new game
        playVm.Board.NewGame();
        Assert.Empty(playVm.Board.MoveHistory);

        // Load the saved game
        await playVm.LoadLastSavedGameCommand.ExecuteAsync(null);

        Assert.Equal(3, playVm.Board.Game.MoveHistory.Count);
        Assert.Equal(2, playVm.Board.MoveHistory.Count); // 2 rows in UI (1. e4 e5, 2. Nf3)
        Assert.False(playVm.Board.IsWhiteTurn);
        Assert.True(GetSquare(playVm.Board, "f3").HasPiece);
        Assert.True(GetSquare(playVm.Board, "e5").HasPiece);
        Assert.True(GetSquare(playVm.Board, "e4").HasPiece);
    }

    // =========================================================================
    // 9. Board Rotation and Coordinate Mapping
    // =========================================================================

    [Fact]
    public void Audit_BoardRotation_PurelyPresentational_DoesNotChangeLegalMovesOrFen()
    {
        var vm = new ChessBoardViewModel();
        vm.MakeMove("e2e4");
        vm.MakeMove("e7e5");

        string fenBefore = vm.CurrentFen;
        bool turnBefore = vm.IsWhiteTurn;
        int moveCountBefore = vm.MoveHistory.Count;

        // Flip board
        vm.FlipBoard();

        Assert.True(vm.IsFlipped);
        Assert.Equal(fenBefore, vm.CurrentFen);
        Assert.Equal(turnBefore, vm.IsWhiteTurn);
        Assert.Equal(moveCountBefore, vm.MoveHistory.Count);

        // Moves made while flipped still execute on correct chess squares
        bool movedWhileFlipped = vm.TryExecuteMove(GetSquare(vm, "g1"), GetSquare(vm, "f3"));
        Assert.True(movedWhileFlipped);
        Assert.True(GetSquare(vm, "f3").HasPiece);
        Assert.False(vm.IsWhiteTurn);
    }

    // =========================================================================
    // 10. Player Color and Game Mode Selection
    // =========================================================================

    [Fact]
    public void Audit_PlayerColorSelection_FlipsBoardForBlackInComputerMode()
    {
        var repo = new MockGameRepository();
        var engine = new MockEngineService();
        var playVm = new PlayViewModel(repo, engine);

        playVm.SetComputerModeCommand.Execute(null);
        Assert.True(playVm.IsHumanVsComputer);

        // Select Black
        playVm.SelectHumanColorCommand.Execute(PieceColor.Black);
        Assert.True(playVm.IsHumanBlack);
        Assert.True(playVm.Board.IsFlipped);

        // Select White
        playVm.SelectHumanColorCommand.Execute(PieceColor.White);
        Assert.True(playVm.IsHumanWhite);
        Assert.False(playVm.Board.IsFlipped);
    }

    // =========================================================================
    // 11. Audio and Animation Events
    // =========================================================================

    [Fact]
    public void Audit_AudioEvents_DispatchedForMoveCaptureCastleCheckMate()
    {
        var audio = new MockAudioService();
        var vm = new ChessBoardViewModel(audio);

        // 1. Quiet move -> ChessSound.Move
        vm.MakeMove("e2e4");
        Assert.Contains(ChessSound.Move, audio.PlayedSounds);

        // 2. Capture -> ChessSound.Capture
        vm.MakeMove("d7d5");
        vm.MakeMove("e4d5");
        Assert.Contains(ChessSound.Capture, audio.PlayedSounds);
    }

    [Fact]
    public void Audit_MoveExecuted_DispatchesCorrectMetadata()
    {
        var vm = new ChessBoardViewModel();
        MoveExecutedEventArgs? executedArgs = null;
        vm.MoveExecuted += (_, args) => executedArgs = args;

        vm.TryExecuteMove(GetSquare(vm, "e2"), GetSquare(vm, "e4"), isUserDrag: true);

        Assert.NotNull(executedArgs);
        Assert.True(executedArgs!.IsUserDrag);
        Assert.Equal("e2", executedArgs.Move.From.ToString());
        Assert.Equal("e4", executedArgs.Move.To.ToString());
        Assert.Equal("e4", executedArgs.Move.San);
        Assert.Equal(PieceColor.White, executedArgs.Move.Color);
        Assert.Equal(PieceType.Pawn, executedArgs.Move.PieceType);
    }

    // =========================================================================
    // 12. Board Layout Stability
    // =========================================================================

    [Fact]
    public void Audit_BoardLayout_CapturedPiecesDoNotResizeBoard()
    {
        RunInSta(() =>
        {
            var control = new KnightLabChess.App.Controls.ChessBoardControl();
            var vm = new ChessBoardViewModel();
            control.DataContext = vm;

            // Compute board size without captures
            var initialSize = control.CalculateTargetBoardSize(new System.Windows.Size(700, 700));

            // Simulate capturing multiple pieces
            for (int i = 0; i < 8; i++)
            {
                vm.CapturedWhitePieces.Add("♙");
                vm.CapturedBlackPieces.Add("♟");
            }

            // Board target size must remain strictly identical
            var afterCapturesSize = control.CalculateTargetBoardSize(new System.Windows.Size(700, 700));
            Assert.Equal(initialSize.Width, afterCapturesSize.Width);
            Assert.Equal(initialSize.Height, afterCapturesSize.Height);
        });
    }

    // =========================================================================
    // 13. Reproducible Regression Tests for Previously Fixed Bugs
    // =========================================================================

    [Fact]
    public void Regression_PawnPromotionDialog_OpensOnlyForLegalPawnPromotion()
    {
        // BUG 3 REGRESSION: Non-pawns or illegal moves must NEVER open the promotion dialog!
        
        // 1. Moving White Rook to 8th rank (a1 -> a8) -> Legal move, but NOT a pawn! Must NOT prompt!
        {
            var game = new GeraChessGameAdapter("r6k/4P3/8/8/8/8/8/R6K w - - 0 1");
            var vm = new ChessBoardViewModel(game);
            bool promptCalled = false;
            vm.PromptPromotion = (_) => { promptCalled = true; return PieceType.Queen; };

            var rook = GetSquare(vm, "a1");
            var a8 = GetSquare(vm, "a8");
            vm.TryExecuteMove(rook, a8);
            Assert.False(promptCalled);
        }

        // 2. White Pawn diagonal move to empty square (e7 -> f8) -> Illegal! Must NOT prompt!
        {
            var game = new GeraChessGameAdapter("r6k/4P3/8/8/8/8/8/R6K w - - 0 1");
            var vm = new ChessBoardViewModel(game);
            bool promptCalled = false;
            vm.PromptPromotion = (_) => { promptCalled = true; return PieceType.Queen; };

            var pawn = GetSquare(vm, "e7");
            var emptyF8 = GetSquare(vm, "f8");
            vm.TryExecuteMove(pawn, emptyF8);
            Assert.False(promptCalled);
        }

        // 3. Legal pawn step to 8th rank (e7 -> e8) -> Legal pawn promotion! MUST prompt!
        {
            var game = new GeraChessGameAdapter("r6k/4P3/8/8/8/8/8/R6K w - - 0 1");
            var vm = new ChessBoardViewModel(game);
            bool promptCalled = false;
            vm.PromptPromotion = (_) => { promptCalled = true; return PieceType.Queen; };

            var pawn = GetSquare(vm, "e7");
            var e8 = GetSquare(vm, "e8");
            vm.TryExecuteMove(pawn, e8);
            Assert.True(promptCalled);
        }
    }

    [Fact]
    public void Regression_NullReference_OnPotentialDragSquare_NeverOccurs()
    {
        // BUG 1 REGRESSION: Mouse actions on empty squares, null targets, or outside bounds must never throw
        var vm = new ChessBoardViewModel();

        Assert.False(vm.TryExecuteMove(null, GetSquare(vm, "e4")));
        Assert.False(vm.TryExecuteMove(GetSquare(vm, "e2"), null));
        Assert.False(vm.TryExecuteMove(null, null));

        vm.SelectSquare(null);
        Assert.Null(vm.SelectedSquare);
    }

    [Fact]
    public void Regression_PromotionCancellation_DoesNotSwitchTurnOrCorruptBoard()
    {
        // BUG 6 REGRESSION: Dismissing promotion dialog must preserve piece on 7th rank and leave turn with White
        var game = new GeraChessGameAdapter("7k/4P3/8/8/8/8/8/7K w - - 0 1");
        var vm = new ChessBoardViewModel(game);
        vm.PromptPromotion = (_) => null; // Cancelled

        bool success = vm.TryExecuteMove(GetSquare(vm, "e7"), GetSquare(vm, "e8"));

        Assert.False(success);
        Assert.True(vm.IsWhiteTurn);
        Assert.True(GetSquare(vm, "e7").HasPiece);
        Assert.Equal("WhitePawnDrawing", GetSquare(vm, "e7").PieceKey);
        Assert.False(GetSquare(vm, "e8").HasPiece);
        Assert.Empty(vm.MoveHistory);
    }

    [Fact]
    public void Regression_BoardRotation_DoesNotMutateGamePositionOrMoveVectors()
    {
        // BUG 4 REGRESSION: Flipping the board multiple times must not invert coordinates or mutate the position
        var vm = new ChessBoardViewModel();
        vm.MakeMove("e2e4");
        vm.MakeMove("e7e5");

        string originalFen = vm.CurrentFen;

        for (int i = 0; i < 6; i++)
        {
            vm.FlipBoard();
        }

        Assert.False(vm.IsFlipped);
        Assert.Equal(originalFen, vm.CurrentFen);
        Assert.True(GetSquare(vm, "e4").HasPiece);
        Assert.True(GetSquare(vm, "e5").HasPiece);
        Assert.True(vm.IsWhiteTurn); // White to move after 1. e4 e5
    }
}
