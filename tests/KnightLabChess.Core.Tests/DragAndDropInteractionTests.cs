using System;
using System.Linq;
using KnightLabChess.App.ViewModels;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Models;
using Xunit;

namespace KnightLabChess.Core.Tests;

/// <summary>
/// Regression and unit tests for the chess drag-and-drop and click-to-move lifecycle.
/// Validates that null references cannot occur, state transitions are predictable,
/// castling gestures work, promotions only prompt on legal moves, and the board remains stable.
/// </summary>
public class DragAndDropInteractionTests
{
    private static BoardSquareViewModel GetSquare(ChessBoardViewModel vm, string algebraic)
    {
        int file = algebraic[0] - 'a';
        int rank = algebraic[1] - '1';
        return vm.Squares.First(s => s.File == file && s.Rank == rank);
    }

    [Fact]
    public void Case1_ClickEmptySquare_DoesNotSelectOrMove()
    {
        var vm = new ChessBoardViewModel();
        var emptySq = GetSquare(vm, "e4");

        Assert.False(emptySq.HasPiece);
        vm.SelectSquare(emptySq);

        Assert.Null(vm.SelectedSquare);
        Assert.False(emptySq.IsSelected);
        Assert.True(vm.IsWhiteTurn);
    }

    [Fact]
    public void Case2_ClickFriendlyPiece_WithoutDragging_SelectsAndHighlightsLegalDestinations()
    {
        var vm = new ChessBoardViewModel();
        var e2Sq = GetSquare(vm, "e2");

        vm.SelectSquare(e2Sq);

        Assert.Same(e2Sq, vm.SelectedSquare);
        Assert.True(e2Sq.IsSelected);

        var e3Sq = GetSquare(vm, "e3");
        var e4Sq = GetSquare(vm, "e4");
        var e5Sq = GetSquare(vm, "e5");

        Assert.True(e3Sq.IsLegalMove);
        Assert.True(e4Sq.IsLegalMove);
        Assert.False(e5Sq.IsLegalMove);
        Assert.Empty(vm.MoveHistory);
    }

    [Fact]
    public void Case3_DragFriendlyPiece_ToLegalDestination_ExecutesMoveSuccessfully()
    {
        var vm = new ChessBoardViewModel();
        var e2 = GetSquare(vm, "e2");
        var e4 = GetSquare(vm, "e4");

        bool moved = vm.TryExecuteMove(e2, e4);

        Assert.True(moved);
        Assert.False(vm.IsWhiteTurn); // Turn shifted to Black
        Assert.Null(vm.SelectedSquare);
        Assert.False(GetSquare(vm, "e2").HasPiece);
        Assert.True(GetSquare(vm, "e4").HasPiece);
        Assert.Single(vm.MoveHistory);
    }

    [Fact]
    public void Case4_DragPiece_ToIllegalDestination_FailsCleanlyWithoutCorruptingState()
    {
        var vm = new ChessBoardViewModel();
        var e2 = GetSquare(vm, "e2");
        var e5 = GetSquare(vm, "e5"); // Illegal jump for pawn

        bool moved = vm.TryExecuteMove(e2, e5);

        Assert.False(moved);
        Assert.True(vm.IsWhiteTurn);
        Assert.True(GetSquare(vm, "e2").HasPiece);
        Assert.False(GetSquare(vm, "e5").HasPiece);
        Assert.Empty(vm.MoveHistory);
        Assert.Null(vm.SelectedSquare);
    }

    [Fact]
    public void Case5_DragPiece_NullSourceOrDestination_ReturnsFalseWithoutThrowing()
    {
        var vm = new ChessBoardViewModel();
        var e4 = GetSquare(vm, "e4");
        var e2 = GetSquare(vm, "e2");

        // Null source
        Assert.False(vm.TryExecuteMove(null, e4));

        // Null destination
        Assert.False(vm.TryExecuteMove(e2, null));

        // Both null
        Assert.False(vm.TryExecuteMove(null, null));

        // Null select square
        vm.SelectSquare(null);
        Assert.Null(vm.SelectedSquare);
    }

