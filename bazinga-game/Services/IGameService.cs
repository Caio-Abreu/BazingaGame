using BazingaGame.Models;

namespace BazingaGame.Services;

public interface IGameService
{
    IReadOnlyList<Choice> GetAllChoices();
    Choice GetChoiceById(int id);
    PlayResult DetermineResult(int playerId, int computerId);
    void AddToScoreboard(string playerSessionId, PlayResult result);
    IReadOnlyList<PlayResult> GetScoreboard(string playerSessionId);
    void ResetScoreboard(string playerSessionId);
}
