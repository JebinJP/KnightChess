namespace KnightLabChess.Core.Models;

/// <summary>
/// Represents a chess piece type.
/// </summary>
public enum PieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

/// <summary>
/// Represents a chess piece color.
/// </summary>
public enum PieceColor
{
    White,
    Black
}

/// <summary>
/// Represents a chess piece with its type and color.
/// </summary>
public record ChessPiece(PieceType Type, PieceColor Color)
{
    /// <summary>
    /// Gets the standard material value of this piece.
    /// </summary>
    public int MaterialValue => Type switch
    {
        PieceType.Pawn => 1,
        PieceType.Knight => 3,
        PieceType.Bishop => 3,
        PieceType.Rook => 5,
        PieceType.Queen => 9,
        PieceType.King => 0,
        _ => 0
    };

    /// <summary>
    /// Gets the Unicode symbol for this piece.
    /// </summary>
    public string Symbol => (Type, Color) switch
    {
        (PieceType.King, PieceColor.White) => "♔",
        (PieceType.Queen, PieceColor.White) => "♕",
        (PieceType.Rook, PieceColor.White) => "♖",
        (PieceType.Bishop, PieceColor.White) => "♗",
        (PieceType.Knight, PieceColor.White) => "♘",
        (PieceType.Pawn, PieceColor.White) => "♙",
        (PieceType.King, PieceColor.Black) => "♚",
        (PieceType.Queen, PieceColor.Black) => "♛",
        (PieceType.Rook, PieceColor.Black) => "♜",
        (PieceType.Bishop, PieceColor.Black) => "♝",
        (PieceType.Knight, PieceColor.Black) => "♞",
        (PieceType.Pawn, PieceColor.Black) => "♟",
        _ => "?"
    };

    /// <summary>
    /// Gets the FEN character for this piece.
    /// </summary>
    public char FenChar
    {
        get
        {
            char c = Type switch
            {
                PieceType.Pawn => 'p',
                PieceType.Knight => 'n',
                PieceType.Bishop => 'b',
                PieceType.Rook => 'r',
                PieceType.Queen => 'q',
                PieceType.King => 'k',
                _ => '?'
            };
            return Color == PieceColor.White ? char.ToUpper(c) : c;
        }
    }
}
