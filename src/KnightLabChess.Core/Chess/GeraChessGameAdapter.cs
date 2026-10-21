using KnightLabChess.Core.Models;
using ExternalChess = global::Chess;

namespace KnightLabChess.Core.Chess;

/// <summary>
/// Domain adapter wrapping Gera.Chess.ChessBoard.
/// Implements standard FIDE rules: distinguishes claimable draws (threefold, 50-move)
/// from automatic draws (fivefold, 75-move, stalemate, insufficient material),
/// handles special moves (castling, en passant, promotion), and ensures zero state mutation on illegal moves.
/// </summary>
public class GeraChessGameAdapter : IChessGame
{
    public const string DefaultStartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private ExternalChess.ChessBoard _board;
    private string _startFen = DefaultStartingFen;
    private readonly List<MoveRecord> _moveHistory = [];
    private readonly List<string> _moveHistoryUci = [];
    private readonly List<string> _positionHistory = [];
    private readonly Dictionary<string, int> _positionCounts = [];

    private bool _isGameOver;
    private GameResult _result = GameResult.InProgress;
    private GameEndReason _endReason = GameEndReason.None;
    private string? _gameEndMessage;
    private ClaimableDrawType _availableClaimableDraw = ClaimableDrawType.None;
    private AutomaticDrawType _activeAutomaticDraw = AutomaticDrawType.None;

    public GeraChessGameAdapter()
    {
        _board = new ExternalChess.ChessBoard
        {
            AutoEndgameRules = ExternalChess.AutoEndgameRules.InsufficientMaterial
        };
        InitializePositionTracking();
    }

    public GeraChessGameAdapter(string fen)
    {
        _startFen = fen;
        _board = ExternalChess.ChessBoard.LoadFromFen(fen, ExternalChess.AutoEndgameRules.InsufficientMaterial);
        InitializePositionTracking();
    }

    public string CurrentFen => _board.ToFen();
    public string StartFen => _startFen;

    public PieceColor ActiveTurn => _board.Turn == ExternalChess.PieceColor.White
        ? PieceColor.White
        : PieceColor.Black;

    public bool IsWhiteTurn => ActiveTurn == PieceColor.White;

    public bool IsInCheck => _board.Turn == ExternalChess.PieceColor.White
        ? _board.WhiteKingChecked
        : _board.BlackKingChecked;

    public bool IsGameOver => _isGameOver;
    public GameResult Result => _result;
    public GameEndReason EndReason => _endReason;
    public string? GameEndMessage => _gameEndMessage;

    public ClaimableDrawType AvailableClaimableDraw => _availableClaimableDraw;
    public bool CanClaimDraw => _availableClaimableDraw != ClaimableDrawType.None;
    public AutomaticDrawType ActiveAutomaticDraw => _activeAutomaticDraw;

    public IReadOnlyList<MoveRecord> MoveHistory => _moveHistory.AsReadOnly();
    public IReadOnlyList<string> MoveHistoryUci => _moveHistoryUci.AsReadOnly();

    public ChessPiece? GetPieceAt(BoardSquare square) => GetPieceAt(square.File, square.Rank);

    public ChessPiece? GetPieceAt(int file, int rank)
    {
        if (file < 0 || file > 7 || rank < 0 || rank > 7)
            return null;

        var squareName = $"{(char)('a' + file)}{rank + 1}";
        var piece = _board[squareName];
        if (piece is null)
            return null;

        return ToDomainPiece(piece);
    }

    public IReadOnlyList<BoardSquare> GetLegalDestinations(BoardSquare from)
    {
        if (_isGameOver || !from.IsValid)
            return [];

        var fromStr = from.AlgebraicNotation;
        var destinations = new HashSet<BoardSquare>();

        foreach (var move in _board.Moves())
        {
            if (move.OriginalPosition.ToString().ToLowerInvariant() == fromStr)
            {
                var destStr = move.NewPosition.ToString().ToLowerInvariant();
                destinations.Add(BoardSquare.FromAlgebraic(destStr));
            }
        }

        return destinations.ToList().AsReadOnly();
    }

