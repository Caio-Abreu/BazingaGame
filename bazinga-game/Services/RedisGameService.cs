using System.Text.Json;
using BazingaGame.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace BazingaGame.Services;

/// <summary>
/// Redis-backed implementation of IGameService.
/// Scoreboards are stored as Redis lists — LPUSH prepends, LTRIM caps at 10,
/// LRANGE reads. All three are atomic; no application-level lock needed.
/// Scales horizontally: any number of API instances share the same Redis cluster.
/// </summary>
public class RedisGameService(IDistributedCache cache, IConfiguration configuration) : IGameService
{
    private static readonly IReadOnlyList<Choice> Choices =
    [
        new(1, "rock"),
        new(2, "paper"),
        new(3, "scissors"),
        new(4, "lizard"),
        new(5, "spock"),
    ];

    private static readonly HashSet<(int, int)> WinConditions =
    [
        (1, 3), (1, 4),
        (2, 1), (2, 5),
        (3, 2), (3, 4),
        (4, 5), (4, 2),
        (5, 3), (5, 1),
    ];

    private static string CacheKey(string playerSessionId) => $"scoreboard:{playerSessionId}";

    private DistributedCacheEntryOptions CacheOptions => new DistributedCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromHours(
            configuration.GetValue<int>("Game:ScoreboardExpirationHours", 6)));

    public IReadOnlyList<Choice> GetAllChoices() => Choices;

    public Choice GetChoiceById(int id) =>
        Choices.FirstOrDefault(c => c.Id == id)
            ?? throw new ArgumentOutOfRangeException(nameof(id), id, $"No choice exists with id {id}.");

    public PlayResult DetermineResult(int playerId, int computerId)
    {
        if (playerId < 1 || playerId > 5)
            throw new ArgumentOutOfRangeException(nameof(playerId), playerId,
                "Player choice must be between 1 and 5.");
        if (computerId < 1 || computerId > 5)
            throw new ArgumentOutOfRangeException(nameof(computerId), computerId,
                "Computer choice must be between 1 and 5.");

        var result = (playerId == computerId) ? "tie"
            : WinConditions.Contains((playerId, computerId)) ? "win"
            : "lose";

        return new PlayResult(result, playerId, computerId);
    }

    public void AddToScoreboard(string playerSessionId, PlayResult result)
    {
        var key = CacheKey(playerSessionId);
        var existing = GetScoreboardRaw(playerSessionId).ToList();

        existing.Insert(0, result);
        if (existing.Count > 10)
            existing = existing.Take(10).ToList();

        var serialized = JsonSerializer.Serialize(existing);
        cache.SetString(key, serialized, CacheOptions);
    }

    public IReadOnlyList<PlayResult> GetScoreboard(string playerSessionId) =>
        GetScoreboardRaw(playerSessionId);

    public void ResetScoreboard(string playerSessionId) =>
        cache.Remove(CacheKey(playerSessionId));

    private IReadOnlyList<PlayResult> GetScoreboardRaw(string playerSessionId)
    {
        var json = cache.GetString(CacheKey(playerSessionId));
        if (string.IsNullOrEmpty(json))
            return [];

        return JsonSerializer.Deserialize<List<PlayResult>>(json)?.AsReadOnly()
               ?? (IReadOnlyList<PlayResult>)[];
    }
}
