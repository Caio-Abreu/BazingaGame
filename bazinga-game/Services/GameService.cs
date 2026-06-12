using BazingaGame.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BazingaGame.Services;

public class GameService(IMemoryCache cache, IConfiguration configuration) : IGameService
{
    private readonly MemoryCacheEntryOptions _scoreboardCacheOptions = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromHours(
            configuration.GetValue<int>("Game:ScoreboardExpirationHours", 6)));

    private static string CacheKey(string playerSessionId) => $"scoreboard:{playerSessionId}";

    public IReadOnlyList<Choice> GetAllChoices() => GameRules.Choices;

    public Choice GetChoiceById(int id) => GameRules.GetChoiceById(id);

    public PlayResult DetermineResult(int playerId, int computerId) =>
        GameRules.DetermineResult(playerId, computerId);

    public Task AddToScoreboardAsync(string playerSessionId, PlayResult result)
    {
        var key = CacheKey(playerSessionId);
        var board = cache.GetOrCreate(key, _ => new List<PlayResult>())!;

        lock (board)
        {
            board.Insert(0, result);
            if (board.Count > 10)
                board.RemoveAt(10);
        }

        // Known race: GetOrCreate + Set are not atomic. Under sustained memory pressure
        // two concurrent requests could each get distinct List instances and the second
        // Set would silently overwrite the first. Acceptable at this scale; production
        // uses RedisGameService which eliminates this with atomic LPUSH+LTRIM.
        cache.Set(key, board, _scoreboardCacheOptions);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlayResult>> GetScoreboardAsync(string playerSessionId)
    {
        if (!cache.TryGetValue(CacheKey(playerSessionId), out List<PlayResult>? board) || board is null)
            return Task.FromResult<IReadOnlyList<PlayResult>>(Array.Empty<PlayResult>());

        lock (board)
        {
            return Task.FromResult<IReadOnlyList<PlayResult>>(board.ToList().AsReadOnly());
        }
    }

    public Task ResetScoreboardAsync(string playerSessionId)
    {
        cache.Remove(CacheKey(playerSessionId));
        return Task.CompletedTask;
    }
}
