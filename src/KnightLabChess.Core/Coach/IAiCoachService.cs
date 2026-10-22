using KnightLabChess.Core.Engine;

namespace KnightLabChess.Core.Coach;

/// <summary>
/// Service abstraction for AI-powered chess coaching grounded in authoritative engine evaluations.
/// </summary>
public interface IAiCoachService
{
    /// <summary>
    /// Whether an AI service (e.g. Gemini API key) is configured.
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
}
