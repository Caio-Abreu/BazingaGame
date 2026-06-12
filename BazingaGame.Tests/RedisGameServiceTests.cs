using System.Text.Json;
using BazingaGame.Models;
using BazingaGame.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace BazingaGame.Tests;

/// <summary>
/// Unit tests for RedisGameService using a mocked IConnectionMultiplexer.
/// We test behavior (what keys are written, what is returned) without a real Redis server.
/// </summary>
public class RedisGameServiceTests
{
    private static readonly PlayResult Win = new("win", 1, 3);
    private static readonly PlayResult Lose = new("lose", 3, 1);

    private static (RedisGameService sut, Mock<IDatabase> dbMock) BuildSut(
        params string[] storedEntries)
    {
        var dbMock = new Mock<IDatabase>();
        var batchMock = new Mock<IBatch>();

        // Simulate stored entries for ListRangeAsync
        var redisValues = storedEntries
            .Select(e => (RedisValue)e)
            .ToArray();

        dbMock.Setup(d => d.ListRangeAsync(It.IsAny<RedisKey>(), 0, 9, CommandFlags.None))
            .ReturnsAsync(redisValues);

        dbMock.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(true);

        batchMock.Setup(b => b.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), When.Always, CommandFlags.None))
            .ReturnsAsync(1L);
        batchMock.Setup(b => b.ListTrimAsync(It.IsAny<RedisKey>(), 0, 9, CommandFlags.None))
            .Returns(Task.CompletedTask);
        batchMock.Setup(b => b.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), ExpireWhen.Always, CommandFlags.None))
            .ReturnsAsync(true);

        dbMock.Setup(d => d.CreateBatch(null)).Returns(batchMock.Object);

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(-1, null)).Returns(dbMock.Object);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("Game:ScoreboardExpirationHours", "6")])
            .Build();

        var sut = new RedisGameService(multiplexerMock.Object, config, NullLogger<RedisGameService>.Instance);
        return (sut, dbMock);
    }

    // --- static methods delegate to GameRules ---

    [Fact]
    public void GetAllChoices_ReturnsFiveChoices()
    {
        var (sut, _) = BuildSut();
        Assert.Equal(5, sut.GetAllChoices().Count);
    }

    [Fact]
    public void GetChoiceById_ReturnsCorrectName()
    {
        var (sut, _) = BuildSut();
        Assert.Equal("rock", sut.GetChoiceById(1).Name);
    }

    [Theory]
    [InlineData(1, 3, "win")]
    [InlineData(3, 1, "lose")]
    [InlineData(1, 1, "tie")]
    public void DetermineResult_MatchesGameRules(int player, int computer, string expected)
    {
        var (sut, _) = BuildSut();
        Assert.Equal(expected, sut.DetermineResult(player, computer).Results);
    }

    // --- GetScoreboardAsync ---

    [Fact]
    public async Task GetScoreboard_WhenEmpty_ReturnsEmptyList()
    {
        var (sut, _) = BuildSut();
        var board = await sut.GetScoreboardAsync("session-1");
        Assert.Empty(board);
    }

    [Fact]
    public async Task GetScoreboard_DeserializesEntriesCorrectly()
    {
        var entry = JsonSerializer.Serialize(Win);
        var (sut, _) = BuildSut(entry);

        var board = await sut.GetScoreboardAsync("session-1");

        Assert.Single(board);
        Assert.Equal("win", board[0].Results);
        Assert.Equal(1, board[0].Player);
        Assert.Equal(3, board[0].Computer);
    }

    [Fact]
    public async Task GetScoreboard_SkipsCorruptEntries()
    {
        var good = JsonSerializer.Serialize(Win);
        var (sut, _) = BuildSut(good, "not-valid-json{{{{");

        var board = await sut.GetScoreboardAsync("session-1");

        Assert.Single(board); // corrupt entry skipped
    }

    [Fact]
    public async Task GetScoreboard_WhenRedisThrows_ReturnsEmpty()
    {
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ListRangeAsync(It.IsAny<RedisKey>(), 0, 9, CommandFlags.None))
            .ThrowsAsync(new RedisException("connection lost"));

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(-1, null)).Returns(dbMock.Object);

        var sut = new RedisGameService(
            multiplexerMock.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<RedisGameService>.Instance);

        var board = await sut.GetScoreboardAsync("session-1");
        Assert.Empty(board);
    }

    // --- AddToScoreboardAsync ---

    [Fact]
    public async Task AddToScoreboard_CallsBatchOperations()
    {
        var dbMock = new Mock<IDatabase>();
        var batchMock = new Mock<IBatch>();

        batchMock.Setup(b => b.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), When.Always, CommandFlags.None))
            .ReturnsAsync(1L);
        batchMock.Setup(b => b.ListTrimAsync(It.IsAny<RedisKey>(), 0, 9, CommandFlags.None))
            .Returns(Task.CompletedTask);
        batchMock.Setup(b => b.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), ExpireWhen.Always, CommandFlags.None))
            .ReturnsAsync(true);

        dbMock.Setup(d => d.CreateBatch(null)).Returns(batchMock.Object);

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(-1, null)).Returns(dbMock.Object);

        var sut = new RedisGameService(
            multiplexerMock.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<RedisGameService>.Instance);

        await sut.AddToScoreboardAsync("session-1", Win);

        batchMock.Verify(b => b.ListLeftPushAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("session-1")),
            It.IsAny<RedisValue>(), When.Always, CommandFlags.None), Times.Once);

        batchMock.Verify(b => b.ListTrimAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("session-1")),
            0, 9, CommandFlags.None), Times.Once);

        batchMock.Verify(b => b.KeyExpireAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("session-1")),
            It.IsAny<TimeSpan?>(), ExpireWhen.Always, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task AddToScoreboard_WhenRedisThrows_DoesNotThrow()
    {
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.CreateBatch(null)).Throws(new RedisException("connection lost"));

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(-1, null)).Returns(dbMock.Object);

        var sut = new RedisGameService(
            multiplexerMock.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<RedisGameService>.Instance);

        var ex = await Record.ExceptionAsync(() => sut.AddToScoreboardAsync("session-1", Win));
        Assert.Null(ex);
    }

    // --- ResetScoreboardAsync ---

    [Fact]
    public async Task ResetScoreboard_DeletesCorrectKey()
    {
        var (sut, dbMock) = BuildSut();

        await sut.ResetScoreboardAsync("session-abc");

        dbMock.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey>(k => k.ToString() == "scoreboard:session-abc"),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ResetScoreboard_WhenRedisThrows_DoesNotThrow()
    {
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ThrowsAsync(new RedisException("connection lost"));

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(-1, null)).Returns(dbMock.Object);

        var sut = new RedisGameService(
            multiplexerMock.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<RedisGameService>.Instance);

        var ex = await Record.ExceptionAsync(() => sut.ResetScoreboardAsync("session-1"));
        Assert.Null(ex);
    }
}