    [Fact]
    public void Case6_StartDraggingAndDropOnSameSquare_PreservesPieceWithoutMove()
    {
        var vm = new ChessBoardViewModel();
        var e2 = GetSquare(vm, "e2");

        bool moved = vm.TryExecuteMove(e2, e2);

        Assert.False(moved);
        Assert.True(vm.IsWhiteTurn);
        Assert.True(GetSquare(vm, "e2").HasPiece);
        Assert.Empty(vm.MoveHistory);
    }

    [Fact]
    public void Case7_DragOpponentPiece_WhenNotTurn_RejectsDragAndMove()
    {
        var vm = new ChessBoardViewModel();
        var e7 = GetSquare(vm, "e7"); // Black pawn
        var e5 = GetSquare(vm, "e5");

        // Attempting to select opponent piece directly
        vm.SelectSquare(e7);
        Assert.Null(vm.SelectedSquare);

        // Attempting to execute move with opponent piece when it's White's turn
        bool moved = vm.TryExecuteMove(e7, e5);
        Assert.False(moved);
        Assert.True(vm.IsWhiteTurn);
        Assert.True(GetSquare(vm, "e7").HasPiece);
    }

    [Fact]
    public void Case8_SelectNonBoardOrDeselect_DoesNotThrow()
    {
        var vm = new ChessBoardViewModel();
        var e2 = GetSquare(vm, "e2");

        vm.SelectSquare(e2);
        Assert.Same(e2, vm.SelectedSquare);

        // Click again to deselect
        vm.SelectSquare(e2);
        Assert.Null(vm.SelectedSquare);
        Assert.False(e2.IsSelected);
    }

    [Fact]
    public void Case9_SimulateReentrancy_DoesNotCorruptOrThrow()
    {
        var vm = new ChessBoardViewModel();
        var e2 = GetSquare(vm, "e2");
        var e4 = GetSquare(vm, "e4");

        // Normal move
        bool first = vm.TryExecuteMove(e2, e4);
        Assert.True(first);

        // Attempting to execute move while input disabled
        vm.IsInputDisabled = true;
        var e7 = GetSquare(vm, "e7");
        var e5 = GetSquare(vm, "e5");
        bool blocked = vm.TryExecuteMove(e7, e5);
        Assert.False(blocked);
        vm.IsInputDisabled = false;
    }

    [Fact]
    public void Case10_FlipBoard_PreservesLegalAndIllegalDragOperations()
    {
        var vm = new ChessBoardViewModel();
        vm.FlipBoard();
        Assert.True(vm.IsFlipped);

        var e2 = GetSquare(vm, "e2");
        var e4 = GetSquare(vm, "e4");

        // Legal move on flipped board
        bool moved = vm.TryExecuteMove(e2, e4);
        Assert.True(moved);
        Assert.False(vm.IsWhiteTurn);

        // Illegal move on flipped board
        var g8 = GetSquare(vm, "g8");
        var g5 = GetSquare(vm, "g5"); // Knight cannot jump to g5
        bool illegalMoved = vm.TryExecuteMove(g8, g5);
        Assert.False(illegalMoved);
    }

