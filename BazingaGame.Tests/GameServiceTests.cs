using BazingaGame.Models;
using BazingaGame.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace BazingaGame.Tests;

public class GameServiceTests
{
    private static IGameService BuildSut() => new GameService(
        new MemoryCache(Options.Create(new MemoryCacheOptions())),
        new ConfigurationBuilder().Build());

    private readonly IGameService _sut = BuildSut();

    // --- DetermineResult ---

    [Theory]
    [InlineData(1, 3)] // rock beats scissors
    [InlineData(1, 4)] // rock beats lizard
    [InlineData(2, 1)] // paper beats rock
    [InlineData(2, 5)] // paper beats spock
    [InlineData(3, 2)] // scissors beats paper
    [InlineData(3, 4)] // scissors beats lizard
    [InlineData(4, 5)] // lizard beats spock
    [InlineData(4, 2)] // lizard beats paper
    [InlineData(5, 3)] // spock beats scissors
    [InlineData(5, 1)] // spock beats rock
    public void DetermineResult_WinningCondition_ReturnsWin(int player, int computer)
    {
        var result = _sut.DetermineResult(player, computer);
        Assert.Equal("win", result.Results);
    }

    [Theory]
    [InlineData(3, 1)] // scissors loses to rock
    [InlineData(4, 1)] // lizard loses to rock
    [InlineData(1, 2)] // rock loses to paper
    [InlineData(5, 2)] // spock loses to paper
    [InlineData(2, 3)] // paper loses to scissors
    [InlineData(4, 3)] // lizard loses to scissors
    [InlineData(5, 4)] // spock loses to lizard
    [InlineData(2, 4)] // paper loses to lizard
    [InlineData(3, 5)] // scissors loses to spock
    [InlineData(1, 5)] // rock loses to spock
    public void DetermineResult_LosingCondition_ReturnsLose(int player, int computer)
    {
        var result = _sut.DetermineResult(player, computer);
        Assert.Equal("lose", result.Results);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void DetermineResult_SameChoice_ReturnsTie(int choice)
    {
        var result = _sut.DetermineResult(choice, choice);
        Assert.Equal("tie", result.Results);
    }

    [Fact]
    public void DetermineResult_ReturnsCorrectPlayerAndComputerIds()
    {
        var result = _sut.DetermineResult(1, 3);
        Assert.Equal(1, result.Player);
        Assert.Equal(3, result.Computer);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 1)]
    [InlineData(-1, 1)]
    public void DetermineResult_InvalidPlayerId_Throws(int badPlayer, int computer)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.DetermineResult(badPlayer, computer));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 6)]
    [InlineData(1, -1)]
    public void DetermineResult_InvalidComputerId_Throws(int player, int badComputer)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.DetermineResult(player, badComputer));
    }

    // --- GetChoiceById ---

    [Theory]
    [InlineData(1, "rock")]
    [InlineData(2, "paper")]
    [InlineData(3, "scissors")]
    [InlineData(4, "lizard")]
    [InlineData(5, "spock")]
    public void GetChoiceById_ValidId_ReturnsCorrectChoice(int id, string expectedName)
    {
        var choice = _sut.GetChoiceById(id);
        Assert.Equal(id, choice.Id);
        Assert.Equal(expectedName, choice.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(999)]
    public void GetChoiceById_InvalidId_ThrowsArgumentOutOfRangeException(int invalidId)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _sut.GetChoiceById(invalidId));
        Assert.Equal("id", ex.ParamName);
        Assert.Contains(invalidId.ToString(), ex.Message);
    }

    // --- GetAllChoices ---

    [Fact]
    public void GetAllChoices_ReturnsFiveChoices()
    {
        Assert.Equal(5, _sut.GetAllChoices().Count);
    }

    [Fact]
    public void GetAllChoices_ContainsExpectedNames()
    {
        var names = _sut.GetAllChoices().Select(c => c.Name).ToList();
        Assert.Contains("rock", names);
        Assert.Contains("paper", names);
        Assert.Contains("scissors", names);
        Assert.Contains("lizard", names);
        Assert.Contains("spock", names);
    }

    [Fact]
    public void GetAllChoices_IdsAreOneToFive()
    {
        var ids = _sut.GetAllChoices().Select(c => c.Id).OrderBy(id => id).ToList();
        Assert.Equal([1, 2, 3, 4, 5], ids);
    }

    // --- Scoreboard ---

    private const string Player1 = "player-session-1";
    private const string Player2 = "player-session-2";

    [Fact]
    public async Task Scoreboard_StartsEmpty()
    {
        Assert.Empty(await _sut.GetScoreboardAsync(Player1));
    }

    [Fact]
    public async Task AddToScoreboard_AddsResult()
    {
        await _sut.AddToScoreboardAsync(Player1, new PlayResult("win", 1, 3));
        Assert.Single(await _sut.GetScoreboardAsync(Player1));
    }

    [Fact]
    public async Task AddToScoreboard_NewestResultIsFirst()
    {
        await _sut.AddToScoreboardAsync(Player1, new PlayResult("win", 1, 3));
        await _sut.AddToScoreboardAsync(Player1, new PlayResult("lose", 2, 5));

        Assert.Equal("lose", (await _sut.GetScoreboardAsync(Player1))[0].Results);
    }

    [Fact]
    public async Task Scoreboard_CapsAtTenItems()
    {
        for (var i = 0; i < 12; i++)
            await _sut.AddToScoreboardAsync(Player1, new PlayResult("tie", 1, 1));

        Assert.Equal(10, (await _sut.GetScoreboardAsync(Player1)).Count);
    }

    [Fact]
    public async Task Scoreboard_EleventhItemDropsOldest()
    {
        for (var i = 0; i < 10; i++)
            await _sut.AddToScoreboardAsync(Player1, new PlayResult("win", 1, 3));

        await _sut.AddToScoreboardAsync(Player1, new PlayResult("lose", 3, 1));

        var board = await _sut.GetScoreboardAsync(Player1);
        Assert.Equal(10, board.Count);
        Assert.Equal("lose", board[0].Results);
        Assert.Equal(9, board.Count(r => r.Results == "win"));
    }

    [Fact]
    public async Task ResetScoreboard_ClearsOnlyThatPlayersResults()
    {
        await _sut.AddToScoreboardAsync(Player1, new PlayResult("win", 1, 3));
        await _sut.AddToScoreboardAsync(Player2, new PlayResult("lose", 2, 1));

        await _sut.ResetScoreboardAsync(Player1);

        Assert.Empty(await _sut.GetScoreboardAsync(Player1));
        Assert.Single(await _sut.GetScoreboardAsync(Player2));
    }

    [Fact]
    public async Task Scoreboard_IsSeparatePerPlayer()
    {
        await _sut.AddToScoreboardAsync(Player1, new PlayResult("win", 1, 3));
        await _sut.AddToScoreboardAsync(Player1, new PlayResult("win", 1, 3));
        await _sut.AddToScoreboardAsync(Player2, new PlayResult("lose", 3, 1));

        Assert.Equal(2, (await _sut.GetScoreboardAsync(Player1)).Count);
        Assert.Single(await _sut.GetScoreboardAsync(Player2));
    }

    [Fact]
    public async Task GetScoreboard_UnknownPlayer_ReturnsEmpty()
    {
        Assert.Empty(await _sut.GetScoreboardAsync("never-played-before"));
    }

    [Fact]
    public async Task GetScoreboard_ReturnsCopy_NotLiveReference()
    {
        await _sut.AddToScoreboardAsync(Player1, new PlayResult("win", 1, 3));
        var snapshot = await _sut.GetScoreboardAsync(Player1);

        await _sut.AddToScoreboardAsync(Player1, new PlayResult("lose", 2, 1));

        Assert.Single(snapshot);
    }
}

