namespace KnightLabChess.Core.Models;

/// <summary>
/// Represents a square on the chess board.
/// </summary>
public readonly record struct BoardSquare(int File, int Rank)
{
    /// <summary>
    /// Gets the algebraic notation for this square (e.g., "e4").
    /// </summary>
    public string AlgebraicNotation => $"{(char)('a' + File)}{Rank + 1}";

    /// <summary>
    /// Whether this is a light square.
    /// </summary>
    public bool IsLight => (File + Rank) % 2 != 0;

    /// <summary>
    /// Parses algebraic notation to a BoardSquare.
    /// </summary>
    public static BoardSquare FromAlgebraic(string notation)
    {
        if (notation.Length != 2)
            throw new ArgumentException($"Invalid square notation: {notation}");

        int file = notation[0] - 'a';
        int rank = notation[1] - '1';

        if (file < 0 || file > 7 || rank < 0 || rank > 7)
            throw new ArgumentException($"Square out of range: {notation}");

        return new BoardSquare(file, rank);
    }

    /// <summary>
    /// Whether this square is valid (within the board).
    /// </summary>
    public bool IsValid => File >= 0 && File <= 7 && Rank >= 0 && Rank <= 7;

    public override string ToString() => AlgebraicNotation;
}
