using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KnightLabChess.Core.Coach;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Settings;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Coach;

/// <summary>
/// Gemini-powered AI chess coach service with strict engine evaluation grounding and offline heuristic fallback.
/// </summary>
public class GeminiCoachService : IAiCoachService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<GeminiCoachService> _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly SemaphoreSlim _throttleLock = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;

    public bool IsConfigured
    {
        get
        {
            var key = ResolveApiKeySync();
            return !string.IsNullOrWhiteSpace(key);
        }
    }

    public GeminiCoachService(
        ISettingsService settingsService,
        ILogger<GeminiCoachService> logger,
        HttpClient? httpClient = null)
    {
        _settingsService = settingsService;
        _logger = logger;
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _ownsHttpClient = true;
        }
    }

    private string? ResolveApiKeySync()
    {
        try
        {
            var task = _settingsService.GetSettingsAsync();
            if (task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(task.Result.GeminiApiKey))
                return task.Result.GeminiApiKey.Trim();
        }
        catch { /* ignore */ }

        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        return string.IsNullOrWhiteSpace(envKey) ? null : envKey.Trim();
    }

    private async Task<string?> ResolveApiKeyAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            if (!string.IsNullOrWhiteSpace(settings.GeminiApiKey))
                return settings.GeminiApiKey.Trim();
        }
        catch { /* ignore */ }

        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        return string.IsNullOrWhiteSpace(envKey) ? null : envKey.Trim();
    }

    public async Task<CoachResponse> AnalyzeMoveAsync(CoachAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveApiKeyAsync();

        // If not configured or AI disabled in settings, return verified rule-based coaching immediately
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BuildOfflineGroundedResponse(request, "Offline Tactical Engine Coach");
        }

        await _throttleLock.WaitAsync(cancellationToken);
        try
        {
            // Rate limiting: guarantee at least 800ms between network calls
            var elapsed = DateTime.UtcNow - _lastRequestTime;
            if (elapsed < TimeSpan.FromMilliseconds(800))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(800) - elapsed, cancellationToken);
            }
            _lastRequestTime = DateTime.UtcNow;

            var settings = await _settingsService.GetSettingsAsync();
            var modelName = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel.Trim();

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            var promptText = BuildPrompt(request, settings.CoachPersona);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = promptText }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2, // Low temperature for high factual precision
                    maxOutputTokens = 600,
                    responseMimeType = "application/json"
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(endpoint, jsonContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Gemini API call failed with status {Status}: {Body}", response.StatusCode, errorBody);
                return BuildOfflineGroundedResponse(request, $"Engine Coach (API {(int)response.StatusCode})");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseGeminiResponse(responseJson, request);
            return parsed ?? BuildOfflineGroundedResponse(request, "Engine Coach (Parsing Fallback)");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception querying Gemini coaching service.");
            return BuildOfflineGroundedResponse(request, "Engine Coach (Network Fallback)");
        }
        finally
        {
            _throttleLock.Release();
        }
    }

    public async Task<string> ExplainPositionAsync(string fen, EngineEvaluation eval, CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BuildOfflinePositionExplanation(fen, eval);
        }

        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var modelName = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel.Trim();
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            string scoreDesc = eval.MateInMoves.HasValue
                ? $"Forced mate in {Math.Abs(eval.MateInMoves.Value)} moves"
                : $"Evaluation: {(eval.ScoreCentipawns ?? 0) / 100.0:+0.0;-0.0;0.0} pawns";

            string pvDesc = eval.PrincipalVariation.Count > 0
                ? string.Join(" ", eval.PrincipalVariation.Take(5))
                : "None";

            var prompt = $@"You are KnightLab AI Chess Coach. Provide a concise 2-sentence positional summary for the student.
