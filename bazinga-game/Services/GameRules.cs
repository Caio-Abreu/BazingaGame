using BazingaGame.Models;

namespace BazingaGame.Services;

internal static class GameRules
{
    internal static readonly IReadOnlyList<Choice> Choices =
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

    internal static Choice GetChoiceById(int id) =>
        Choices.FirstOrDefault(c => c.Id == id)
            ?? throw new ArgumentOutOfRangeException(nameof(id), id, $"No choice exists with id {id}.");

    internal static PlayResult DetermineResult(int playerId, int computerId)
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
}
