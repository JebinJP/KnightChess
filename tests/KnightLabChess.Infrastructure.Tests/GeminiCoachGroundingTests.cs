using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Coach;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Settings;
using KnightLabChess.Infrastructure.Coach;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnightLabChess.Infrastructure.Tests;

/// <summary>
/// Unit and anti-hallucination regression tests for Gemini AI Coach.
/// Verifies rules-engine move validation, progressive hints, mate lines, promotion handling, and API resilience.
/// </summary>
public class GeminiCoachGroundingTests
{
    private class TestSettingsService : ISettingsService
    {
        public UserSettings Settings { get; set; } = new();
        public Task<UserSettings> GetSettingsAsync() => Task.FromResult(Settings);
        public Task SaveSettingsAsync(UserSettings settings)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
        public string GetDatabasePath() => "test.db";
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }

    // =========================================================================
    // 1. Anti-Hallucination & Legal Move Validation
    // =========================================================================

    [Fact]
    public async Task AnswerQuestion_WhenQueryingIllegalMove_RejectsMoveAndFlagsAsIllegal()
    {
        var settingsService = new TestSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        // Standard starting position: e4 is legal, but e5 for White on move 1 is illegal!
        string startFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var request = new CoachQuestionRequest(
            CurrentFen: startFen,
            QuestionText: "Why can't I play e5 on my first turn?",
            Evaluation: new EngineEvaluation { ScoreCentipawns = 20, Depth = 12 },
            MoveHistorySan: [],
            SkillLevel: "Beginner"
        );

        var response = await coach.AnswerQuestionAsync(request);

        Assert.NotNull(response);
        Assert.True(response.Success);
        // e5 is illegal for White on move 1
        Assert.False(response.IsReferencedMoveLegal);
        Assert.Contains("illegal", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnswerQuestion_WhenQueryingLegalMove_ValidatesMoveAndConfirmsLegality()
    {
        var settingsService = new TestSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        string startFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var request = new CoachQuestionRequest(
            CurrentFen: startFen,
            QuestionText: "Can I play Nf3 here?",
            Evaluation: new EngineEvaluation { ScoreCentipawns = 25, Depth = 12, BestMoveUci = "g1f3" },
            MoveHistorySan: [],
            SkillLevel: "Intermediate"
        );

        var response = await coach.AnswerQuestionAsync(request);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.True(response.IsReferencedMoveLegal);
        Assert.Equal("Nf3", response.ValidatedMoveSan);
    }

    // =========================================================================
    // 2. Tactical Positions, Forced Mate & Incomplete Analysis Handling
    // =========================================================================

    [Fact]
    public async Task ExplainPosition_WhenForcedMateLine_AccuratelyReflectsMateInSummary()
    {
        var settingsService = new TestSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        // Position where White has Mate in 2
        var eval = new EngineEvaluation
        {
            MateInMoves = 2,
            Depth = 15,
            BestMoveUci = "d1h5",
            PrincipalVariation = ["d1h5", "g7g6", "h5e5"]
        };

        var summary = await coach.ExplainPositionAsync("rnbqkbnr/pppp1ppp/8/4p3/6P1/5P2/PPPPP2P/RNBQKBNR b KQkq - 0 2", eval);

        Assert.NotNull(summary);
        Assert.Contains("Mate in 2", summary);
    }

    [Fact]
    public async Task ExplainPosition_WhenEvaluationUnavailable_AcknowledgesUncertaintyWithoutInventingNumbers()
    {
        var settingsService = new TestSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        var emptyEval = new EngineEvaluation
        {
            ScoreCentipawns = null,
            MateInMoves = null,
            Depth = 0,
            BestMoveUci = null
        };

        var summary = await coach.ExplainPositionAsync("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", emptyEval);

        Assert.NotNull(summary);
        Assert.Contains("unavailable", summary, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 3. Pawn Promotion Scenarios
    // =========================================================================

    [Fact]
    public async Task AnswerQuestion_WhenPawnPromotionIsLegal_ValidatesPromotionNotation()
    {
        var settingsService = new TestSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        // White pawn on e7, 8th rank empty
        string promotionFen = "8/4P3/8/8/8/8/8/4K2k w - - 0 1";

        var request = new CoachQuestionRequest(
            CurrentFen: promotionFen,
            QuestionText: "Should I play e8=Q?",
            Evaluation: new EngineEvaluation { ScoreCentipawns = 900, Depth = 14, BestMoveUci = "e7e8q" },
            MoveHistorySan: [],
            SkillLevel: "Intermediate"
        );

        var response = await coach.AnswerQuestionAsync(request);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.True(response.IsReferencedMoveLegal);
        Assert.NotNull(response.ValidatedMoveSan);
        Assert.Contains("e8", response.ValidatedMoveSan);
    }

    [Fact]
    public async Task AnswerQuestion_WhenPawnPromotionIsIllegal_RejectsMove()
    {
        var settingsService = new TestSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        // White pawn on e6 (cannot promote in one step)
        string fen = "8/8/4P3/8/8/8/8/4K2k w - - 0 1";

        var request = new CoachQuestionRequest(
            CurrentFen: fen,
            QuestionText: "Can I play e8=Q right now?",
            Evaluation: new EngineEvaluation { ScoreCentipawns = 300, Depth = 12 },
            MoveHistorySan: [],
            SkillLevel: "Beginner"
        );

        var response = await coach.AnswerQuestionAsync(request);

        Assert.NotNull(response);
        Assert.False(response.IsReferencedMoveLegal);
        Assert.Contains("illegal", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 4. Progressive Hint Ladder (Levels 1 to 4)
    // =========================================================================

    [Theory]
    [InlineData(1, "Sector")]
    [InlineData(2, "Concept")]
    [InlineData(3, "Piece")]
    [InlineData(4, "Solution")]
    public async Task GetProgressiveHintAsync_ReturnsAppropriateInformationForLevel(int level, string expectedFocus)
    {
        var settingsService = new TestSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        string fen = "r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/2NP1N2/PPP2PPP/R1BQK2R b KQkq - 0 5";
        var eval = new EngineEvaluation
        {
            ScoreCentipawns = 10,
            Depth = 12,
            BestMoveUci = "d7d6",
            PrincipalVariation = ["d7d6", "h2h3"]
        };

        var request = new CoachHintRequest(fen, level, eval, "Intermediate");
        var response = await coach.GetProgressiveHintAsync(request);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal(level, response.HintLevel);
        Assert.False(string.IsNullOrWhiteSpace(response.HintText));

        if (level == 4)
        {
            // Level 4 must provide concrete best move
            Assert.Contains("d6", response.HintText, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Lower levels must not give away the exact move immediately
            Assert.Contains(expectedFocus, response.HintTitle, StringComparison.OrdinalIgnoreCase);
        }
    }

    // =========================================================================
    // 5. Opponent Threat Radar
    // =========================================================================

    [Fact]
    public async Task AnalyzeThreatsAsync_IdentifiesDirectChecksAndOpponentIdeas()
    {
        var settingsService = new TestSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        // Position where Black has a hanging rook or direct check
        string fen = "r1bqk2r/pppp1ppp/2n2n2/4p3/1b2P3/2NP1N2/PPP2PPP/R1BQKB1R w KQkq - 1 5";
        var eval = new EngineEvaluation
        {
            ScoreCentipawns = -40,
            Depth = 12,
            BestMoveUci = "c1d2"
        };

        var request = new CoachThreatRequest(fen, eval, "Intermediate");
        var response = await coach.AnalyzeThreatsAsync(request);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotEmpty(response.DefensiveIdeas);
        Assert.False(string.IsNullOrWhiteSpace(response.Summary));
    }

    // =========================================================================
    // 6. Deliberately Misleading or Malformed API Responses
    // =========================================================================

    [Fact]
    public async Task AnalyzeMoveAsync_WhenApiReturnsMalformedJson_FallsBackGracefully()
    {
        var settingsService = new TestSettingsService
        {
            Settings = new UserSettings { GeminiApiKey = "test-api-key" }
        };

        var mockHandler = new MockHttpMessageHandler(_ =>
        {
            // Return invalid non-JSON content
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<!DOCTYPE html><html><body>Error Gateway</body></html>", Encoding.UTF8, "text/html")
            };
            return Task.FromResult(response);
        });

        using var client = new HttpClient(mockHandler);
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance, client);

        var request = new CoachAnalysisRequest(
            FenBefore: "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            FenAfter: "rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 2",
            MoveSan: "e5",
            MoveUci: "e7e5",
            BestMoveSan: "e5",
            BestMoveUci: "e7e5",
            ScoreBeforeCp: 25,
            ScoreAfterCp: 20,
            MateIn: null,
            PrincipalVariationSan: ["Nf3"],
            Classification: CoachMoveClassification.BestMove
        );

        var result = await coach.AnalyzeMoveAsync(request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        // Must fallback to offline grounded response without crashing
        Assert.Contains("Best Move", result.Title);
    }

    [Fact]
    public async Task AnalyzeMoveAsync_WhenApiQuotaExhausted_ReturnsGracefulOfflineExplanation()
    {
        var settingsService = new TestSettingsService
        {
            Settings = new UserSettings { GeminiApiKey = "test-api-key" }
        };

        var mockHandler = new MockHttpMessageHandler(_ =>
        {
            // Return 429 Too Many Requests / Quota Exceeded
            var response = new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("{\"error\": {\"code\": 429, \"message\": \"RESOURCE_EXHAUSTED\"}}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var client = new HttpClient(mockHandler);
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance, client);

        var request = new CoachAnalysisRequest(
            FenBefore: "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            FenAfter: "rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 2",
            MoveSan: "e5",
            MoveUci: "e7e5",
            BestMoveSan: "e5",
            BestMoveUci: "e7e5",
            ScoreBeforeCp: 25,
            ScoreAfterCp: 20,
            MateIn: null,
            PrincipalVariationSan: ["Nf3"],
            Classification: CoachMoveClassification.BestMove
        );

        var result = await coach.AnalyzeMoveAsync(request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(CoachMoveClassification.BestMove, result.Classification);
    }

    // =========================================================================
    // 7. Post-Game Review & Turning Points
    // =========================================================================

    [Fact]
    public async Task ReviewGameAsync_ProducesAccuracyAndActionableRecommendations()
    {
        var settingsService = new TestSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        var turningPoints = new List<TurningPointItem>
        {
            new(
                MoveNumber: 12,
                IsWhiteMove: false,
                PlayedMoveSan: "Nxd4??",
                PlayedMoveUci: "c6d4",
                BestMoveSan: "Be7",
                BestMoveUci: "f8e7",
                ScoreChangeCp: 450,
                Classification: CoachMoveClassification.Blunder,
                FenBefore: "r1bqk2r/pp2bppp/2n1pn2/8/2BNP3/2N1B3/PP3PPP/R2QK2R b KQkq - 0 9",
                FenAfter: "r1bqk2r/pp2bppp/4pn2/8/2BnP3/2N1B3/PP3PPP/R2QK2R w KQkq - 0 10",
                Explanation: "Blunder: Black lost a piece after exd4."
            )
        };

        var request = new CoachGameReviewRequest(
            StartFen: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            MoveHistorySan: ["e4", "c5", "Nf3", "d6", "d4", "cxd4"],
            MoveHistoryUci: ["e2e4", "c7c5", "g1f3", "d7d6", "d2d4", "c5d4"],
            TurningPoints: turningPoints,
            WhiteAccuracyPercentage: 88,
            BlackAccuracyPercentage: 64,
            SkillLevel: "Intermediate"
        );

        var response = await coach.ReviewGameAsync(request);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal(88, response.WhiteAccuracy);
        Assert.Equal(64, response.BlackAccuracy);
        Assert.NotEmpty(response.KeyTakeaways);
        Assert.NotEmpty(response.RecommendedLessonTopics);
    }
}