    public bool IsLegalMove(BoardSquare from, BoardSquare to, PieceType? promotion = null)
    {
        if (_isGameOver || !from.IsValid || !to.IsValid)
            return false;

        return FindLegalMove(from, to, promotion) is not null;
    }

    public bool IsPromotionMove(BoardSquare from, BoardSquare to)
    {
        if (_isGameOver || !from.IsValid || !to.IsValid)
            return false;

        var piece = GetPieceAt(from);
        if (piece is null || piece.Type != PieceType.Pawn || piece.Color != ActiveTurn)
            return false;

        int promotionRank = piece.Color == PieceColor.White ? 7 : 0;
        if (to.Rank != promotionRank)
            return false;

        var fromStr = from.AlgebraicNotation;
        var toStr = to.AlgebraicNotation;

        return _board.Moves().Any(m =>
            m.OriginalPosition.ToString().ToLowerInvariant() == fromStr &&
            m.NewPosition.ToString().ToLowerInvariant() == toStr &&
            m.IsPromotion);
    }

    public MoveResult MakeMove(BoardSquare from, BoardSquare to, PieceType? promotion = null)
    {
        if (_isGameOver)
            return MoveResult.Failed("Game is already over.");

        var candidate = FindLegalMove(from, to, promotion);
        if (candidate is null)
            return MoveResult.Failed($"Illegal move from {from} to {to}.");

        return ExecuteMoveInternal(candidate, from, to, promotion);
    }

    public MoveResult MakeMove(string moveString)
    {
        if (_isGameOver)
            return MoveResult.Failed("Game is already over.");

        var trimmed = moveString.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return MoveResult.Failed("Empty move string.");

        // Check if string is UCI format (e.g., "e2e4", "e7e8q")
        if (trimmed.Length is 4 or 5 &&
            trimmed[0] >= 'a' && trimmed[0] <= 'h' &&
            trimmed[1] >= '1' && trimmed[1] <= '8' &&
            trimmed[2] >= 'a' && trimmed[2] <= 'h' &&
            trimmed[3] >= '1' && trimmed[3] <= '8')
        {
            var from = BoardSquare.FromAlgebraic(trimmed[..2]);
            var to = BoardSquare.FromAlgebraic(trimmed.Substring(2, 2));
            PieceType? promo = null;
            if (trimmed.Length == 5)
            {
                promo = char.ToLowerInvariant(trimmed[4]) switch
                {
                    'q' => PieceType.Queen,
                    'r' => PieceType.Rook,
                    'b' => PieceType.Bishop,
                    'n' => PieceType.Knight,
                    _ => null
                };
            }
            return MakeMove(from, to, promo);
        }

        // Try parsing via SAN from board.Moves()
        var sanMatch = _board.Moves().FirstOrDefault(m => string.Equals(m.San, trimmed, StringComparison.OrdinalIgnoreCase));
        if (sanMatch is not null)
        {
            var from = BoardSquare.FromAlgebraic(sanMatch.OriginalPosition.ToString().ToLowerInvariant());
            var to = BoardSquare.FromAlgebraic(sanMatch.NewPosition.ToString().ToLowerInvariant());
            PieceType? promo = sanMatch.IsPromotion && sanMatch.Promotion is not null
                ? ToDomainPieceType(sanMatch.Promotion.Type)
                : null;
            return ExecuteMoveInternal(sanMatch, from, to, promo);
        }

        return MoveResult.Failed($"Could not parse move: {moveString}");
    }

