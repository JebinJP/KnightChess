using System.Collections.Generic;
using KnightLabChess.Core.Engine;

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
/// Context payload provided to the AI coach to ground move analysis in verified engine calculations.
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
/// Grounded explanation returned by the AI coach for a single move.
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

/// <summary>
/// Request for progressive hints that guide the student without giving away the answer immediately.
/// </summary>
public record CoachHintRequest(
    string CurrentFen,
    int HintLevel, // 1 = Broad board area / sector, 2 = Objective / concept, 3 = Piece to move, 4 = Full solution
    EngineEvaluation? Evaluation,
    string SkillLevel = "Intermediate"
);

/// <summary>
/// Progressive pedagogical hint response.
/// </summary>
public record CoachHintResponse(
    bool Success,
    int HintLevel,
    string HintTitle,
    string HintText,
    string? TargetSquare = null,
    string? ErrorMessage = null
);

/// <summary>
/// Request for opponent threat analysis on the current board.
/// </summary>
public record CoachThreatRequest(
    string CurrentFen,
    EngineEvaluation? Evaluation,
    string SkillLevel = "Intermediate"
);

/// <summary>
/// Structured threat analysis detailing opponent's candidate ideas and defensive remedies.
/// </summary>
public record CoachThreatResponse(
    bool Success,
    string Summary,
    IReadOnlyList<string> DirectThreats,
    IReadOnlyList<string> DefensiveIdeas,
    string? PrimaryThreatSquare = null,
    string? ErrorMessage = null
);

/// <summary>
/// Represents a critical turning point identified during a completed match.
/// </summary>
public record TurningPointItem(
    int MoveNumber,
    bool IsWhiteMove,
    string PlayedMoveSan,
    string PlayedMoveUci,
    string? BestMoveSan,
    string? BestMoveUci,
    int ScoreChangeCp,
    CoachMoveClassification Classification,
    string FenBefore,
    string FenAfter,
    string? Explanation = null
);

/// <summary>
/// Request to generate a comprehensive post-game pedagogical review.
/// </summary>
public record CoachGameReviewRequest(
    string StartFen,
    IReadOnlyList<string> MoveHistorySan,
    IReadOnlyList<string> MoveHistoryUci,
    IReadOnlyList<TurningPointItem> TurningPoints,
    int WhiteAccuracyPercentage,
    int BlackAccuracyPercentage,
    string SkillLevel = "Intermediate"
);

/// <summary>
/// Full game review with accuracy, key turning points, and lesson/puzzle recommendations.
/// </summary>
public record CoachGameReviewResponse(
    bool Success,
    string OverallSummary,
    int WhiteAccuracy,
    int BlackAccuracy,
    IReadOnlyList<TurningPointItem> AnalyzedTurningPoints,
    IReadOnlyList<string> KeyTakeaways,
    IReadOnlyList<string> RecommendedLessonTopics,
    string? ErrorMessage = null
);

/// <summary>
/// Request to ask the AI coach a natural language question about the active position.
/// </summary>
public record CoachQuestionRequest(
    string CurrentFen,
    string QuestionText,
    EngineEvaluation? Evaluation,
    IReadOnlyList<string> MoveHistorySan,
    string SkillLevel = "Intermediate"
);

/// <summary>
/// Response to student's question, strictly validated against the chess rules engine.
/// </summary>
public record CoachQuestionResponse(
    bool Success,
    string Question,
    string Answer,
    bool IsReferencedMoveLegal = true,
    string? ValidatedMoveSan = null,
    string? ErrorMessage = null
);
