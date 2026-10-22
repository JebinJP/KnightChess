using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KnightLabChess.App.ViewModels;
using KnightLabChess.App.Views.Dialogs;

namespace KnightLabChess.App.Controls;

/// <summary>
/// Interaction logic for ChessBoardControl.xaml.
/// Implements dual-mode interaction: legal drag-and-drop piece movement and click-click selection.
/// Managed via an explicit drag-and-drop state machine (Idle -> Pending -> Dragging).
/// </summary>
public partial class ChessBoardControl : UserControl
{
    private enum DragState
    {
        Idle,      // No drag interaction in progress
        Pending,   // Mouse down on a valid piece, waiting for drag threshold
        Dragging   // Pointer moved beyond threshold, active piece drag in progress
    }

    private DragState _dragState = DragState.Idle;
    private BoardSquareViewModel? _sourceSquare;
    private Point _dragStartPoint;
    private Point _grabOffset;
    private bool _isReleasingCapture;

    private const double DragThreshold = 6.0;

    public static readonly DependencyProperty MinBoardSizeProperty =
        DependencyProperty.Register(
            nameof(MinBoardSize),
            typeof(double),
            typeof(ChessBoardControl),
            new PropertyMetadata(380.0, (d, e) =>
            {
                if (d is ChessBoardControl c && e.NewValue is double val)
                {
                    c.MinWidth = val;
                    c.MinHeight = val;
                }
            }));

    public double MinBoardSize
    {
        get => (double)GetValue(MinBoardSizeProperty);
        set => SetValue(MinBoardSizeProperty, value);
    }

    public ChessBoardControl()
    {
        InitializeComponent();
        MinWidth = MinBoardSize;
        MinHeight = MinBoardSize;
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => CleanupDrag(true, "Unloaded");
    }

    public Size CalculateTargetBoardSize(Size constraint)
    {
        double availableW = constraint.Width;
        double availableH = constraint.Height;

        if (double.IsInfinity(availableW) && double.IsInfinity(availableH))
        {
            availableW = 600.0;
            availableH = 600.0;
        }
        else if (double.IsInfinity(availableW))
        {
            availableW = availableH;
        }
        else if (double.IsInfinity(availableH))
        {
            availableH = availableW;
        }

        double boardSize = Math.Max(MinBoardSize, Math.Min(availableW, availableH));
        return new Size(boardSize, boardSize);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var squareSize = CalculateTargetBoardSize(constraint);
        base.MeasureOverride(squareSize);
        return squareSize;
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var squareSize = CalculateTargetBoardSize(arrangeBounds);
        base.ArrangeOverride(squareSize);
        return squareSize;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        CleanupDrag(true, "DataContextChanged");

        if (e.NewValue is ChessBoardViewModel vm && vm.PromptPromotion is null)
        {
            vm.PromptPromotion = (color) =>
            {
                var parentWindow = Window.GetWindow(this);
                var dlg = new PromotionDialog(color);
                if (parentWindow != null)
                {
                    dlg.Owner = parentWindow;
                }
                var result = dlg.ShowDialog();
                return result == true ? dlg.SelectedPieceType : null;
            };
        }
    }

    private ImageSource? ResolvePieceImage(string? pieceKey)
    {
        if (string.IsNullOrEmpty(pieceKey)) return null;

        var res = TryFindResource(pieceKey) ?? Application.Current?.TryFindResource(pieceKey);
        return res switch
        {
            DrawingImage di => di,
            Drawing d => new DrawingImage(d),
            ImageSource isrc => isrc,
            _ => null
        };
    }

    private BoardSquareViewModel? GetSquareAtPoint(ChessBoardViewModel vm, Point pt)
    {
        double boardWidth = SquaresItemsControl.ActualWidth > 0 ? SquaresItemsControl.ActualWidth : (double.IsNaN(SquaresItemsControl.Width) ? 560.0 : SquaresItemsControl.Width);
        double boardHeight = SquaresItemsControl.ActualHeight > 0 ? SquaresItemsControl.ActualHeight : (double.IsNaN(SquaresItemsControl.Height) ? 560.0 : SquaresItemsControl.Height);

        if (boardWidth <= 0 || boardHeight <= 0) return null;
        if (pt.X < 0 || pt.X >= boardWidth || pt.Y < 0 || pt.Y >= boardHeight) return null;

        double sqWidth = boardWidth / 8.0;
        double sqHeight = boardHeight / 8.0;

        int col = (int)Math.Floor(pt.X / sqWidth);
        int row = (int)Math.Floor(pt.Y / sqHeight);

        if (col >= 0 && col < 8 && row >= 0 && row < 8)
        {
            int idx = row * 8 + col;
            if (idx >= 0 && idx < vm.Squares.Count)
            {
                return vm.Squares[idx];
            }
        }

        return null;
    }

