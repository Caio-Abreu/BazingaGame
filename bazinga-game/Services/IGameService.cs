using BazingaGame.Models;

namespace BazingaGame.Services;

public interface IGameService
{
    IReadOnlyList<Choice> GetAllChoices();
    Choice GetChoiceById(int id);
    PlayResult DetermineResult(int playerId, int computerId);
    Task AddToScoreboardAsync(string playerSessionId, PlayResult result);
    Task<IReadOnlyList<PlayResult>> GetScoreboardAsync(string playerSessionId);
    Task ResetScoreboardAsync(string playerSessionId);
}
