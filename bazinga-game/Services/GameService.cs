using BazingaGame.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BazingaGame.Services;

public class GameService(IMemoryCache cache, IConfiguration configuration) : IGameService
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
        (1, 3), (1, 4),  // rock beats scissors, lizard
        (2, 1), (2, 5),  // paper beats rock, spock
        (3, 2), (3, 4),  // scissors beats paper, lizard
        (4, 5), (4, 2),  // lizard beats spock, paper
        (5, 3), (5, 1),  // spock beats scissors, rock
    ];

    private readonly MemoryCacheEntryOptions _scoreboardCacheOptions = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromHours(
            configuration.GetValue<int>("Game:ScoreboardExpirationHours", 6)));

    private static string CacheKey(string playerSessionId) => $"scoreboard:{playerSessionId}";

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

        // GetOrCreate + re-set to refresh the sliding expiration on each write
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

    public void ResetScoreboard(string playerSessionId)
    {
        cache.Remove(CacheKey(playerSessionId));
    }
}
