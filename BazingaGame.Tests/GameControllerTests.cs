using System.Net;
using System.Net.Http.Json;
using BazingaGame.Models;
using BazingaGame.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BazingaGame.Tests;

public class FakeRandomService(int choiceId = 1) : IRandomService
{
    public Task<int> GetRandomChoiceIdAsync() => Task.FromResult(choiceId);
}

public class ThrowingRandomService : IRandomService
{
    public Task<int> GetRandomChoiceIdAsync() =>
        throw new HttpRequestException("External random API unavailable");
}

/// <summary>
/// Each test class gets a fresh WebApplicationFactory — isolated singleton scoreboard per test.
/// NOT using IClassFixture because GameService is Singleton: shared state across tests
/// would make results order-dependent.
/// </summary>
public class GameControllerTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GameControllerTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton<IRandomService, FakeRandomService>()));

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Player-Id", "test-player-session");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // --- GET /choices ---

    [Fact]
    public async Task GetChoices_Returns200()
    {
        var response = await _client.GetAsync("/choices");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetChoices_ReturnsFiveItems()
    {
        var choices = await _client.GetFromJsonAsync<List<Choice>>("/choices");
        Assert.NotNull(choices);
        Assert.Equal(5, choices.Count);
    }

    [Fact]
    public async Task GetChoices_EachItemHasIdAndName()
    {
        var choices = await _client.GetFromJsonAsync<List<Choice>>("/choices");
        Assert.All(choices!, c =>
        {
            Assert.InRange(c.Id, 1, 5);
            Assert.False(string.IsNullOrEmpty(c.Name));
        });
    }

    // --- GET /choice ---

    [Fact]
    public async Task GetChoice_Returns200()
    {
        var response = await _client.GetAsync("/choice");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetChoice_ReturnsValidChoice()
    {
        var choice = await _client.GetFromJsonAsync<Choice>("/choice");
        Assert.NotNull(choice);
        Assert.InRange(choice.Id, 1, 5);
    }

    // --- POST /play ---

    [Fact]
    public async Task Play_ValidRequest_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/play", new { player = 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Play_ValidRequest_ReturnsResultWithPlayerAndComputer()
    {
        var response = await _client.PostAsJsonAsync("/play", new { player = 1 });
        var result = await response.Content.ReadFromJsonAsync<PlayResult>();

        Assert.NotNull(result);
        Assert.Equal(1, result.Player);
        Assert.InRange(result.Computer, 1, 5);
        Assert.Contains(result.Results, ["win", "lose", "tie"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task Play_InvalidPlayerId_Returns400(int invalidId)
    {
        var response = await _client.PostAsJsonAsync("/play", new { player = invalidId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Play_InvalidPlayerId_ResponseBodyContainsErrorMessage()
    {
        var response = await _client.PostAsJsonAsync("/play", new { player = 99 });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("error", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Play_MissingBody_Returns400()
    {
        var response = await _client.PostAsync("/play",
            new StringContent("", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- GET /scoreboard ---

    [Fact]
    public async Task GetScoreboard_Returns200()
    {
        var response = await _client.GetAsync("/scoreboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetScoreboard_AfterPlay_ContainsResult()
    {
        await _client.PostAsJsonAsync("/play", new { player = 1 });

        var board = await _client.GetFromJsonAsync<List<PlayResult>>("/scoreboard");

        Assert.NotNull(board);
        Assert.NotEmpty(board);
    }

    // --- DELETE /scoreboard ---

    [Fact]
    public async Task DeleteScoreboard_Returns204()
    {
        var response = await _client.DeleteAsync("/scoreboard");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteScoreboard_ClearsResults()
    {
        await _client.PostAsJsonAsync("/play", new { player = 1 });
        await _client.DeleteAsync("/scoreboard");

        var board = await _client.GetFromJsonAsync<List<PlayResult>>("/scoreboard");
        Assert.Empty(board!);
    }

    // --- GET /health ---

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>
/// Unit tests for RandomService fallback behavior.
/// The fallback lives inside RandomService.GetRandomChoiceIdAsync(), so it must be tested
/// by giving RandomService an HttpClient that fails — not by replacing IRandomService entirely.
/// </summary>
public class RandomServiceFallbackTests
{
    private static RandomService BuildWithFailingHttpClient()
    {
        // HttpMessageHandler that always throws, simulating a down or circuit-open dependency
        var handler = new FailingHttpHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RandomService>.Instance;
        return new RandomService(httpClient, logger);
    }

    [Fact]
    public async Task GetRandomChoiceId_WhenHttpClientThrows_ReturnsFallbackInRange()
    {
        var sut = BuildWithFailingHttpClient();
        var result = await sut.GetRandomChoiceIdAsync();
        Assert.InRange(result, 1, 5);
    }

    [Fact]
    public async Task GetRandomChoiceId_WhenHttpClientThrows_DoesNotThrow()
    {
        var sut = BuildWithFailingHttpClient();
        var ex = await Record.ExceptionAsync(() => sut.GetRandomChoiceIdAsync());
        Assert.Null(ex);
    }
}

internal class FailingHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("Simulated network failure");
}

/// <summary>
/// Tests that rate limiting returns 429 after the per-IP limit is exceeded.
/// Each test class gets its own factory so the rate limit window is fresh.
/// </summary>
public class RateLimitTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RateLimitTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton<IRandomService, FakeRandomService>()));

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Player-Id", "rate-limit-test-session");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Play_After30Requests_Returns429()
    {
        for (var i = 0; i < 30; i++)
            await _client.PostAsJsonAsync("/play", new { player = 1 });

        var response = await _client.PostAsJsonAsync("/play", new { player = 1 });
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Play_RateLimitResponse_ContainsErrorMessage()
    {
        for (var i = 0; i < 30; i++)
            await _client.PostAsJsonAsync("/play", new { player = 1 });

        var response = await _client.PostAsJsonAsync("/play", new { player = 1 });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("error", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Read_After60Requests_Returns429()
    {
        for (var i = 0; i < 60; i++)
            await _client.GetAsync("/choices");

        var response = await _client.GetAsync("/choices");
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}

/// <summary>
/// Tests that CORS headers are present and correct on responses.
/// </summary>
public class CorsTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CorsTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton<IRandomService, FakeRandomService>()));

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetChoices_WithAllowedOrigin_ReturnsAccessControlHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/choices");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await _client.SendAsync(request);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Expected Access-Control-Allow-Origin header to be present");
        Assert.Equal("http://localhost:5173",
            response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task GetChoices_WithDisallowedOrigin_DoesNotReturnAccessControlHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/choices");
        request.Headers.Add("Origin", "https://evil.example.com");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Expected no Access-Control-Allow-Origin header for disallowed origin");
    }

    [Fact]
    public async Task Preflight_WithAllowedOrigin_Returns204()
    {
        // Browser sends OPTIONS before a cross-origin POST
        var request = new HttpRequestMessage(HttpMethod.Options, "/play");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type, X-Player-Id");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

/// <summary>
/// Tests the /health/live and /health/ready endpoints.
/// </summary>
public class HealthCheckTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HealthCheckTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton<IRandomService, FakeRandomService>()));

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task LivenessCheck_AlwaysReturns200()
    {
        // /health/live has no checks — if it responds, the process is alive
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LivenessCheck_ReturnsHealthyBody()
    {
        var response = await _client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }
}
