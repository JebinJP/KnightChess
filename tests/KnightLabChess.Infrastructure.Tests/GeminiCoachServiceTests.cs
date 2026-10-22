using KnightLabChess.Core.Coach;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Settings;
using KnightLabChess.Infrastructure.Coach;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnightLabChess.Infrastructure.Tests;

public class GeminiCoachServiceTests
{
    private class FakeSettingsService : ISettingsService
    {
        public UserSettings Settings { get; set; } = new();
        public Task<UserSettings> GetSettingsAsync() => Task.FromResult(Settings);
        public Task SaveSettingsAsync(UserSettings settings)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
        public string GetDatabasePath() => "fake.db";
    }

    [Fact]
    public void IsConfigured_WhenApiKeyEmpty_ReturnsFalse()
    {
        var settingsService = new FakeSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        Assert.False(coach.IsConfigured);
    }

    [Fact]
    public void IsConfigured_WhenApiKeyProvided_ReturnsTrue()
    {
        var settingsService = new FakeSettingsService
        {
            Settings = new UserSettings { GeminiApiKey = "AIzaSyFakeKeyTest123" }
        };
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        Assert.True(coach.IsConfigured);
    }

    [Theory]
    [InlineData(CoachMoveClassification.BestMove, "Best Move")]
    [InlineData(CoachMoveClassification.Blunder, "Blunder")]
    [InlineData(CoachMoveClassification.Mistake, "Mistake")]
    [InlineData(CoachMoveClassification.Inaccuracy, "Inaccuracy")]
    [InlineData(CoachMoveClassification.Good, "Solid")]
    public async Task AnalyzeMoveAsync_WithoutApiKey_ReturnsGroundedOfflineResponse(CoachMoveClassification classification, string expectedKeyword)
    {
        var settingsService = new FakeSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

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
            PrincipalVariationSan: ["Nf3", "Nc6"],
            Classification: classification,
            SkillLevel: "Intermediate");

        var response = await coach.AnalyzeMoveAsync(request);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal(classification, response.Classification);
        Assert.Contains(expectedKeyword, response.Title, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(response.Summary));
        Assert.False(string.IsNullOrWhiteSpace(response.TacticalExplanation));
        Assert.False(string.IsNullOrWhiteSpace(response.StrategicAdvice));
    }

    [Fact]
    public async Task ExplainPositionAsync_WithoutApiKey_ReturnsGroundedSummary()
    {
        var settingsService = new FakeSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        var eval = new EngineEvaluation
        {
            ScoreCentipawns = 150,
            Depth = 12,
            BestMoveUci = "e2e4",
            PrincipalVariation = ["e2e4", "e7e5", "g1f3"]
        };

        var summary = await coach.ExplainPositionAsync("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", eval);

        Assert.NotNull(summary);
        Assert.Contains("White", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pawns", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplainPositionAsync_WhenBlackToMoveAndWinning_ReportsBlackAdvantage()
    {
        var settingsService = new FakeSettingsService();
        using var coach = new GeminiCoachService(settingsService, NullLogger<GeminiCoachService>.Instance);

        // Black to move with +2.0 pawns advantage from Black's perspective
        var eval = new EngineEvaluation
        {
            ScoreCentipawns = 200,
            Depth = 12,
            BestMoveUci = "d7d5"
        };

        var summary = await coach.ExplainPositionAsync("rnbqkbnr/ppp1pppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1", eval);

        Assert.NotNull(summary);
        Assert.Contains("Black", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("White holds an advantage", summary, StringComparison.OrdinalIgnoreCase);
    }
}