    private void Board_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Reset any stale drag state
        CleanupDrag(false, "NewMouseDown");

        if (DataContext is not ChessBoardViewModel vm || vm.IsInputDisabled || vm.IsGameOver)
            return;

        var pt = e.GetPosition(SquaresItemsControl);
        var sq = GetSquareAtPoint(vm, pt);

        Debug.WriteLine($"[KnightLab DND] PreviewMouseDown: pt=({pt.X:F1},{pt.Y:F1}), square={sq?.SquareName ?? "none"}, hasPiece={sq?.HasPiece}, isWhite={sq?.IsWhitePiece}, activeTurn={vm.Game.ActiveTurn}");

        if (sq != null && sq.HasPiece && sq.IsWhitePiece == vm.Game.IsWhiteTurn)
        {
            _dragState = DragState.Pending;
            _sourceSquare = sq;
            _dragStartPoint = pt;

            double boardWidth = SquaresItemsControl.ActualWidth > 0 ? SquaresItemsControl.ActualWidth : 560.0;
            double boardHeight = SquaresItemsControl.ActualHeight > 0 ? SquaresItemsControl.ActualHeight : 560.0;
            double sqWidth = boardWidth / 8.0;
            double sqHeight = boardHeight / 8.0;

            int col = (int)Math.Floor(pt.X / sqWidth);
            int row = (int)Math.Floor(pt.Y / sqHeight);

            double sqCenterX = col * sqWidth + sqWidth / 2.0;
            double sqCenterY = row * sqHeight + sqHeight / 2.0;
            _grabOffset = new Point(pt.X - sqCenterX, pt.Y - sqCenterY);
        }
    }

    private void Board_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragState == DragState.Idle)
            return;

        var source = _sourceSquare;
        if (source == null || DataContext is not ChessBoardViewModel vm)
        {
            CleanupDrag(false, "NullSourceOrVm");
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CleanupDrag(true, "LeftButtonReleased");
            return;
        }

        var currentPoint = e.GetPosition(SquaresItemsControl);

        if (_dragState == DragState.Pending)
        {
            var delta = currentPoint - _dragStartPoint;
            if (delta.Length > DragThreshold)
            {
                if (!source.HasPiece)
                {
                    CleanupDrag(false, "SourcePieceVanished");
                    return;
                }

                _dragState = DragState.Dragging;
                bool captured = BoardContainerGrid.CaptureMouse();
                Debug.WriteLine($"[KnightLab DND] Transition to Dragging: piece={source.PieceKey} at {source.SquareName}, delta={delta.Length:F1}, captureSucceeded={captured}");

                source.IsBeingDragged = true;

                if (vm.SelectedSquare != source)
                {
                    vm.SelectSquare(source);
                }

                var img = ResolvePieceImage(source.PieceKey);
                if (img != null)
                {
                    GhostPiece.Source = img;
                    double sqSize = (SquaresItemsControl.ActualWidth > 0 ? SquaresItemsControl.ActualWidth : 560.0) / 8.0;
                    GhostPiece.Width = Math.Max(48, sqSize * 0.92);
                    GhostPiece.Height = Math.Max(48, sqSize * 0.92);
                    GhostPiece.Visibility = Visibility.Visible;

                    UpdateGhostPosition(currentPoint);
                }
            }
        }
        else if (_dragState == DragState.Dragging)
        {
            UpdateGhostPosition(currentPoint);
        }
    }

    private void UpdateGhostPosition(Point currentPoint)
    {
        double centerX = currentPoint.X - _grabOffset.X;
        double centerY = currentPoint.Y - _grabOffset.Y;
        Canvas.SetLeft(GhostPiece, centerX - GhostPiece.Width / 2.0);
        Canvas.SetTop(GhostPiece, centerY - GhostPiece.Height / 2.0);
    }

    private void Board_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragState == DragState.Dragging && DataContext is ChessBoardViewModel vm)
        {
            e.Handled = true;

            var source = _sourceSquare;
            var dropPt = e.GetPosition(SquaresItemsControl);
            var targetSq = GetSquareAtPoint(vm, dropPt);

            Debug.WriteLine($"[KnightLab DND] PreviewMouseLeftButtonUp: source={source?.SquareName}, target={targetSq?.SquareName ?? "null (outside)"}, dropPt=({dropPt.X:F1},{dropPt.Y:F1})");

            _isReleasingCapture = true;
            try
            {
                if (BoardContainerGrid.IsMouseCaptured)
                {
                    BoardContainerGrid.ReleaseMouseCapture();
                }
            }
            finally
            {
                _isReleasingCapture = false;
            }

            GhostPiece.Visibility = Visibility.Collapsed;
            GhostPiece.Source = null;

            if (source != null)
            {
                source.IsBeingDragged = false;
            }

            if (source != null && targetSq != null)
            {
                if (targetSq != source)
                {
                    bool moved = vm.TryExecuteMove(source, targetSq);
                    Debug.WriteLine($"[KnightLab DND] Move execution ({source.SquareName} -> {targetSq.SquareName}): moved={moved}");

                    if (!moved && targetSq.HasPiece && targetSq.IsWhitePiece == vm.Game.IsWhiteTurn)
                    {
                        vm.SelectSquare(targetSq);
                    }
                }
                else
                {
                    Debug.WriteLine($"[KnightLab DND] Dropped back onto source square {source.SquareName}; preserving selection.");
                }
            }
            else
            {
                Debug.WriteLine("[KnightLab DND] Dropped outside board; clearing selection.");
                vm.ClearSelection();
            }

            CleanupDrag(false, "MoveExecutedOrCancelled");
        }
        else
        {
            if (_dragState == DragState.Pending)
            {
                Debug.WriteLine($"[KnightLab DND] Click without drag on {_sourceSquare?.SquareName}; allowing Button click to proceed.");
            }
            CleanupDrag(false, "ClickWithoutDragOrIdle");
        }
    }

    private void CleanupDrag(bool releaseCapture, string reason)
    {
        try
        {
            if (_sourceSquare != null)
            {
                _sourceSquare.IsBeingDragged = false;
            }

            GhostPiece.Visibility = Visibility.Collapsed;
            GhostPiece.Source = null;

            if (releaseCapture && BoardContainerGrid.IsMouseCaptured)
            {
                _isReleasingCapture = true;
                try
                {
                    BoardContainerGrid.ReleaseMouseCapture();
                }
                finally
                {
                    _isReleasingCapture = false;
                }
            }
        }
        finally
        {
            if (_dragState != DragState.Idle)
            {
                Debug.WriteLine($"[KnightLab DND] CleanupDrag: reason={reason}, previousState={_dragState}");
            }
            _dragState = DragState.Idle;
            _sourceSquare = null;
        }
    }

    private void Board_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isReleasingCapture) return;

        // CRITICAL FIX: Only clean up if BoardContainerGrid itself lost mouse capture.
        // Because LostMouseCapture is a bubbling event, when a child Button loses capture
        // (which happens when BoardContainerGrid.CaptureMouse() is called upon dragging),
        // that event bubbles to BoardContainerGrid. We MUST NOT abort our drag for child elements!
        if (!ReferenceEquals(e.OriginalSource, BoardContainerGrid))
        {
            Debug.WriteLine($"[KnightLab DND] Child element {e.OriginalSource?.GetType().Name} lost capture; keeping board capture active.");
            return;
        }

        if (_dragState != DragState.Idle)
        {
            Debug.WriteLine($"[KnightLab DND] BoardContainerGrid lost mouse capture externally (State={_dragState}). Cleaning up drag.");
            CleanupDrag(false, "LostMouseCaptureOnBoardContainerGrid");
        }
    }

    private void Board_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_dragState != DragState.Idle)
        {
            Debug.WriteLine($"[KnightLab DND] Right click detected; cancelling drag. (State was {_dragState})");
            if (DataContext is ChessBoardViewModel vm)
            {
                vm.ClearSelection();
            }
            CleanupDrag(true, "RightClickCancel");
            e.Handled = true;
        }
    }

    private void Board_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _dragState != DragState.Idle)
        {
            Debug.WriteLine($"[KnightLab DND] Escape key pressed; cancelling drag. (State was {_dragState})");
            if (DataContext is ChessBoardViewModel vm)
            {
                vm.ClearSelection();
            }
            CleanupDrag(true, "EscapeKeyCancel");
            e.Handled = true;
        }
    }
}
