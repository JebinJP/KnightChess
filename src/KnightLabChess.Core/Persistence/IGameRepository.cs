using KnightLabChess.Core.Models;

namespace KnightLabChess.Core.Persistence;

/// <summary>
/// Contract for persisting and retrieving chess match records and journals.
/// </summary>
public interface IGameRepository
{
    Task<int> SaveGameAsync(GameRecord game);
    Task<GameRecord?> GetGameAsync(int id);
    Task<IReadOnlyList<GameRecord>> GetRecentGamesAsync(int limit = 20);
    Task DeleteGameAsync(int id);
}
