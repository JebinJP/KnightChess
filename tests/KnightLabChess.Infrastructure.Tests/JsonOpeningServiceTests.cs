using KnightLabChess.Infrastructure.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnightLabChess.Infrastructure.Tests;

public class JsonOpeningServiceTests
{
    private readonly JsonOpeningService _openingService;

    public JsonOpeningServiceTests()
    {
        _openingService = new JsonOpeningService(
            AppDomain.CurrentDomain.BaseDirectory,
            NullLogger<JsonOpeningService>.Instance);
    }

    [Fact]
    public async Task GetAllOpeningsAsync_LoadsOpeningsFromDisk()
    {
        var openings = await _openingService.GetAllOpeningsAsync();

        Assert.NotEmpty(openings);
        Assert.True(openings.Count >= 10);
        Assert.Contains(openings, o => o.Eco == "B90" && o.Name.Contains("Sicilian"));
        Assert.Contains(openings, o => o.Eco == "C50" && o.Name.Contains("Italian"));
    }

    [Fact]
    public async Task SearchOpeningsAsync_MatchesByNameAndEco()
    {
        var sicilians = await _openingService.SearchOpeningsAsync("Sicilian");
        Assert.NotEmpty(sicilians);
        Assert.All(sicilians, o => Assert.Contains("Sicilian", o.Name, StringComparison.OrdinalIgnoreCase));

        var c50 = await _openingService.SearchOpeningsAsync("C50");
        Assert.Single(c50);
        Assert.Equal("Italian Game", c50[0].Name);
    }

    [Fact]
    public async Task GetCandidateMovesAsync_ReturnsNextContinuations()
    {
        // After 1. e4
        var candidates = await _openingService.GetCandidateMovesAsync(["e2e4"]);

        Assert.NotEmpty(candidates);
        // Black plays e7e5 (Open Game), c7c5 (Sicilian), e7e6 (French), c7c6 (Caro-Kann)
        Assert.Contains(candidates, c => c.UciMove == "c7c5");
        Assert.Contains(candidates, c => c.UciMove == "e7e5");
    }

    [Fact]
    public async Task FindOpeningByMovesAsync_IdentifiesPlayedMoves()
    {
        // 1. e4 c5 2. c3 (Alapin Sicilian)
        var alapin = await _openingService.FindOpeningByMovesAsync(["e2e4", "c7c5", "c2c3"]);

        Assert.NotNull(alapin);
        Assert.Equal("B22", alapin.Eco);
        Assert.Equal("Alapin Variation", alapin.Variation);
    }
}