    public bool UndoLastMove()
    {
        if (_board.ExecutedMoves.Count == 0 || _moveHistory.Count == 0)
            return false;

        // Undo in chess engine
        _board.Cancel();

        // Roll back position counts
        if (_positionHistory.Count > 0)
        {
            var lastKey = _positionHistory[^1];
            _positionHistory.RemoveAt(_positionHistory.Count - 1);
            if (_positionCounts.TryGetValue(lastKey, out var count))
            {
                if (count <= 1)
                    _positionCounts.Remove(lastKey);
                else
                    _positionCounts[lastKey] = count - 1;
            }
        }

        // Remove from move logs
        if (_moveHistory.Count > 0) _moveHistory.RemoveAt(_moveHistory.Count - 1);
        if (_moveHistoryUci.Count > 0) _moveHistoryUci.RemoveAt(_moveHistoryUci.Count - 1);

        // Reset game-over state if match had finished on that move
        _isGameOver = false;
        _result = GameResult.InProgress;
        _endReason = GameEndReason.None;
        _gameEndMessage = null;

        // Re-evaluate draw and check states
        EvaluateDrawAndEndgameStates();
        return true;
    }

    public bool ClaimDraw()
    {
        if (_isGameOver || _availableClaimableDraw == ClaimableDrawType.None)
            return false;

        _isGameOver = true;
        _result = GameResult.Draw;
        _endReason = _availableClaimableDraw == ClaimableDrawType.ThreefoldRepetition
            ? GameEndReason.ThreefoldRepetition
            : GameEndReason.FiftyMoveRule;

        _gameEndMessage = _endReason == GameEndReason.ThreefoldRepetition
            ? "Draw by threefold repetition (claimed)."
            : "Draw by 50-move rule (claimed).";

        return true;
    }

    public bool Resign(PieceColor resigningPlayer)
    {
        if (_isGameOver)
            return false;

        _isGameOver = true;
        _result = resigningPlayer == PieceColor.White ? GameResult.BlackWins : GameResult.WhiteWins;
        _endReason = GameEndReason.Resignation;
        _gameEndMessage = $"{resigningPlayer} resigned. {(resigningPlayer == PieceColor.White ? "Black" : "White")} wins!";
        return true;
    }

    public void ResetToStartingPosition()
    {
        _startFen = DefaultStartingFen;
        _board = new ExternalChess.ChessBoard
        {
            AutoEndgameRules = ExternalChess.AutoEndgameRules.InsufficientMaterial
        };
        _moveHistory.Clear();
        _moveHistoryUci.Clear();
        _isGameOver = false;
        _result = GameResult.InProgress;
        _endReason = GameEndReason.None;
        _gameEndMessage = null;
        InitializePositionTracking();
    }

    public void LoadFromPosition(string startFen, IEnumerable<string> moves)
    {
        _startFen = string.IsNullOrWhiteSpace(startFen) ? DefaultStartingFen : startFen;
        _board = ExternalChess.ChessBoard.LoadFromFen(_startFen, ExternalChess.AutoEndgameRules.InsufficientMaterial);
        _moveHistory.Clear();
        _moveHistoryUci.Clear();
        _isGameOver = false;
        _result = GameResult.InProgress;
        _endReason = GameEndReason.None;
        _gameEndMessage = null;
        InitializePositionTracking();

        foreach (var move in moves)
        {
            var res = MakeMove(move);
            if (!res.Success)
            {
                throw new InvalidOperationException($"Failed to replay move '{move}': {res.ErrorMessage}");
            }
        }
    }

    public void LoadFromPgn(string pgn)
    {
        var loaded = ExternalChess.ChessBoard.LoadFromPgn(pgn);

        var movesList = loaded.ExecutedMoves.Select(m => m.San ?? m.ToString()).ToList();
        var startFen = loaded.LoadedFromFen ? loaded.Headers.GetValueOrDefault("FEN", DefaultStartingFen) : DefaultStartingFen;

        LoadFromPosition(startFen, movesList);
    }

