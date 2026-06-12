using System.Text.Json;
using BazingaGame.Models;
using StackExchange.Redis;

namespace BazingaGame.Services;

/// <summary>
/// Redis-backed implementation of IGameService.
///
/// Uses IConnectionMultiplexer (raw StackExchange.Redis) so scoreboard writes pipeline
/// LPUSH + LTRIM + EXPIRE atomically — no read-modify-write race.
///
/// Failure strategy: RedisException is caught on every operation so the game keeps
/// working if Redis is unreachable. Reads return empty; writes and resets are dropped.
/// All failures are logged with structured properties for observability.
/// </summary>
public class RedisGameService(
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<RedisGameService> logger) : IGameService
{
    private static string CacheKey(string playerSessionId) => $"scoreboard:{playerSessionId}";

    // Computed once — configuration does not change at runtime.
    private readonly TimeSpan _expiration = TimeSpan.FromHours(
        configuration.GetValue<int>("Game:ScoreboardExpirationHours", 6));

    public IReadOnlyList<Choice> GetAllChoices() => GameRules.Choices;

    public Choice GetChoiceById(int id) => GameRules.GetChoiceById(id);

    public PlayResult DetermineResult(int playerId, int computerId) =>
        GameRules.DetermineResult(playerId, computerId);

    public async Task AddToScoreboardAsync(string playerSessionId, PlayResult result)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = CacheKey(playerSessionId);
            var serialized = JsonSerializer.Serialize(result);

            // Atomic pipeline: LPUSH prepends, LTRIM caps at 10, EXPIRE refreshes the window.
            var batch = db.CreateBatch();
            var pushTask = batch.ListLeftPushAsync(key, serialized);
            var trimTask = batch.ListTrimAsync(key, 0, 9);
            var expireTask = batch.KeyExpireAsync(key, _expiration);
            batch.Execute();

            await Task.WhenAll(pushTask, trimTask, expireTask);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable during AddToScoreboard — result dropped. " +
                "ExternalService={ExternalService} PlayerSessionId={PlayerSessionId}",
                "redis", playerSessionId);
        }
    }

    public async Task<IReadOnlyList<PlayResult>> GetScoreboardAsync(string playerSessionId)
    {
        try
        {
            var db = redis.GetDatabase();
            var entries = await db.ListRangeAsync(CacheKey(playerSessionId), 0, 9);

            var results = new List<PlayResult>();
            foreach (var entry in entries)
            {
                try
                {
                    var deserialized = JsonSerializer.Deserialize<PlayResult>(entry!);
                    if (deserialized is null) throw new JsonException("Deserialized to null.");
                    results.Add(deserialized);
                }
                catch (JsonException)
                {
                    logger.LogWarning(
                        "Corrupt scoreboard entry in Redis — skipping. " +
                        "PlayerSessionId={PlayerSessionId} Entry={Entry}",
                        playerSessionId, (string)entry!);
                }
            }

            return results.AsReadOnly();
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

    public async Task ResetScoreboardAsync(string playerSessionId)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(CacheKey(playerSessionId));
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
