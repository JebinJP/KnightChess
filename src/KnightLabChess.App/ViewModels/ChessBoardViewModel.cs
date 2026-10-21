using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Models;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// Represents a single square on the visual chess board.
/// </summary>
public partial class BoardSquareViewModel : ObservableObject
{
    [ObservableProperty] private int _file;
    [ObservableProperty] private int _rank;
    [ObservableProperty] private string? _pieceSymbol;
    [ObservableProperty] private bool _isLightSquare;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isLegalMove;
    [ObservableProperty] private bool _isLastMoveFrom;
    [ObservableProperty] private bool _isLastMoveTo;
    [ObservableProperty] private bool _isCheck;
    [ObservableProperty] private bool _hasPiece;
    [ObservableProperty] private bool _isWhitePiece;
    [ObservableProperty] private string? _pieceKey;

    public string SquareName => $"{(char)('a' + File)}{Rank + 1}";
}

/// <summary>
/// Represents a move in the move history display.
/// </summary>
public partial class MoveHistoryItem : ObservableObject
{
    [ObservableProperty] private int _moveNumber;
    [ObservableProperty] private string _whiteSan = string.Empty;
    [ObservableProperty] private string? _blackSan;
    [ObservableProperty] private bool _isCurrentMove;
}

/// <summary>
/// Core chess board ViewModel — manages visual board state, move execution, legal move highlights,
/// turn management, draw claims, and full game state synchronization.
/// Reusable across Play, Analysis, and Lesson views.
/// </summary>
public partial class ChessBoardViewModel : ObservableObject
{
    private IChessGame _game;

    [ObservableProperty] private ObservableCollection<BoardSquareViewModel> _squares = [];
    [ObservableProperty] private ObservableCollection<MoveHistoryItem> _moveHistory = [];
    [ObservableProperty] private bool _isFlipped;
    [ObservableProperty] private string _statusText = "White to move";
    [ObservableProperty] private bool _isGameOver;
    [ObservableProperty] private string? _gameResultText;
    [ObservableProperty] private BoardSquareViewModel? _selectedSquare;
    [ObservableProperty] private string _currentFen = string.Empty;
    [ObservableProperty] private bool _isWhiteTurn = true;
    [ObservableProperty] private bool _isInCheck;
    [ObservableProperty] private bool _canClaimDraw;
    [ObservableProperty] private string? _claimableDrawText;
    [ObservableProperty] private ObservableCollection<string> _capturedWhitePieces = [];
    [ObservableProperty] private ObservableCollection<string> _capturedBlackPieces = [];
    [ObservableProperty] private bool _isInputDisabled;
    [ObservableProperty] private IReadOnlyList<string>? _allowedMoves;

    /// <summary>
    /// Fired when a legal move is made (provides SAN notation).
    /// </summary>
    public event EventHandler<string>? MoveMade;

    /// <summary>
    /// Fired when the game ends.
    /// </summary>
    public event EventHandler<string>? GameEnded;

    /// <summary>
    /// Function to prompt user for promotion piece when pawn reaches 8th/1st rank.
    /// Returns the selected PieceType.
    /// </summary>
    public Func<PieceColor, PieceType>? PromptPromotion { get; set; }

    public ChessBoardViewModel() : this(new GeraChessGameAdapter())
    {
    }

    public ChessBoardViewModel(IChessGame game)
    {
        _game = game;
        InitializeSquares();
        UpdateBoardDisplay();
    }

    public IChessGame Game => _game;

    private void InitializeSquares()
    {
        Squares.Clear();
        // Standard visual display: rank 8 down to 1 (top to bottom), file a to h (left to right)
        for (int rank = 7; rank >= 0; rank--)
        {
            for (int file = 0; file < 8; file++)
            {
                Squares.Add(new BoardSquareViewModel
                {
                    File = file,
                    Rank = rank,
                    IsLightSquare = (file + rank) % 2 != 0
                });
            }
        }
    }

    /// <summary>
    /// Resets the board to standard starting position.
    /// </summary>
    [RelayCommand]
    public void NewGame()
    {
        _game.ResetToStartingPosition();
        IsGameOver = false;
        GameResultText = null;
        SelectedSquare = null;
        MoveHistory.Clear();
        UpdateBoardDisplay();
    }

    /// <summary>
    /// Flips the board perspective between White and Black.
    /// </summary>
    [RelayCommand]
    public void FlipBoard()
    {
        IsFlipped = !IsFlipped;
        UpdateBoardDisplay();
    }

    /// <summary>
    /// Undoes the last halfmove.
    /// </summary>
    [RelayCommand]
    public void UndoMove()
    {
        if (IsInputDisabled) return;

        bool undone = _game.UndoLastMove();
        if (undone)
        {
            SelectedSquare = null;
            UpdateBoardDisplay();
        }
    }