FEN: {fen}
{scoreDesc}
Engine Recommended Continuation: {pvDesc}
CRITICAL: Base your analysis exclusively on the provided evaluation. Output only plain text advice.";

            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { temperature = 0.3, maxOutputTokens = 250 }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, jsonContent, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var respStr = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JsonNode.Parse(respStr);
                var text = root?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ExplainPositionAsync network call failed; using offline summary.");
        }

        return BuildOfflinePositionExplanation(fen, eval);
    }

    private static string BuildPrompt(CoachAnalysisRequest req, string persona)
    {
        string evalChange = (req.ScoreBeforeCp.HasValue && req.ScoreAfterCp.HasValue)
            ? $"Score changed from {req.ScoreBeforeCp.Value / 100.0:+0.0;-0.0;0.0} to {req.ScoreAfterCp.Value / 100.0:+0.0;-0.0;0.0} pawns."
            : string.Empty;

        string mateInfo = req.MateIn.HasValue ? $"Mate in {req.MateIn.Value} detected." : string.Empty;
        string pv = req.PrincipalVariationSan.Count > 0
            ? string.Join(" ", req.PrincipalVariationSan.Take(6))
            : req.BestMoveSan ?? "N/A";

        return $@"You are the {persona} for KnightLab Chess.
Analyze this move for a {req.SkillLevel} student.

FACTUAL ENGINE GROUNDING:
- Position FEN before move: {req.FenBefore}
- Played Move: {req.MoveSan}
- Verified Engine Best Move: {req.BestMoveSan ?? "Same"}
- Move Classification: {req.Classification}
- {evalChange} {mateInfo}
- Tactical Principal Variation: {pv}

STRICT ANTI-HALLUCINATION RULES:
1. ONLY refer to moves and pieces that physically exist in the position or in the provided principal variation.
2. NEVER invent hypothetical variations that contradict the provided engine line.
3. Keep the advice actionable, instructive, and concise.

Return STRICT JSON adhering to this schema:
{{
  ""title"": ""Short title (e.g. Tactical Oversight, Strong Outpost, Missed Pin)"",
  ""summary"": ""One sentence summary of the move quality"",
  ""tacticalExplanation"": ""Detailed 1-2 sentence tactical breakdown of threats or advantages created"",
  ""strategicAdvice"": ""1 sentence piece coordination or positional principle to remember"",
  ""keyThreats"": [""threat 1"", ""threat 2""],
  ""alternativeIdea"": ""Brief mention of why {req.BestMoveSan ?? "the move"} was preferred, or null if best move""
}}";
    }

    private static CoachResponse? ParseGeminiResponse(string json, CoachAnalysisRequest request)
    {
        try
        {
            var doc = JsonNode.Parse(json);
            var text = doc?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Strip Markdown JSON fences or extract the first valid JSON object
            text = text.Trim();
            int firstBrace = text.IndexOf('{');
            int lastBrace = text.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                text = text.Substring(firstBrace, lastBrace - firstBrace + 1);
            }
            else
            {
                if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                    text = text.Substring(7).Trim();
                if (text.StartsWith("```"))
                    text = text.Substring(3).Trim();
                if (text.EndsWith("```"))
                    text = text.Substring(0, text.Length - 3).Trim();
            }

            var parsed = JsonNode.Parse(text);
            if (parsed == null) return null;

            var title = parsed["title"]?.GetValue<string>() ?? $"{request.Classification}";
            var summary = parsed["summary"]?.GetValue<string>() ?? $"{request.MoveSan} was played.";
            var tactical = parsed["tacticalExplanation"]?.GetValue<string>() ?? string.Empty;
            var strategic = parsed["strategicAdvice"]?.GetValue<string>() ?? string.Empty;
            var alternative = parsed["alternativeIdea"]?.GetValue<string>();

            var threats = new List<string>();
            if (parsed["keyThreats"] is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item != null) threats.Add(item.ToString());
                }
            }

            return new CoachResponse(
                Success: true,
                Classification: request.Classification,
                Title: title,
                Summary: summary,
                TacticalExplanation: tactical,
                StrategicAdvice: strategic,
                KeyThreats: threats,
                AlternativeIdea: alternative);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Deterministic, engine-grounded feedback used when offline, unconfigured, or during network timeouts.
    /// </summary>
    public static CoachResponse BuildOfflineGroundedResponse(CoachAnalysisRequest req, string sourceBadge)
    {
        string title;
        string summary;
        string tactical;
        string strategic;
        var threats = new List<string>();
        string? alternative = null;

        string best = req.BestMoveSan ?? req.MoveSan;
        string pv = req.PrincipalVariationSan.Count > 0 ? string.Join(" ", req.PrincipalVariationSan.Take(4)) : best;

        switch (req.Classification)
        {
            case CoachMoveClassification.BestMove:
                title = "Best Move Found!";
                summary = $"Excellent play! {req.MoveSan} is the engine's highest-evaluated move.";
                tactical = $"Directly continues the critical engine line: {pv}.";
                strategic = "Maintains optimal piece activity and central authority.";
                break;

            case CoachMoveClassification.Excellent:
                title = "Excellent Move";
                summary = $"Strong choice with {req.MoveSan}. Keeps full control of the initiative.";
                tactical = $"Preserves nearly all advantages compared to the alternative line with {best}.";
                strategic = "Active piece placement minimizes counterplay.";
                break;

            case CoachMoveClassification.Good:
                title = "Solid Move";
                summary = $"{req.MoveSan} is a playable, solid move keeping the position balanced.";
                tactical = best != req.MoveSan ? $"Engine slightly favored {best} ({pv}) for more dynamic pressure." : "Keeps positions stable.";
                strategic = "Focus on coordinating your pieces toward key outposts.";
                alternative = best != req.MoveSan ? $"Consider {best} in similar structures." : null;
                break;

            case CoachMoveClassification.Inaccuracy:
                title = "Minor Inaccuracy";
                summary = $"{req.MoveSan} allows your opponent a slight opportunity to balance or counter-attack.";
                tactical = $"The engine prefers {best} which exerts stronger pressure along {pv}.";
                strategic = "Avoid moving without calculating your opponent's most active response.";
                threats.Add("Gives opponent extra defensive tempos");
                alternative = $"Better was {best}.";
                break;

            case CoachMoveClassification.Mistake:
                title = "Tactical Mistake";
                summary = $"{req.MoveSan} concedes a noticeable advantage to your opponent.";
                tactical = $"You missed the stronger continuation {best}. The engine line demonstrates: {pv}.";
                strategic = "Always verify checks, captures, and threats before committing your move.";
                threats.Add("Loss of tempo or positional control");
                alternative = $"The correct tactical move was {best}.";
                break;

            case CoachMoveClassification.Blunder:
                title = "Critical Blunder";
                summary = $"{req.MoveSan} severely compromises your position or forfeits decisive material.";
                tactical = $"Crucial tactical oversight. The required response was {best} ({pv}).";
                strategic = "Slow down and thoroughly scan for hanging pieces and discovered checks.";
                threats.Add("Immediate tactical punishment");
                threats.Add("Material deficit or severe king vulnerability");
                alternative = $"Mandatory move was {best}.";
                break;

            case CoachMoveClassification.Book:
            default:
                title = "Opening Book Move";
                summary = $"{req.MoveSan} is a recognized grandmaster opening continuation.";
                tactical = "Develops pieces following classical opening principles.";
                strategic = "Fight for the center and prepare king castling.";
                break;
        }

        return new CoachResponse(
            Success: true,
            Classification: req.Classification,
            Title: $"{title} ({sourceBadge})",
            Summary: summary,
            TacticalExplanation: tactical,
            StrategicAdvice: strategic,
            KeyThreats: threats,
            AlternativeIdea: alternative);
    }

    private static string BuildOfflinePositionExplanation(string fen, EngineEvaluation eval)
    {
        if (eval.MateInMoves.HasValue)
        {
            int mate = eval.MateInMoves.Value;
            return mate > 0
                ? $"Forced checkmate in {mate} moves! Follow the engine's line to convert."
                : $"Defensive crisis: opponent has a forced mate in {Math.Abs(mate)} moves.";
        }

        int cp = eval.ScoreCentipawns ?? 0;
        double pawns = Math.Abs(cp) / 100.0;

        // In UCI engines, evaluation is from the perspective of the side to move
        var fenTokens = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool isWhiteTurn = fenTokens.Length < 2 || fenTokens[1] == "w";

        int whiteScoreCp = isWhiteTurn ? cp : -cp;
        double whitePawns = Math.Abs(whiteScoreCp) / 100.0;

        if (Math.Abs(whiteScoreCp) < 40)
            return $"Equal position (eval {whiteScoreCp / 100.0:+0.0;-0.0;0.0}). Both sides have balanced prospects.";
        if (whiteScoreCp > 0)
            return $"White holds an advantage of +{whitePawns:F1} pawns with superior activity.";
        return $"Black holds an advantage of -{whitePawns:F1} pawns with strong counterplay.";
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
        _throttleLock.Dispose();
    }
}
