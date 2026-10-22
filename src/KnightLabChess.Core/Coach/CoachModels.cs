namespace KnightLabChess.Core.Coach;

/// <summary>
/// Mathematical and pedagogical classification of a chess move relative to the engine's best line.
/// </summary>
public enum CoachMoveClassification
{
    Book,
    BestMove,
    Excellent,
    Good,
    Inaccuracy,
    Mistake,
    Blunder
}

/// <summary>
/// Context payload provided to the AI coach to ground analysis in verified engine calculations.
/// </summary>
public record CoachAnalysisRequest(
    string FenBefore,
    string FenAfter,
    string MoveSan,
    string MoveUci,
    string? BestMoveSan,
    string? BestMoveUci,
    int? ScoreBeforeCp,
    int? ScoreAfterCp,
    int? MateIn,
    IReadOnlyList<string> PrincipalVariationSan,
    CoachMoveClassification Classification,
    string SkillLevel = "Intermediate"
);

/// <summary>
/// Grounded explanation returned by the AI coach.
/// </summary>
public record CoachResponse(
    bool Success,
    CoachMoveClassification Classification,
    string Title,
    string Summary,
    string TacticalExplanation,
    string StrategicAdvice,
    IReadOnlyList<string> KeyThreats,
    string? AlternativeIdea,
    string? ErrorMessage = null
);
