namespace BazingaGame.Services;

public interface IRandomService
{
    Task<int> GetRandomChoiceIdAsync();
}
