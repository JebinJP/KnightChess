using KnightLabChess.Core.Models;

namespace KnightLabChess.Core.Chess;

/// <summary>
/// Domain contract for a playable chess game.
/// Provides legal move generation, move execution, special moves, draw tracking, and state persistence.
/// </summary>
public interface IChessGame
{
    string CurrentFen { get; }
    string StartFen { get; }
    PieceColor ActiveTurn { get; }
    bool IsWhiteTurn => ActiveTurn == PieceColor.White;
    bool IsInCheck { get; }
    bool IsGameOver { get; }
    GameResult Result { get; }
    GameEndReason EndReason { get; }
    string? GameEndMessage { get; }

    ClaimableDrawType AvailableClaimableDraw { get; }
    bool CanClaimDraw => AvailableClaimableDraw != ClaimableDrawType.None;
    AutomaticDrawType ActiveAutomaticDraw { get; }

    IReadOnlyList<MoveRecord> MoveHistory { get; }
    IReadOnlyList<string> MoveHistoryUci { get; }

    ChessPiece? GetPieceAt(BoardSquare square);
    ChessPiece? GetPieceAt(int file, int rank);

    IReadOnlyList<BoardSquare> GetLegalDestinations(BoardSquare from);
    bool IsLegalMove(BoardSquare from, BoardSquare to, PieceType? promotion = null);

    MoveResult MakeMove(BoardSquare from, BoardSquare to, PieceType? promotion = null);
    MoveResult MakeMove(string moveString);

    bool UndoLastMove();
    bool ClaimDraw();
    bool Resign(PieceColor resigningPlayer);

    void ResetToStartingPosition();
    void LoadFromPosition(string startFen, IEnumerable<string> moves);
    void LoadFromPgn(string pgn);
    string GetPgn(string whitePlayer = "White", string blackPlayer = "Black");
}
