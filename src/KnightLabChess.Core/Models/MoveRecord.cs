namespace KnightLabChess.Core.Models;

/// <summary>
/// Immutable record of a completed chess move.
/// </summary>
public record MoveRecord(
    int MoveNumber,
    BoardSquare From,
    BoardSquare To,
    string San,
    string Uci,
    PieceColor Color,
    PieceType PieceType,
    PieceType? Promotion = null,
    ChessPiece? CapturedPiece = null,
    bool IsCheck = false,
    bool IsCheckmate = false,
    bool IsCastling = false,
    bool IsEnPassant = false
)
{
    public bool IsPromotion => Promotion.HasValue;
}