    /// <summary>
    /// Claims a draw when threefold repetition or 50-move rule is available.
    /// </summary>
    [RelayCommand]
    public void ClaimDraw()
    {
        if (!CanClaimDraw || IsGameOver) return;

        bool claimed = _game.ClaimDraw();
        if (claimed)
        {
            UpdateBoardDisplay();
        }
    }

    /// <summary>
    /// Resigns the active game for the current player.
    /// </summary>
    [RelayCommand]
    public void Resign()
    {
        if (IsGameOver) return;

        _game.Resign(_game.ActiveTurn);
        UpdateBoardDisplay();
    }

    /// <summary>
    /// Handles user click on a square.
    /// </summary>
    [RelayCommand]
    public void SelectSquare(BoardSquareViewModel square)
    {
        if (IsInputDisabled || IsGameOver) return;

        // If no square currently selected
        if (SelectedSquare is null)
        {
            // Only select if there is a piece belonging to the active player
            if (square.HasPiece && square.IsWhitePiece == _game.IsWhiteTurn)
            {
                SelectedSquare = square;
                square.IsSelected = true;
                HighlightLegalMoves(square);
            }
            return;
        }

        // If clicking on the already selected square, deselect
        if (SelectedSquare == square)
        {
            ClearSelection();
            return;
        }

        // If clicking on another friendly piece, change selection
        if (square.HasPiece && square.IsWhitePiece == _game.IsWhiteTurn)
        {
            ClearSelection();
            SelectedSquare = square;
            square.IsSelected = true;
            HighlightLegalMoves(square);
            return;
        }

        // Attempting to move from SelectedSquare to clicked square
        var from = new BoardSquare(SelectedSquare.File, SelectedSquare.Rank);
        var to = new BoardSquare(square.File, square.Rank);

        // Check if move is a pawn promotion
        PieceType? promo = null;
        var piece = _game.GetPieceAt(from);
        if (piece?.Type == PieceType.Pawn && (to.Rank == 7 || to.Rank == 0))
        {
            promo = PromptPromotion is not null
                ? PromptPromotion(piece.Color)
                : PieceType.Queen;
        }

        if (_game.IsLegalMove(from, to, promo))
        {
            var moveResult = _game.MakeMove(from, to, promo);
            ClearSelection();

            if (moveResult.Success)
            {
                UpdateBoardDisplay();
                MoveMade?.Invoke(this, moveResult.Move!.San);
            }
        }
        else
        {
            ClearSelection();
        }
    }

