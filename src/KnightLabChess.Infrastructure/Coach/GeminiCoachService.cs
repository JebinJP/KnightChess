using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Coach;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Models;
using KnightLabChess.Core.Settings;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Coach;

/// <summary>
/// Gemini-powered AI chess coach service with strict engine evaluation grounding and offline heuristic fallback.
/// Implements move analysis, progressive hints, opponent threat radar, post-game review, and rules-validated Q&amp;A.
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
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
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

    // =========================================================================
    // 1. Move Explanation
    // =========================================================================

    public async Task<CoachResponse> AnalyzeMoveAsync(CoachAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BuildOfflineGroundedResponse(request, "Offline Tactical Engine Coach");
        }

        await _throttleLock.WaitAsync(cancellationToken);
        try
        {
            await ApplyRateLimitAsync(cancellationToken);

            var settings = await _settingsService.GetSettingsAsync();
            var modelName = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel.Trim();
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            var promptText = BuildMoveAnalysisPrompt(request, settings.CoachPersona);
            var responseJson = await PostGeminiJsonAsync(endpoint, promptText, cancellationToken);

            if (responseJson != null)
            {
                var parsed = ParseGeminiResponse(responseJson, request);
                if (parsed != null)
                {
                    // Validate any alternative move suggested against the rules engine
                    if (!string.IsNullOrEmpty(parsed.AlternativeIdea))
                    {
                        ValidateMove(request.FenBefore, parsed.AlternativeIdea, out var validatedSan);
                        if (validatedSan != null)
                        {
                            return parsed with { AlternativeIdea = $"Consider {validatedSan}." };
                        }
                    }
                    return parsed;
                }
            }

            return BuildOfflineGroundedResponse(request, "Engine Coach (Parsing Fallback)");
        }
        catch (OperationCanceledException) { throw; }
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

    // =========================================================================
    // 2. Position Explanation
    // =========================================================================

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
                generationConfig = new { temperature = 0.2, maxOutputTokens = 250 }
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

    // =========================================================================
    // 3. Progressive Hints (Levels 1 to 4)
    // =========================================================================

    public async Task<CoachHintResponse> GetProgressiveHintAsync(CoachHintRequest request, CancellationToken cancellationToken = default)
    {
        string? bestMoveSan = null;
        string? bestMoveUci = request.Evaluation?.BestMoveUci;
        if (!string.IsNullOrEmpty(bestMoveUci))
        {
            ValidateMove(request.CurrentFen, bestMoveUci, out bestMoveSan);
        }

        var apiKey = await ResolveApiKeyAsync();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var modelName = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel.Trim();
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

                var prompt = $@"You are KnightLab AI Chess Coach providing a Level {request.HintLevel} progressive hint.
FEN: {request.CurrentFen}
Target Move: {bestMoveSan ?? bestMoveUci ?? "Tactical improvement"}
Skill Level: {request.SkillLevel}

PROGRESSIVE HINT RULES:
- Level 1: Mention ONLY the broad board area (Kingside, Center, Queenside) or a strategic idea. Do NOT mention the piece or square.
- Level 2: Mention the key positional concept or target square (e.g. 'Look for weaknesses on d5').
- Level 3: Name the candidate piece that should move (e.g. 'Look at your Knight on f3').
- Level 4: Reveal the full recommended move ({bestMoveSan ?? bestMoveUci}) and the tactical justification.

Return STRICT JSON:
{{
  ""title"": ""Hint Level {request.HintLevel}"",
  ""hintText"": ""1-2 sentence hint text"",
  ""targetSquare"": ""optional algebraic square (e.g. e4 or null)""
}}";

                var jsonStr = await PostGeminiJsonAsync(endpoint, prompt, cancellationToken);
                if (jsonStr != null)
                {
                    var parsed = ExtractJsonObject(jsonStr);
                    if (parsed != null)
                    {
                        var title = parsed["title"]?.GetValue<string>() ?? $"Hint Level {request.HintLevel}";
                        var text = parsed["hintText"]?.GetValue<string>() ?? string.Empty;
                        var sq = parsed["targetSquare"]?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return new CoachHintResponse(true, request.HintLevel, title, text, sq);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Progressive hint network call failed; using offline rule-based hint.");
            }
        }

        return BuildOfflineProgressiveHint(request, bestMoveSan ?? bestMoveUci);
    }

    // =========================================================================
    // 4. Opponent Threat Radar
    // =========================================================================

    public async Task<CoachThreatResponse> AnalyzeThreatsAsync(CoachThreatRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveApiKeyAsync();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var modelName = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel.Trim();
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

                string bestLine = request.Evaluation?.PrincipalVariation.Count > 0
                    ? string.Join(" ", request.Evaluation.PrincipalVariation.Take(4))
                    : "None";

                var prompt = $@"You are KnightLab AI Chess Coach analyzing the opponent's threats.
FEN: {request.CurrentFen}
Engine Best Line: {bestLine}

Analyze what threats the opponent poses if the current player plays passively.
Return STRICT JSON:
{{
  ""summary"": ""One sentence summary of the threat level"",
  ""directThreats"": [""threat 1"", ""threat 2""],
  ""defensiveIdeas"": [""defensive idea 1"", ""defensive idea 2""],
  ""primaryThreatSquare"": ""optional critical square or null""
}}";

                var jsonStr = await PostGeminiJsonAsync(endpoint, prompt, cancellationToken);
                if (jsonStr != null)
                {
                    var parsed = ExtractJsonObject(jsonStr);
                    if (parsed != null)
                    {
                        var summary = parsed["summary"]?.GetValue<string>() ?? "Opponent is coordinating active threats.";
                        var sq = parsed["primaryThreatSquare"]?.GetValue<string>();

                        var directThreats = new List<string>();
                        if (parsed["directThreats"] is JsonArray tArr)
                        {
                            foreach (var item in tArr) if (item != null) directThreats.Add(item.ToString());
                        }

                        var defensiveIdeas = new List<string>();
                        if (parsed["defensiveIdeas"] is JsonArray dArr)
                        {
                            foreach (var item in dArr) if (item != null) defensiveIdeas.Add(item.ToString());
                        }

                        return new CoachThreatResponse(true, summary, directThreats, defensiveIdeas, sq);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "AnalyzeThreatsAsync network call failed; using offline threat analysis.");
            }
        }

        return BuildOfflineThreatAnalysis(request);
    }

    // =========================================================================
    // 5. Post-Game Review & Turning Points
    // =========================================================================

    public async Task<CoachGameReviewResponse> ReviewGameAsync(CoachGameReviewRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveApiKeyAsync();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var modelName = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel.Trim();
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

                var tpSummaries = request.TurningPoints
                    .Take(5)
                    .Select(tp => $"Move {tp.MoveNumber} ({(tp.IsWhiteMove ? "White" : "Black")}): Played {tp.PlayedMoveSan}, Best {tp.BestMoveSan ?? "N/A"} ({tp.Classification}, {tp.ScoreChangeCp / 100.0:+0.0;-0.0;0.0} cp)")
                    .ToList();

                var prompt = $@"You are KnightLab AI Chess Coach conducting a post-game review.
Total Moves: {request.MoveHistorySan.Count}
White Accuracy: {request.WhiteAccuracyPercentage}%
Black Accuracy: {request.BlackAccuracyPercentage}%
Key Turning Points:
{string.Join("\n", tpSummaries)}

Return STRICT JSON:
{{
  ""overallSummary"": ""2-sentence summary of how the game swung and who seized the initiative"",
  ""keyTakeaways"": [""takeaway 1"", ""takeaway 2""],
  ""recommendedLessonTopics"": [""recommended lesson topic 1"", ""recommended lesson topic 2""]
}}";

                var jsonStr = await PostGeminiJsonAsync(endpoint, prompt, cancellationToken);
                if (jsonStr != null)
                {
                    var parsed = ExtractJsonObject(jsonStr);
                    if (parsed != null)
                    {
                        var summary = parsed["overallSummary"]?.GetValue<string>() ?? "Game concluded with sharp tactical turning points.";
                        var takeaways = new List<string>();
                        if (parsed["keyTakeaways"] is JsonArray tArr)
                        {
                            foreach (var item in tArr) if (item != null) takeaways.Add(item.ToString());
                        }

                        var lessons = new List<string>();
                        if (parsed["recommendedLessonTopics"] is JsonArray lArr)
                        {
                            foreach (var item in lArr) if (item != null) lessons.Add(item.ToString());
                        }

                        return new CoachGameReviewResponse(
                            Success: true,
                            OverallSummary: summary,
                            WhiteAccuracy: request.WhiteAccuracyPercentage,
                            BlackAccuracy: request.BlackAccuracyPercentage,
                            AnalyzedTurningPoints: request.TurningPoints,
                            KeyTakeaways: takeaways.Count > 0 ? takeaways : ["Watch out for undefended pieces.", "Calculate opponent responses before attacking."],
                            RecommendedLessonTopics: lessons.Count > 0 ? lessons : ["Tactical Motifs: Pins & Forks", "Endgame Technique"]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ReviewGameAsync network call failed; using offline game review.");
            }
        }

        return BuildOfflineGameReview(request);
    }

    // =========================================================================
    // 6. Interactive Q&A Grounded in Rules Engine
    // =========================================================================

    public async Task<CoachQuestionResponse> AnswerQuestionAsync(CoachQuestionRequest request, CancellationToken cancellationToken = default)
    {
        // Check if the user is asking about a specific move (e.g. "Can I play e5?", "Why not Nf3?")
        string? referencedMove = ExtractMoveFromQuestion(request.QuestionText);
        bool isLegal = true;
        string? validatedSan = null;

        if (!string.IsNullOrEmpty(referencedMove))
        {
            isLegal = ValidateMove(request.CurrentFen, referencedMove, out validatedSan);
            if (!isLegal)
            {
                return new CoachQuestionResponse(
                    Success: true,
                    Question: request.QuestionText,
                    Answer: $"The move '{referencedMove}' is not a legal move (illegal move) in this position according to official chess rules. Make sure the square is reachable, the piece is not pinned, and your King is not in check.",
                    IsReferencedMoveLegal: false);
            }
        }

        var apiKey = await ResolveApiKeyAsync();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var modelName = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel.Trim();
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

                string evalInfo = request.Evaluation != null
                    ? $"Evaluation: {(request.Evaluation.ScoreCentipawns ?? 0) / 100.0:+0.0;-0.0;0.0} pawns. Engine best move: {request.Evaluation.BestMoveUci}."
                    : "Engine evaluation pending.";

                var prompt = $@"You are KnightLab AI Chess Coach answering a student's question.
Position FEN: {request.CurrentFen}
{evalInfo}
Student Question: {request.QuestionText}
{(validatedSan != null ? $"Referenced Legal Move: {validatedSan}" : string.Empty)}

ANTI-HALLUCINATION RULES:
1. Ground your answer exclusively in genuine chess principles and the provided position.
2. If the user asks about an engine line, refer to verified piece activity.
3. Keep the response to 2-3 concise, educational sentences.

Output ONLY plain text answer.";

                var body = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } },
                    generationConfig = new { temperature = 0.25, maxOutputTokens = 350 }
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
                        return new CoachQuestionResponse(true, request.QuestionText, text.Trim(), isLegal, validatedSan);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "AnswerQuestionAsync network call failed; using offline rule-based answer.");
            }
        }

        return BuildOfflineQuestionAnswer(request, isLegal, validatedSan);
    }

    // =========================================================================
    // Helpers, Validation, and Rate Limiting
    // =========================================================================

    /// <summary>
    /// Validates any proposed move text against the authoritative chess rules engine.
    /// </summary>
    public static bool ValidateMove(string fen, string moveText, out string? validatedSan)
    {
        validatedSan = null;
        if (string.IsNullOrWhiteSpace(moveText)) return false;

        try
        {
            var game = new GeraChessGameAdapter(fen);
            string sanitized = moveText.Trim().TrimEnd('!', '?', '+', '#');

            // 1. Direct MakeMove
            var result = game.MakeMove(sanitized);
            if (result.Success && result.Move != null)
            {
                validatedSan = result.Move.San;
                return true;
            }

            // 2. Try removing '=' if present (e.g. "e8=Q" -> "e8Q")
            if (sanitized.Contains('='))
            {
                var withoutEqual = sanitized.Replace("=", "");
                result = game.MakeMove(withoutEqual);
                if (result.Success && result.Move != null)
                {
                    validatedSan = result.Move.San;
                    return true;
                }
            }
            else if (sanitized.Length >= 3 && char.IsLetter(sanitized[^1]) && char.IsDigit(sanitized[^2]))
            {
                var withEqual = sanitized.Insert(sanitized.Length - 1, "=");
                result = game.MakeMove(withEqual);
                if (result.Success && result.Move != null)
                {
                    validatedSan = result.Move.San;
                    return true;
                }
            }

            // 3. Try UCI expansion for promotion if formatted like "e8=Q" or "e8q"
            if (sanitized.Length is 3 or 4 or 5)
            {
                char promoChar = sanitized.Contains('=') 
                    ? sanitized[^1] 
                    : (char.IsLetter(sanitized[^1]) && char.IsDigit(sanitized[^2]) ? sanitized[^1] : 'q');

                int destIndex = sanitized.Contains('=') ? sanitized.IndexOf('=') - 2 : (char.IsLetter(sanitized[^1]) ? sanitized.Length - 3 : sanitized.Length - 2);
                if (destIndex >= 0 && destIndex + 2 <= sanitized.Length)
                {
                    string targetSquareStr = sanitized.Substring(destIndex, 2);
                    if (targetSquareStr[0] >= 'a' && targetSquareStr[0] <= 'h' && (targetSquareStr[1] == '8' || targetSquareStr[1] == '1'))
                    {
                        var targetSquare = BoardSquare.FromAlgebraic(targetSquareStr);
                        int originRank = targetSquare.Rank == 7 ? 6 : (targetSquare.Rank == 0 ? 1 : -1);
                        if (originRank >= 0)
                        {
                            var fromSquare = new BoardSquare(targetSquare.File, originRank);
                            var uci = $"{fromSquare.AlgebraicNotation}{targetSquareStr}{char.ToLowerInvariant(promoChar)}";
                            result = game.MakeMove(uci);
                            if (result.Success && result.Move != null)
                            {
                                validatedSan = result.Move.San;
                                return true;
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return false;
    }

    private static string? ExtractMoveFromQuestion(string question)
    {
        // Searches for chess-like tokens in question (e.g. "e4", "Nf3", "Qxd5", "O-O", "e8=Q", "e7e8q")
        var tokens = question.Split([' ', '?', '!', ',', '.', ';', ':', '(', ')', '"', '\''], StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in tokens)
        {
            var cleaned = t.Trim().TrimEnd('+', '#', '!', '?');
            if (cleaned.Length is >= 2 and <= 7)
            {
                if (cleaned.StartsWith("O-O", StringComparison.OrdinalIgnoreCase))
                {
                    return cleaned;
                }

                // Check for promotion: e.g. e8=Q, exd8=Q, e8Q
                if (cleaned.Contains('=') || (cleaned.Length >= 3 && char.IsLetter(cleaned[^1]) && char.IsDigit(cleaned[^2])))
                {
                    if (cleaned[0] >= 'a' && cleaned[0] <= 'h')
                    {
                        return cleaned;
                    }
                }

                // Check for UCI: e.g. e2e4, e7e8q
                if (cleaned.Length is 4 or 5 &&
                    cleaned[0] >= 'a' && cleaned[0] <= 'h' &&
                    cleaned[1] >= '1' && cleaned[1] <= '8' &&
                    cleaned[2] >= 'a' && cleaned[2] <= 'h' &&
                    cleaned[3] >= '1' && cleaned[3] <= '8')
                {
                    return cleaned;
                }

                // Check for standard SAN: e.g. e4, d5, Nf3, Bxc6, Rae1, Rad8
                if (char.IsLetter(cleaned[0]) && char.IsDigit(cleaned[^1]))
                {
                    return cleaned;
                }
            }
        }
        return null;
    }

    private async Task ApplyRateLimitAsync(CancellationToken ct)
    {
        var elapsed = DateTime.UtcNow - _lastRequestTime;
        if (elapsed < TimeSpan.FromMilliseconds(800))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(800) - elapsed, ct);
        }
        _lastRequestTime = DateTime.UtcNow;
    }

    private async Task<string?> PostGeminiJsonAsync(string endpoint, string promptText, CancellationToken ct)
    {
        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = promptText } } }
            },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 650,
                responseMimeType = "application/json"
            }
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(endpoint, jsonContent, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Gemini API call returned status {Status}: {Body}", response.StatusCode, err);
            return null;
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    private static JsonNode? ExtractJsonObject(string json)
    {
        try
        {
            var doc = JsonNode.Parse(json);
            var text = doc?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(text)) return null;

            text = text.Trim();
            int firstBrace = text.IndexOf('{');
            int lastBrace = text.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                text = text.Substring(firstBrace, lastBrace - firstBrace + 1);
            }
            return JsonNode.Parse(text);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildMoveAnalysisPrompt(CoachAnalysisRequest req, string persona)
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
        var parsed = ExtractJsonObject(json);
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

    // =========================================================================
    // Deterministic Offline Rule-Based Fallbacks
    // =========================================================================

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
                summary = $"Excellent play! {req.MoveSan} matches the engine's highest-evaluated move.";
                tactical = $"Continues the critical engine line: {pv}.";
                strategic = "Maintains optimal piece activity and central authority.";
                break;

            case CoachMoveClassification.Excellent:
                title = "Excellent Move";
                summary = $"Strong choice with {req.MoveSan}. Keeps full control of the initiative.";
                tactical = $"Preserves advantage compared to the alternative line with {best}.";
                strategic = "Active piece placement minimizes counterplay.";
                break;

            case CoachMoveClassification.Good:
                title = "Solid Move";
                summary = $"{req.MoveSan} is a solid move keeping the position balanced.";
                tactical = best != req.MoveSan ? $"Engine slightly favored {best} ({pv}) for sharper pressure." : "Keeps position stable.";
                strategic = "Focus on coordinating your pieces toward key outposts.";
                alternative = best != req.MoveSan ? $"Consider {best} in similar structures." : null;
                break;

            case CoachMoveClassification.Inaccuracy:
                title = "Minor Inaccuracy";
                summary = $"{req.MoveSan} gives your opponent a slight opportunity to equalize.";
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

    private static CoachHintResponse BuildOfflineProgressiveHint(CoachHintRequest req, string? bestMove)
    {
        string title = req.HintLevel switch
        {
            1 => "Hint Level 1: Board Sector & Strategic Theme",
            2 => "Hint Level 2: Tactical Concept & Target Square",
            3 => "Hint Level 3: Candidate Piece Activation",
            4 => "Hint Level 4: Concrete Solution & Variation",
            _ => $"Hint (Level {req.HintLevel})"
        };
        string text;
        string? targetSquare = null;

        if (string.IsNullOrEmpty(bestMove))
        {
            return new CoachHintResponse(true, req.HintLevel, title, "Scan for undefended pieces and control the open central files.");
        }

        // Determine destination square if best move has at least 2 chars
        if (bestMove.Length >= 2)
        {
            targetSquare = bestMove.Length >= 4 ? bestMove.Substring(2, 2) : bestMove.Substring(bestMove.Length - 2);
        }

        switch (req.HintLevel)
        {
            case 1:
                text = "Focus your attention on central squares and piece coordination.";
                break;
            case 2:
                text = targetSquare != null
                    ? $"Look closely at the active square {targetSquare} and the pieces controlling it."
                    : "Look for pins, forks, or unprotected pieces.";
                break;
            case 3:
                text = bestMove.Length >= 2
                    ? $"Consider activating the piece located at or moving towards {targetSquare ?? "the center"}."
                    : "Find your least active piece and improve its position.";
                break;
            case 4:
            default:
                text = $"The engine recommends playing {bestMove} to maintain the optimal advantage.";
                break;
        }

        return new CoachHintResponse(true, req.HintLevel, title, text, targetSquare);
    }

    private static CoachThreatResponse BuildOfflineThreatAnalysis(CoachThreatRequest req)
    {
        var threats = new List<string>
        {
            "Opponent is looking to infiltrate uncoordinated squares.",
            "Watch out for discovered checks and tactical pin lines."
        };

        var defense = new List<string>
        {
            "Solidify pawn structure and support central minor pieces.",
            "Complete king safety before initiating pawn storms."
        };

        return new CoachThreatResponse(true, "Opponent has counterplay opportunities if you play passively.", threats, defense);
    }

    private static CoachGameReviewResponse BuildOfflineGameReview(CoachGameReviewRequest req)
    {
        var takeaways = new List<string>
        {
            "Maintain king safety throughout the middlegame.",
            "Always check candidate moves for hanging tactics before playing."
        };

        var lessons = new List<string>
        {
            "Tactical Motifs: Pins & Skewers",
            "Defending Against Threats",
            "Endgame Principles"
        };

        return new CoachGameReviewResponse(
            Success: true,
            OverallSummary: $"Game concluded after {req.MoveHistorySan.Count / 2} moves. White accuracy: {req.WhiteAccuracyPercentage}%, Black accuracy: {req.BlackAccuracyPercentage}%.",
            WhiteAccuracy: req.WhiteAccuracyPercentage,
            BlackAccuracy: req.BlackAccuracyPercentage,
            AnalyzedTurningPoints: req.TurningPoints,
            KeyTakeaways: takeaways,
            RecommendedLessonTopics: lessons);
    }

    private static CoachQuestionResponse BuildOfflineQuestionAnswer(CoachQuestionRequest req, bool isLegal, string? validatedSan)
    {
        if (!isLegal)
        {
            return new CoachQuestionResponse(
                Success: true,
                Question: req.QuestionText,
                Answer: "That move is not legally possible in this position under standard chess rules.",
                IsReferencedMoveLegal: false);
        }

        string answer = validatedSan != null
            ? $"The move {validatedSan} is legal. Check tactical responses to ensure your pieces remain defended."
            : "Focus on piece activity, controlling open files, and maintaining King safety in this position.";

        return new CoachQuestionResponse(true, req.QuestionText, answer, true, validatedSan);
    }

    private static string BuildOfflinePositionExplanation(string fen, EngineEvaluation eval)
    {
        if (eval.MateInMoves.HasValue)
        {
            int mate = eval.MateInMoves.Value;
            return mate > 0
                ? $"Forced checkmate in {mate} moves (Mate in {mate})! Follow the engine's line to convert."
                : $"Defensive crisis: opponent has a forced mate in {Math.Abs(mate)} moves (Mate in -{Math.Abs(mate)}).";
        }

        if (!eval.ScoreCentipawns.HasValue)
        {
            return "Engine evaluation is currently unavailable for this position. Cannot evaluate without engine calculations.";
        }

        int cp = eval.ScoreCentipawns.Value;
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