    [Fact]
    public void Case11_ExecuteCapture_UpdatesCapturedPiecesAndRemainsStable()
    {
        var vm = new ChessBoardViewModel();

        // 1. e4 d5
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "e2"), GetSquare(vm, "e4")));
        Assert.True(vm.TryExecuteMove(GetSquare(vm, "e7"), GetSquare(vm, "e5")));

        // 2. exd5 (capture Black Pawn on d5)
        // Setup via direct move or FEN for precise capture test
        vm.LoadFen("rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq d6 0 2");
        var e4 = GetSquare(vm, "e4");
        var d5 = GetSquare(vm, "d5");

        bool captured = vm.TryExecuteMove(e4, d5);
        Assert.True(captured);
        Assert.Equal("♟", vm.CapturedBlackPieces.FirstOrDefault());
        Assert.True(GetSquare(vm, "d5").HasPiece);
        Assert.True(GetSquare(vm, "d5").IsWhitePiece);
    }

    [Fact]
    public void Case12_PromotePawnLegally_PromptsUserAndExecutesPromotion()
    {
        // Setup: White pawn on e7, empty e8
        var vm = new ChessBoardViewModel();
        vm.LoadFen("7k/4P3/8/8/8/8/8/7K w - - 0 1");

        bool promptCalled = false;
        vm.PromptPromotion = (color) =>
        {
            promptCalled = true;
            Assert.Equal(PieceColor.White, color);
            return PieceType.Knight; // Promote to Knight
        };

        var e7 = GetSquare(vm, "e7");
        var e8 = GetSquare(vm, "e8");

        bool moved = vm.TryExecuteMove(e7, e8);

        Assert.True(promptCalled);
        Assert.True(moved);
        var promotedPiece = GetSquare(vm, "e8");
        Assert.True(promotedPiece.HasPiece);
        Assert.Equal("WhiteKnightDrawing", promotedPiece.PieceKey);
    }

    [Fact]
    public void Case12_PromotionCancelled_PreservesBoardStateWithoutMoving()
    {
        var vm = new ChessBoardViewModel();
        vm.LoadFen("7k/4P3/8/8/8/8/8/7K w - - 0 1");

        vm.PromptPromotion = (color) => null; // User cancelled

        var e7 = GetSquare(vm, "e7");
        var e8 = GetSquare(vm, "e8");

        bool moved = vm.TryExecuteMove(e7, e8);

        Assert.False(moved);
        Assert.True(GetSquare(vm, "e7").HasPiece);
        Assert.False(GetSquare(vm, "e8").HasPiece);
    }

    [Fact]
    public void Case13_StartNewGame_ClearsAllSelectionAndDragState()
    {
        var vm = new ChessBoardViewModel();
        var e2 = GetSquare(vm, "e2");
        vm.SelectSquare(e2);
        e2.IsBeingDragged = true;

        vm.NewGame();

        Assert.Null(vm.SelectedSquare);
        Assert.All(vm.Squares, s =>
        {
            Assert.False(s.IsSelected);
            Assert.False(s.IsBeingDragged);
            Assert.False(s.IsLegalMove);
        });
        Assert.True(vm.IsWhiteTurn);
        Assert.Empty(vm.MoveHistory);
    }

    [Fact]
    public void Case14_RepeatInteractionsManyTimes_ZeroExceptionsOrDuplicateMoves()
    {
        var vm = new ChessBoardViewModel();

        for (int i = 0; i < 50; i++)
        {
            // Null parameters
            Assert.False(vm.TryExecuteMove(null, null));
            vm.SelectSquare(null);

            // Empty square selection
            vm.SelectSquare(GetSquare(vm, "e4"));

            // Deselect
            var e2 = GetSquare(vm, "e2");
            vm.SelectSquare(e2);
            vm.SelectSquare(e2);

            // Illegal move
            Assert.False(vm.TryExecuteMove(e2, GetSquare(vm, "h5")));
        }

        // State remains perfectly clean
        Assert.Null(vm.SelectedSquare);
        Assert.True(vm.IsWhiteTurn);
        Assert.Empty(vm.MoveHistory);
    }

    [Fact]
    public void CastlingGesture_DragKingOntoKingsideRook_ExecutesCastling()
    {
        var vm = new ChessBoardViewModel();
        // Position where White can castle Kingside
        vm.LoadFen("r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4");

        var kingSq = GetSquare(vm, "e1");
        var rookSq = GetSquare(vm, "h1");

        // Drag King onto Rook
        bool moved = vm.TryExecuteMove(kingSq, rookSq);

        Assert.True(moved);
        // King should be on g1, Rook should be on f1
        Assert.Equal("WhiteKingDrawing", GetSquare(vm, "g1").PieceKey);
        Assert.Equal("WhiteRookDrawing", GetSquare(vm, "f1").PieceKey);
        Assert.False(GetSquare(vm, "e1").HasPiece);
        Assert.False(GetSquare(vm, "h1").HasPiece);
    }

    [Fact]
    public void CastlingGesture_DragKingOntoQueensideRook_ExecutesCastling()
    {
        var vm = new ChessBoardViewModel();
        // Position where White can castle Queenside
        vm.LoadFen("r3kbnr/ppp1pppp/2nqb3/3p4/3P4/2NQB3/PPP1PPPP/R3KBNR w KQkq - 4 4");

        var kingSq = GetSquare(vm, "e1");
        var rookSq = GetSquare(vm, "a1");

        // Drag King onto Queenside Rook
        bool moved = vm.TryExecuteMove(kingSq, rookSq);

        Assert.True(moved);
        // King should be on c1, Rook should be on d1
        Assert.Equal("WhiteKingDrawing", GetSquare(vm, "c1").PieceKey);
        Assert.Equal("WhiteRookDrawing", GetSquare(vm, "d1").PieceKey);
        Assert.False(GetSquare(vm, "e1").HasPiece);
        Assert.False(GetSquare(vm, "a1").HasPiece);
    }

    private static void RunInSta(Action action)
    {
        Exception? exThrown = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    var app = new KnightLabChess.App.App();
                    app.InitializeComponent();
                }
                action();
            }
            catch (Exception ex)
            {
                exThrown = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exThrown != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exThrown).Throw();
        }
    }

    [Fact]
    public void UI_LostMouseCaptureFromChildButton_DoesNotCancelDrag()
    {
        RunInSta(() =>
        {
            var control = new KnightLabChess.App.Controls.ChessBoardControl();
            var vm = new ChessBoardViewModel();
            control.DataContext = vm;

            var dragStateField = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetField("_dragState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var sourceSquareField = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetField("_sourceSquare", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var lostMouseCaptureMethod = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetMethod("Board_LostMouseCapture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            // Simulate active drag
            var e2 = GetSquare(vm, "e2");
            sourceSquareField.SetValue(control, e2);
            var draggingVal = Enum.Parse(dragStateField.FieldType, "Dragging");
            dragStateField.SetValue(control, draggingVal);

            // Create a MouseEventArgs where OriginalSource is a child Button (simulating the Button losing capture when board captures)
            var childButton = new System.Windows.Controls.Button();
            var dummyMouseDevice = System.Windows.Input.InputManager.Current.PrimaryMouseDevice;
            var mouseEventArgs = new System.Windows.Input.MouseEventArgs(dummyMouseDevice, 0)
            {
                RoutedEvent = System.Windows.UIElement.LostMouseCaptureEvent,
                Source = childButton
            };

            // Invoke Board_LostMouseCapture
            lostMouseCaptureMethod.Invoke(control, [control, mouseEventArgs]);

            // VERIFICATION: Drag state MUST NOT be cancelled by a child button's lost capture!
            var currentDragState = dragStateField.GetValue(control)?.ToString();
            Assert.Equal("Dragging", currentDragState);
            Assert.Same(e2, sourceSquareField.GetValue(control));
        });
    }

    [Fact]
    public void UI_LostMouseCaptureFromBoardContainerGrid_CancelsDragSafely()
    {
        RunInSta(() =>
        {
            var control = new KnightLabChess.App.Controls.ChessBoardControl();
            var vm = new ChessBoardViewModel();
            control.DataContext = vm;

            var dragStateField = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetField("_dragState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var sourceSquareField = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetField("_sourceSquare", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var lostMouseCaptureMethod = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetMethod("Board_LostMouseCapture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var boardContainerGridField = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetField("BoardContainerGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            var grid = (System.Windows.Controls.Grid)boardContainerGridField.GetValue(control)!;

            // Simulate active drag
            var e2 = GetSquare(vm, "e2");
            e2.IsBeingDragged = true;
            sourceSquareField.SetValue(control, e2);
            var draggingVal = Enum.Parse(dragStateField.FieldType, "Dragging");
            dragStateField.SetValue(control, draggingVal);

            // Create MouseEventArgs where OriginalSource is BoardContainerGrid itself (external loss of capture)
            var dummyMouseDevice = System.Windows.Input.InputManager.Current.PrimaryMouseDevice;
            var mouseEventArgs = new System.Windows.Input.MouseEventArgs(dummyMouseDevice, 0)
            {
                RoutedEvent = System.Windows.UIElement.LostMouseCaptureEvent,
                Source = grid
            };

            // Invoke Board_LostMouseCapture
            lostMouseCaptureMethod.Invoke(control, [control, mouseEventArgs]);

            // VERIFICATION: Drag state must safely revert to Idle and clear source
            var currentDragState = dragStateField.GetValue(control)?.ToString();
            Assert.Equal("Idle", currentDragState);
            Assert.Null(sourceSquareField.GetValue(control));
            Assert.False(e2.IsBeingDragged);
        });
    }

    [Fact]
    public void UI_RightClickOrEscape_CancelsDragAndClearsSelection()
    {
        RunInSta(() =>
        {
            var control = new KnightLabChess.App.Controls.ChessBoardControl();
            var vm = new ChessBoardViewModel();
            control.DataContext = vm;

            var dragStateField = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetField("_dragState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var sourceSquareField = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetField("_sourceSquare", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var rightButtonDownMethod = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetMethod("Board_PreviewMouseRightButtonDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            var e2 = GetSquare(vm, "e2");
            vm.SelectSquare(e2);
            e2.IsBeingDragged = true;
            sourceSquareField.SetValue(control, e2);
            var draggingVal = Enum.Parse(dragStateField.FieldType, "Dragging");
            dragStateField.SetValue(control, draggingVal);

            // Right click
            var dummyMouseDevice = System.Windows.Input.InputManager.Current.PrimaryMouseDevice;
            var rightClickArgs = new System.Windows.Input.MouseButtonEventArgs(dummyMouseDevice, 0, System.Windows.Input.MouseButton.Right)
            {
                RoutedEvent = System.Windows.UIElement.PreviewMouseRightButtonDownEvent
            };

            rightButtonDownMethod.Invoke(control, [control, rightClickArgs]);

            Assert.Equal("Idle", dragStateField.GetValue(control)?.ToString());
            Assert.Null(sourceSquareField.GetValue(control));
            Assert.Null(vm.SelectedSquare);
            Assert.False(e2.IsBeingDragged);
            Assert.True(rightClickArgs.Handled);
        });
    }

    [Fact]
    public void UI_GetSquareAtPoint_ResolvesCorrectSquareForStandardAndFlippedBoards()
    {
        RunInSta(() =>
        {
            var control = new KnightLabChess.App.Controls.ChessBoardControl();
            var vm = new ChessBoardViewModel();
            control.DataContext = vm;

            var getSquareMethod = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetMethod("GetSquareAtPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            // 560x560 board: each square is 70x70 px.
            // Center of visual row 6, col 4: X = 4 * 70 + 35 = 315, Y = 6 * 70 + 35 = 455.
            // In standard orientation (White bottom), visual row 6 is rank 2 (rank index 1), col 4 is file e (file index 4).
            var pt = new System.Windows.Point(315, 455);
            var sqStandard = (BoardSquareViewModel?)getSquareMethod.Invoke(control, [vm, pt]);

            Assert.NotNull(sqStandard);
            Assert.Equal("e2", sqStandard!.SquareName);

            // Now flip the board (Black bottom, White top)
            vm.FlipBoard();
            Assert.True(vm.IsFlipped);

            // On flipped board, visual row 6, col 4:
            // visual row 6 is rank index 6 (rank 7), col 4 is file index 7 - 4 = 3 (file d) -> d7.
            var sqFlipped = (BoardSquareViewModel?)getSquareMethod.Invoke(control, [vm, pt]);
            Assert.NotNull(sqFlipped);
            Assert.Equal("d7", sqFlipped!.SquareName);

            // Center of visual row 1, col 3: X = 3 * 70 + 35 = 245, Y = 1 * 70 + 35 = 105.
            // On flipped board: rank index 1 (rank 2), file index 7 - 3 = 4 (file e) -> e2!
            var ptE2Flipped = new System.Windows.Point(245, 105);
            var sqE2Flipped = (BoardSquareViewModel?)getSquareMethod.Invoke(control, [vm, ptE2Flipped]);
            Assert.NotNull(sqE2Flipped);
            Assert.Equal("e2", sqE2Flipped!.SquareName);

            // Point outside board: X = -10, Y = 100 -> must return null
            var ptOutside = new System.Windows.Point(-10, 100);
            var sqOutside = (BoardSquareViewModel?)getSquareMethod.Invoke(control, [vm, ptOutside]);
            Assert.Null(sqOutside);
        });
    }

    [Fact]
    public void UI_ChessBoardControl_MaintainsSquareAspectRatioAndResponsiveMeasure()
    {
        RunInSta(() =>
        {
            var control = new KnightLabChess.App.Controls.ChessBoardControl();
            var vm = new ChessBoardViewModel();
            control.DataContext = vm;

            // 1. Constrained by width < height (e.g. 700 width, 900 height)
            var size1 = control.CalculateTargetBoardSize(new System.Windows.Size(700, 900));
            Assert.Equal(700, size1.Width);
            Assert.Equal(700, size1.Height);

            // 2. Constrained by height < width (e.g. 1000 width, 650 height)
            var size2 = control.CalculateTargetBoardSize(new System.Windows.Size(1000, 650));
            Assert.Equal(650, size2.Width);
            Assert.Equal(650, size2.Height);

            // 3. Constrained below MinBoardSize (e.g. 300 width, 300 height with MinBoardSize 380)
            var size3 = control.CalculateTargetBoardSize(new System.Windows.Size(300, 300));
            Assert.Equal(380, size3.Width);
            Assert.Equal(380, size3.Height);

            // 4. Unconstrained (infinite bounds fallback to 600)
            var sizeInf = control.CalculateTargetBoardSize(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(600, sizeInf.Width);
            Assert.Equal(600, sizeInf.Height);

            // 5. Measure and Arrange lifecycle produces square dimensions
            control.Measure(new System.Windows.Size(720, 900));
            Assert.Equal(720, control.DesiredSize.Width);
            Assert.Equal(720, control.DesiredSize.Height);

            control.Arrange(new System.Windows.Rect(0, 0, 720, 720));
            Assert.Equal(720, control.RenderSize.Width);
            Assert.Equal(720, control.RenderSize.Height);
        });
    }

    [Fact]
    public void Part3_ClickToMove_DispatchesMoveExecuted_WithIsUserDragFalse()
    {
        var vm = new ChessBoardViewModel();
        MoveExecutedEventArgs? dispatchedArgs = null;
        vm.MoveExecuted += (_, args) => dispatchedArgs = args;

        var e2 = GetSquare(vm, "e2");
        var e4 = GetSquare(vm, "e4");

        vm.SelectSquare(e2);
        vm.SelectSquare(e4);

        Assert.NotNull(dispatchedArgs);
        Assert.False(dispatchedArgs!.IsUserDrag);
        Assert.Equal("e2", dispatchedArgs.Move.From.ToString());
        Assert.Equal("e4", dispatchedArgs.Move.To.ToString());
        Assert.Equal("e4", dispatchedArgs.Move.San);
    }

    [Fact]
    public void Part3_DragAndDrop_DispatchesMoveExecuted_WithIsUserDragTrue()
    {
        var vm = new ChessBoardViewModel();
        MoveExecutedEventArgs? dispatchedArgs = null;
        vm.MoveExecuted += (_, args) => dispatchedArgs = args;

        var d2 = GetSquare(vm, "d2");
        var d4 = GetSquare(vm, "d4");

        bool moved = vm.TryExecuteMove(d2, d4, isUserDrag: true);

        Assert.True(moved);
        Assert.NotNull(dispatchedArgs);
        Assert.True(dispatchedArgs!.IsUserDrag);
        Assert.Equal("d2", dispatchedArgs.Move.From.ToString());
        Assert.Equal("d4", dispatchedArgs.Move.To.ToString());
    }

    [Fact]
    public void Part3_EngineMakeMove_DispatchesMoveExecuted_WithIsUserDragFalse()
    {
        var vm = new ChessBoardViewModel();
        MoveExecutedEventArgs? dispatchedArgs = null;
        vm.MoveExecuted += (_, args) => dispatchedArgs = args;

        bool moved = vm.MakeMove("e2e4");

        Assert.True(moved);
        Assert.NotNull(dispatchedArgs);
        Assert.False(dispatchedArgs!.IsUserDrag);
        Assert.Equal("e2", dispatchedArgs.Move.From.ToString());
        Assert.Equal("e4", dispatchedArgs.Move.To.ToString());
    }

    [Fact]
    public void Part3_IllegalMove_DoesNotDispatchMoveExecuted_NorMutateBoard()
    {
        var vm = new ChessBoardViewModel();
        bool eventFired = false;
        vm.MoveExecuted += (_, _) => eventFired = true;

        var e2 = GetSquare(vm, "e2");
        var e5 = GetSquare(vm, "e5"); // Illegal 3-square jump

        bool moved = vm.TryExecuteMove(e2, e5, isUserDrag: true);

        Assert.False(moved);
        Assert.False(eventFired);
        Assert.True(vm.IsWhiteTurn);
        Assert.True(GetSquare(vm, "e2").HasPiece);
        Assert.False(GetSquare(vm, "e5").HasPiece);
    }

    [Fact]
    public void Part3_BoardRefreshed_FiresOnUndoFlipAndNewGame()
    {
        var vm = new ChessBoardViewModel();
        int refreshCount = 0;
        vm.BoardRefreshed += (_, _) => refreshCount++;

        // 1. Move and Undo
        vm.TryExecuteMove(GetSquare(vm, "e2"), GetSquare(vm, "e4"));
        vm.UndoMove();
        Assert.Equal(1, refreshCount);

        // 2. Flip board
        vm.FlipBoard();
        Assert.Equal(2, refreshCount);

        // 3. New game
        vm.NewGame();
        Assert.Equal(3, refreshCount);
    }

    [Fact]
    public void Part3_CastlingMove_MoveRecordContainsCastlingMetadata()
    {
        var vm = new ChessBoardViewModel();
        // Setup position for White kingside castle: clear e2-e4, e7-e5, g1-f3, b8-c6, f1-c4, g8-f6
        vm.MakeMove("e2e4");
        vm.MakeMove("e7e5");
        vm.MakeMove("g1f3");
        vm.MakeMove("b8c6");
        vm.MakeMove("f1c4");
        vm.MakeMove("g8f6");

        MoveExecutedEventArgs? castleArgs = null;
        vm.MoveExecuted += (_, args) => castleArgs = args;

        // Execute Kingside castle e1 -> g1
        var e1 = GetSquare(vm, "e1");
        var g1 = GetSquare(vm, "g1");
        bool moved = vm.TryExecuteMove(e1, g1, isUserDrag: false);

        Assert.True(moved);
        Assert.NotNull(castleArgs);
        Assert.True(castleArgs!.Move.IsCastling);
        Assert.Equal("O-O", castleArgs.Move.San);

        // Verify King on g1, Rook on f1
        Assert.True(GetSquare(vm, "g1").HasPiece);
        Assert.Equal("WhiteKingDrawing", GetSquare(vm, "g1").PieceKey);
        Assert.True(GetSquare(vm, "f1").HasPiece);
        Assert.Equal("WhiteRookDrawing", GetSquare(vm, "f1").PieceKey);
    }

    [Fact]
    public void Part3_CaptureMove_MoveRecordContainsCapturedPieceMetadata()
    {
        var vm = new ChessBoardViewModel();
        vm.MakeMove("e2e4");
        vm.MakeMove("d7d5");

        MoveExecutedEventArgs? captureArgs = null;
        vm.MoveExecuted += (_, args) => captureArgs = args;

        var e4 = GetSquare(vm, "e4");
        var d5 = GetSquare(vm, "d5");
        bool captured = vm.TryExecuteMove(e4, d5, isUserDrag: true);

        Assert.True(captured);
        Assert.NotNull(captureArgs);
        Assert.NotNull(captureArgs!.Move.CapturedPiece);
        Assert.Equal(PieceType.Pawn, captureArgs.Move.CapturedPiece!.Type);
        Assert.Equal(PieceColor.Black, captureArgs.Move.CapturedPiece!.Color);
    }

    [Fact]
    public void Part3_PawnPromotion_MoveRecordContainsPromotionMetadata()
    {
        // Setup position with pawn on 7th rank ready to promote
        var game = new GeraChessGameAdapter("8/4P3/8/8/8/8/8/4K2k w - - 0 1");
        var vm = new ChessBoardViewModel(game);
        vm.PromptPromotion = (_) => PieceType.Queen;

        MoveExecutedEventArgs? promoArgs = null;
        vm.MoveExecuted += (_, args) => promoArgs = args;

        var e7 = GetSquare(vm, "e7");
        var e8 = GetSquare(vm, "e8");
        bool moved = vm.TryExecuteMove(e7, e8, isUserDrag: false);

        Assert.True(moved);
        Assert.NotNull(promoArgs);
        Assert.True(promoArgs!.Move.IsPromotion);
        Assert.Equal(PieceType.Queen, promoArgs.Move.Promotion);
        Assert.Equal("WhiteQueenDrawing", GetSquare(vm, "e8").PieceKey);
    }

    [Fact]
    public void Part3_UI_ChessBoardControl_LifecycleAndAnimationCancellation()
    {
        RunInSta(() =>
        {
            var control = new KnightLabChess.App.Controls.ChessBoardControl();
            var vm = new ChessBoardViewModel();
            control.DataContext = vm;

            var e4 = GetSquare(vm, "e4");
            e4.IsBeingAnimated = true;
            Assert.True(e4.IsBeingAnimated);

            // Invoking private StopCurrentAnimation via reflection
            var stopMethod = typeof(KnightLabChess.App.Controls.ChessBoardControl)
                .GetMethod("StopCurrentAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(stopMethod);
            stopMethod!.Invoke(control, null);

            // Square must be cleanly unflagged
            Assert.False(e4.IsBeingAnimated);
        });
    }

    [Fact]
    public void Part3_ShowMoveAnimations_Property_TogglesStateCorrectly()
    {
        RunInSta(() =>
        {
            var control = new KnightLabChess.App.Controls.ChessBoardControl();
            var vm = new ChessBoardViewModel();
            control.DataContext = vm;

            Assert.True(control.ShowMoveAnimations);
            Assert.True(vm.ShowMoveAnimations);

            control.ShowMoveAnimations = false;
            Assert.False(control.ShowMoveAnimations);

            vm.ShowMoveAnimations = false;
            Assert.False(vm.ShowMoveAnimations);
        });
    }
}
