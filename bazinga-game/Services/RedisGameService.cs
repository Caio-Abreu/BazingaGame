using System.Text.Json;
using BazingaGame.Models;
using StackExchange.Redis;

namespace BazingaGame.Services;

/// <summary>
/// Redis-backed implementation of IGameService.
///
/// Uses IConnectionMultiplexer (raw StackExchange.Redis) instead of IDistributedCache
/// so we can pipeline LPUSH + LTRIM + EXPIRE as a single atomic transaction.
/// This eliminates the read-modify-write race that IDistributedCache would introduce.
///
/// Failure strategy: if Redis is unreachable, all scoreboard operations degrade gracefully —
/// reads return empty, writes are dropped — so the game keeps working without the dependency.
/// </summary>
public class RedisGameService(
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<RedisGameService> logger) : IGameService
{
    private static string CacheKey(string playerSessionId) => $"scoreboard:{playerSessionId}";

    private TimeSpan Expiration => TimeSpan.FromHours(
        configuration.GetValue<int>("Game:ScoreboardExpirationHours", 6));

    public IReadOnlyList<Choice> GetAllChoices() => GameRules.Choices;

    public Choice GetChoiceById(int id) => GameRules.GetChoiceById(id);

    public PlayResult DetermineResult(int playerId, int computerId) =>
        GameRules.DetermineResult(playerId, computerId);

    public void AddToScoreboard(string playerSessionId, PlayResult result)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = CacheKey(playerSessionId);
            var serialized = JsonSerializer.Serialize(result);

            // Atomic pipeline: LPUSH prepends the new result, LTRIM caps the list at 10,
            // EXPIRE refreshes the sliding window — all in one round-trip.
            var batch = db.CreateBatch();
            _ = batch.ListLeftPushAsync(key, serialized);
            _ = batch.ListTrimAsync(key, 0, 9);
            _ = batch.KeyExpireAsync(key, Expiration);
            batch.Execute();
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable during AddToScoreboard — result dropped. " +
                "ExternalService={ExternalService} PlayerSessionId={PlayerSessionId}",
                "redis", playerSessionId);
        }
    }

    public IReadOnlyList<PlayResult> GetScoreboard(string playerSessionId)
    {
        try
        {
            var db = redis.GetDatabase();
            var entries = db.ListRange(CacheKey(playerSessionId), 0, 9);

            return entries
                .Select(e => JsonSerializer.Deserialize<PlayResult>(e!))
                .Where(r => r is not null)
                .Select(r => r!)
                .ToList()
                .AsReadOnly();
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable during GetScoreboard — returning empty. " +
                "ExternalService={ExternalService} PlayerSessionId={PlayerSessionId}",
                "redis", playerSessionId);
            return Array.Empty<PlayResult>();
        }
    }

    public void ResetScoreboard(string playerSessionId)
    {
        try
        {
            redis.GetDatabase().KeyDelete(CacheKey(playerSessionId));
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable during ResetScoreboard — reset skipped. " +
                "ExternalService={ExternalService} PlayerSessionId={PlayerSessionId}",
                "redis", playerSessionId);
        }
    }
}
