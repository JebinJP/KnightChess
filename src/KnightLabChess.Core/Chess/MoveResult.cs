using KnightLabChess.Core.Models;

namespace KnightLabChess.Core.Chess;

/// <summary>
/// Result of an attempted chess move execution.
/// </summary>
public class MoveResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public MoveRecord? Move { get; init; }
    public bool IsCheck { get; init; }
    public bool IsCheckmate { get; init; }
    public bool IsStalemate { get; init; }
    public ClaimableDrawType ClaimableDraw { get; init; } = ClaimableDrawType.None;
    public AutomaticDrawType AutomaticDraw { get; init; } = AutomaticDrawType.None;
    public bool IsGameOver => IsCheckmate || AutomaticDraw != AutomaticDrawType.None;
    public GameResult Result { get; init; } = GameResult.InProgress;
    public GameEndReason EndReason { get; init; } = GameEndReason.None;

    public static MoveResult Failed(string reason) => new()
    {
        Success = false,
        ErrorMessage = reason
    };

    public static MoveResult Ok(
        MoveRecord move,
        bool isCheck,
        bool isCheckmate,
        bool isStalemate,
        ClaimableDrawType claimableDraw,
        AutomaticDrawType automaticDraw,
        GameResult result,
        GameEndReason endReason) => new()
    {
        Success = true,
        Move = move,
        IsCheck = isCheck,
        IsCheckmate = isCheckmate,
        IsStalemate = isStalemate,
        ClaimableDraw = claimableDraw,
        AutomaticDraw = automaticDraw,
        Result = result,
        EndReason = endReason
    };
}