/// <summary>
/// Direct unit tests for GameRules — the shared logic used by both GameService and RedisGameService.
/// </summary>
public class GameRulesTests
{
    [Theory]
    [InlineData(1, 3, "win")]
    [InlineData(1, 4, "win")]
    [InlineData(2, 1, "win")]
    [InlineData(3, 2, "win")]
    [InlineData(1, 2, "lose")]
    [InlineData(2, 3, "lose")]
    [InlineData(1, 1, "tie")]
    [InlineData(3, 3, "tie")]
    public void DetermineResult_AllOutcomes(int player, int computer, string expected)
    {
        var result = GameRules.DetermineResult(player, computer);
        Assert.Equal(expected, result.Results);
    }

    [Theory]
    [InlineData(1, "rock")]
    [InlineData(2, "paper")]
    [InlineData(3, "scissors")]
    [InlineData(4, "lizard")]
    [InlineData(5, "spock")]
    public void GetChoiceById_ReturnsCorrectName(int id, string name)
    {
        Assert.Equal(name, GameRules.GetChoiceById(id).Name);
    }

    [Fact]
    public void Choices_HasFiveItems()
    {
        Assert.Equal(5, GameRules.Choices.Count);
    }

    [Fact]
    public void GetChoiceById_InvalidId_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameRules.GetChoiceById(99));
    }
}
