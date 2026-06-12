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

    public void AddToScoreboard(string playerSessionId, PlayResult result)
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
        // Set would silently overwrite the first. Acceptable at this scale; would need
        // a distributed lock or IDistributedCache + optimistic concurrency for production.
        cache.Set(key, board, _scoreboardCacheOptions);
    }

    public IReadOnlyList<PlayResult> GetScoreboard(string playerSessionId)
    {
        if (!cache.TryGetValue(CacheKey(playerSessionId), out List<PlayResult>? board) || board is null)
            return Array.Empty<PlayResult>();

        lock (board)
        {
            return board.ToList().AsReadOnly();
        }
    }

    public void ResetScoreboard(string playerSessionId) =>
        cache.Remove(CacheKey(playerSessionId));
}