    public string GetPgn(string whitePlayer = "White", string blackPlayer = "Black")
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Event \"KnightLab Chess Match\"]");
        sb.AppendLine($"[Site \"Local\"]");
        sb.AppendLine($"[Date \"{DateTime.UtcNow:yyyy.MM.dd}\"]");
        sb.AppendLine($"[Round \"1\"]");
        sb.AppendLine($"[White \"{whitePlayer}\"]");
        sb.AppendLine($"[Black \"{blackPlayer}\"]");

        var resultStr = _result switch
        {
            GameResult.WhiteWins => "1-0",
            GameResult.BlackWins => "0-1",
            GameResult.Draw => "1/2-1/2",
            _ => "*"
        };
        sb.AppendLine($"[Result \"{resultStr}\"]");

        if (_startFen != DefaultStartingFen)
        {
            sb.AppendLine("[SetUp \"1\"]");
            sb.AppendLine($"[FEN \"{_startFen}\"]");
        }

        sb.AppendLine();

        for (int i = 0; i < _moveHistory.Count; i++)
        {
            if (i % 2 == 0)
            {
                sb.Append($"{i / 2 + 1}. ");
            }
            sb.Append($"{_moveHistory[i].San} ");
        }

        sb.Append(resultStr);
        return sb.ToString();
    }

    private ExternalChess.Move? FindLegalMove(BoardSquare from, BoardSquare to, PieceType? promotion)
    {
        var fromStr = from.AlgebraicNotation;
        var toStr = to.AlgebraicNotation;

        return _board.Moves().FirstOrDefault(m =>
            m.OriginalPosition.ToString().ToLowerInvariant() == fromStr &&
            m.NewPosition.ToString().ToLowerInvariant() == toStr &&
            (m.IsPromotion
                ? (promotion.HasValue && m.Promotion is not null && ToDomainPieceType(m.Promotion.Type) == promotion.Value)
                : !promotion.HasValue));
    }

    private MoveResult ExecuteMoveInternal(ExternalChess.Move move, BoardSquare from, BoardSquare to, PieceType? promotion)
    {
        var movingPiece = _board[move.OriginalPosition];
        var movingColor = ActiveTurn;
        var pieceType = movingPiece is not null ? ToDomainPieceType(movingPiece.Type) : PieceType.Pawn;
        var captured = move.CapturedPiece is not null ? ToDomainPiece(move.CapturedPiece) : null;
        var san = move.San ?? $"{from}{to}";
        var uci = $"{from.AlgebraicNotation}{to.AlgebraicNotation}" + (promotion.HasValue ? char.ToLowerInvariant(promotion.Value.ToString()[0]) : "");

        var moveNumber = _moveHistory.Count / 2 + 1;

        // Execute move on Gera.Chess board
        bool moved = _board.Move(move);
        if (!moved)
            return MoveResult.Failed("Engine rejected move execution.");

        // Record position in repetition tracking
        RecordCurrentPosition();

        // Check endgame, check, and draw states
        EvaluateDrawAndEndgameStates();

        var record = new MoveRecord(
            MoveNumber: moveNumber,
            From: from,
            To: to,
            San: san,
            Uci: uci,
            Color: movingColor,
            PieceType: pieceType,
            Promotion: promotion,
            CapturedPiece: captured,
            IsCheck: IsInCheck,
            IsCheckmate: _endReason == GameEndReason.Checkmate,
            IsCastling: move.IsCastling,
            IsEnPassant: move.IsEnPassant
        );

        _moveHistory.Add(record);
        _moveHistoryUci.Add(uci);

        return MoveResult.Ok(
            record,
            IsInCheck,
            _endReason == GameEndReason.Checkmate,
            _activeAutomaticDraw == AutomaticDrawType.Stalemate,
            _availableClaimableDraw,
            _activeAutomaticDraw,
            _result,
            _endReason
        );
    }

    private void InitializePositionTracking()
    {
        _positionCounts.Clear();
        _positionHistory.Clear();
        RecordCurrentPosition();
        EvaluateDrawAndEndgameStates();
    }

    private void RecordCurrentPosition()
    {
        var key = GetPositionKey(_board.ToFen());
        _positionHistory.Add(key);
        _positionCounts[key] = _positionCounts.GetValueOrDefault(key) + 1;
    }

    private static string GetPositionKey(string fen)
    {
        // FEN fields: <pieces> <turn> <castling> <en-passant> <halfmove> <fullmove>
        // Repetition key is first 4 fields according to FIDE rules
        var parts = fen.Split(' ');
        return parts.Length >= 4 ? string.Join(" ", parts.Take(4)) : fen;
    }

    private void EvaluateDrawAndEndgameStates()
    {
        _availableClaimableDraw = ClaimableDrawType.None;
        _activeAutomaticDraw = AutomaticDrawType.None;

        // 1. Check native engine EndGame (Checkmate, Stalemate, InsufficientMaterial)
        var eg = _board.EndGame;
        if (eg is not null)
        {
            if (eg.EndgameType == ExternalChess.EndgameType.Checkmate)
            {
                _isGameOver = true;
                _result = eg.WonSide == ExternalChess.PieceColor.White ? GameResult.WhiteWins : GameResult.BlackWins;
                _endReason = GameEndReason.Checkmate;
                _gameEndMessage = $"{(_result == GameResult.WhiteWins ? "White" : "Black")} wins by checkmate!";
                return;
            }
            if (eg.EndgameType == ExternalChess.EndgameType.Stalemate)
            {
                _isGameOver = true;
                _result = GameResult.Draw;
                _endReason = GameEndReason.Stalemate;
                _activeAutomaticDraw = AutomaticDrawType.Stalemate;
                _gameEndMessage = "Draw by stalemate.";
                return;
            }
            if (eg.EndgameType == ExternalChess.EndgameType.InsufficientMaterial)
            {
                _isGameOver = true;
                _result = GameResult.Draw;
                _endReason = GameEndReason.InsufficientMaterial;
                _activeAutomaticDraw = AutomaticDrawType.InsufficientMaterial;
                _gameEndMessage = "Draw by insufficient material.";
                return;
            }
        }

        // 2. Repetition evaluation (Threefold = Claimable, Fivefold = Automatic)
        var currentKey = GetPositionKey(_board.ToFen());
        int repCount = _positionCounts.GetValueOrDefault(currentKey, 0);

        if (repCount >= 5)
        {
            _isGameOver = true;
            _result = GameResult.Draw;
            _endReason = GameEndReason.ThreefoldRepetition;
            _activeAutomaticDraw = AutomaticDrawType.FivefoldRepetition;
            _gameEndMessage = "Draw by fivefold repetition (automatic).";
            return;
        }

        if (repCount >= 3)
        {
            _availableClaimableDraw = ClaimableDrawType.ThreefoldRepetition;
        }

        // 3. Halfmove clock evaluation (50-move = Claimable, 75-move = Automatic)
        var fenParts = _board.ToFen().Split(' ');
        if (fenParts.Length >= 5 && int.TryParse(fenParts[4], out int halfmoves))
        {
            if (halfmoves >= 150)
            {
                _isGameOver = true;
                _result = GameResult.Draw;
                _endReason = GameEndReason.FiftyMoveRule;
                _activeAutomaticDraw = AutomaticDrawType.SeventyFiveMoveRule;
                _gameEndMessage = "Draw by 75-move rule (automatic).";
                return;
            }

            if (halfmoves >= 100)
            {
                // Can claim 50-move rule
                if (_availableClaimableDraw == ClaimableDrawType.None)
                {
                    _availableClaimableDraw = ClaimableDrawType.FiftyMoveRule;
                }
            }
        }
    }

    private static ChessPiece ToDomainPiece(ExternalChess.Piece piece)
    {
        var type = ToDomainPieceType(piece.Type);
        var color = piece.Color == ExternalChess.PieceColor.White ? PieceColor.White : PieceColor.Black;
        return new ChessPiece(type, color);
    }

    private static PieceType ToDomainPieceType(ExternalChess.PieceType type) => type.Name switch
    {
        "Pawn" => PieceType.Pawn,
        "Knight" => PieceType.Knight,
        "Bishop" => PieceType.Bishop,
        "Rook" => PieceType.Rook,
        "Queen" => PieceType.Queen,
        "King" => PieceType.King,
        _ => PieceType.Pawn
    };
}
