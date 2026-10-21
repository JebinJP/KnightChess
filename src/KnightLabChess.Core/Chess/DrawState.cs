namespace KnightLabChess.Core.Chess;

/// <summary>
/// Represents draws that can be claimed by a player under standard rules (non-terminating until claimed).
/// </summary>
public enum ClaimableDrawType
{
    None,
    ThreefoldRepetition,
    FiftyMoveRule
}

/// <summary>
/// Represents mandatory/automatic draws that immediately conclude the game under standard FIDE rules.
/// </summary>
public enum AutomaticDrawType
{
    None,
    Stalemate,
    FivefoldRepetition,
    SeventyFiveMoveRule,
    InsufficientMaterial
}
