using System.Threading;
using System.Threading.Tasks;
using KnightLabChess.Core.Engine;

namespace KnightLabChess.Core.Coach;

/// <summary>
/// Service abstraction for AI-powered chess coaching grounded in authoritative engine evaluations.
/// Supports move explanations, threat analysis, progressive hints, interactive Q&amp;A, and post-game reviews.
/// </summary>
public interface IAiCoachService
{
    /// <summary>
    /// Whether an AI service (e.g. Gemini API key or environment variable) is configured.
    /// If false, the service provides an offline rule-based coaching fallback grounded in engine metrics.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Analyzes a played move with pedagogical feedback grounded in verified engine calculations.
    /// </summary>
    Task<CoachResponse> AnalyzeMoveAsync(CoachAnalysisRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provides an overall strategic and tactical commentary on a board position.
    /// </summary>
    Task<string> ExplainPositionAsync(string fen, EngineEvaluation eval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates progressive hints (level 1-4) without immediately giving away the engine solution.
    /// </summary>
    Task<CoachHintResponse> GetProgressiveHintAsync(CoachHintRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes the opponent's direct threats and tactical objectives on the current board.
    /// </summary>
    Task<CoachThreatResponse> AnalyzeThreatsAsync(CoachThreatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reviews a completed match, identifying accuracy scores, turning points, and lesson recommendations.
    /// </summary>
    Task<CoachGameReviewResponse> ReviewGameAsync(CoachGameReviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers a student's natural language question about the active board with strict rules-engine grounding.
    /// </summary>
    Task<CoachQuestionResponse> AnswerQuestionAsync(CoachQuestionRequest request, CancellationToken cancellationToken = default);
}