    /// <summary>
    /// Programmatically executes a move using SAN or UCI notation (e.g. from an engine or tutorial).
    /// </summary>
    public bool MakeMove(string moveText)
    {
        if (IsGameOver) return false;

        MoveResult moveResult;
        if (moveText.Length is 4 or 5 &&
            char.IsLetter(moveText[0]) && char.IsDigit(moveText[1]) &&
            char.IsLetter(moveText[2]) && char.IsDigit(moveText[3]))
        {
            var from = new BoardSquare(moveText[0] - 'a', moveText[1] - '1');
            var to = new BoardSquare(moveText[2] - 'a', moveText[3] - '1');
            PieceType? promo = moveText.Length == 5 ? moveText[4] switch
            {
                'q' => PieceType.Queen,
                'r' => PieceType.Rook,
                'b' => PieceType.Bishop,
                'n' => PieceType.Knight,
                _ => null
            } : null;

            moveResult = _game.MakeMove(from, to, promo);
        }
        else
        {
            moveResult = _game.MakeMove(moveText);
        }

        if (moveResult.Success)
        {
            ClearSelection();
            UpdateBoardDisplay();
            MoveMade?.Invoke(this, moveResult.Move?.San ?? moveText);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Loads a position from FEN string.
    /// </summary>
    public void LoadFen(string fen)
    {
        _game.LoadFromPosition(fen, []);
        SelectedSquare = null;
        UpdateBoardDisplay();
    }

    /// <summary>
    /// Loads a game from starting FEN and sequential moves.
    /// </summary>
    public void LoadGame(string startFen, IEnumerable<string> moves)
    {
        _game.LoadFromPosition(startFen, moves);
        SelectedSquare = null;
        UpdateBoardDisplay();
    }

    /// <summary>
    /// Undoes the last move played on the board and updates the display.
    /// </summary>
    public bool UndoLastMove()
    {
        if (_game.UndoLastMove())
        {
            SelectedSquare = null;
            UpdateBoardDisplay();
            return true;
        }
        return false;
    }

    private void HighlightLegalMoves(BoardSquareViewModel square)
    {
        var from = new BoardSquare(square.File, square.Rank);
        var destinations = _game.GetLegalDestinations(from);
        var destSet = new HashSet<BoardSquare>(destinations);

        foreach (var sq in Squares)
        {
            var boardSq = new BoardSquare(sq.File, sq.Rank);
            sq.IsLegalMove = destSet.Contains(boardSq);
        }
    }

    private void ClearSelection()
    {
        if (SelectedSquare is not null)
        {
            SelectedSquare.IsSelected = false;
            SelectedSquare = null;
        }

        foreach (var sq in Squares)
        {
            sq.IsLegalMove = false;
        }
    }

    private void UpdateBoardDisplay()
    {
        CurrentFen = _game.CurrentFen;
        IsWhiteTurn = _game.IsWhiteTurn;
        IsInCheck = _game.IsInCheck;
        IsGameOver = _game.IsGameOver;
        CanClaimDraw = _game.CanClaimDraw;

        ClaimableDrawText = _game.AvailableClaimableDraw switch
        {
            ClaimableDrawType.ThreefoldRepetition => "Threefold repetition available",
            ClaimableDrawType.FiftyMoveRule => "50-move rule available",
            _ => null
        };

        // Clear square states
        foreach (var sq in Squares)
        {
            sq.IsLastMoveFrom = false;
            sq.IsLastMoveTo = false;
            sq.IsCheck = false;
        }

        // Update piece positions based on board orientation
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                int displayRank = IsFlipped ? rank : (7 - rank);
                int displayFile = IsFlipped ? (7 - file) : file;
                int index = (7 - displayRank) * 8 + displayFile;

                if (index < 0 || index >= Squares.Count) continue;

                var sq = Squares[index];
                sq.File = file;
                sq.Rank = rank;
                sq.IsLightSquare = (file + rank) % 2 != 0;

                var piece = _game.GetPieceAt(file, rank);
                if (piece is not null)
                {
                    sq.HasPiece = true;
                    sq.IsWhitePiece = piece.Color == PieceColor.White;
                    sq.PieceKey = $"{(piece.Color == PieceColor.White ? "White" : "Black")}{piece.Type}Geometry";
                    sq.PieceSymbol = piece.Symbol;
                }
                else
                {
                    sq.HasPiece = false;
                    sq.IsWhitePiece = false;
                    sq.PieceKey = null;
                    sq.PieceSymbol = null;
                }
            }
        }

        // Highlight last move from and to squares
        if (_game.MoveHistory.Count > 0)
        {
            var lastMove = _game.MoveHistory[^1];
            foreach (var sq in Squares)
            {
                if (sq.File == lastMove.From.File && sq.Rank == lastMove.From.Rank)
                    sq.IsLastMoveFrom = true;
                if (sq.File == lastMove.To.File && sq.Rank == lastMove.To.Rank)
                    sq.IsLastMoveTo = true;
            }
        }

        // Highlight checked King
        if (IsInCheck)
        {
            var checkedColor = _game.ActiveTurn;
            foreach (var sq in Squares)
            {
                var piece = _game.GetPieceAt(sq.File, sq.Rank);
                if (piece?.Type == PieceType.King && piece.Color == checkedColor)
                {
                    sq.IsCheck = true;
                    break;
                }
            }
        }

        // Synchronize Move History Log
        UpdateMoveHistory();

        // Update Captured Pieces
        UpdateCapturedPieces();

        // Update Game Status Text
        UpdateStatusText();
    }

    private void UpdateMoveHistory()
    {
        MoveHistory.Clear();
        var moves = _game.MoveHistory;

        for (int i = 0; i < moves.Count; i += 2)
        {
            var item = new MoveHistoryItem
            {
                MoveNumber = (i / 2) + 1,
                WhiteSan = moves[i].San,
                BlackSan = (i + 1 < moves.Count) ? moves[i + 1].San : null,
                IsCurrentMove = (i + 1 >= moves.Count)
            };
            MoveHistory.Add(item);
        }
    }

    private void UpdateCapturedPieces()
    {
        CapturedWhitePieces.Clear();
        CapturedBlackPieces.Clear();

        foreach (var move in _game.MoveHistory)
        {
            if (move.CapturedPiece is not null)
            {
                if (move.CapturedPiece.Color == PieceColor.White)
                    CapturedWhitePieces.Add(move.CapturedPiece.Symbol);
                else
                    CapturedBlackPieces.Add(move.CapturedPiece.Symbol);
            }
        }
    }

    private void UpdateStatusText()
    {
        if (_game.IsGameOver)
        {
            GameResultText = _game.GameEndMessage ?? "Game Over";
            StatusText = GameResultText;
            GameEnded?.Invoke(this, GameResultText);
        }
        else if (IsInCheck)
        {
            StatusText = $"{(_game.IsWhiteTurn ? "White" : "Black")} is in check!";
        }
        else
        {
            StatusText = $"{(_game.IsWhiteTurn ? "White" : "Black")} to move";
        }
    }
}
