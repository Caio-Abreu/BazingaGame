using BazingaGame.Models;
using BazingaGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BazingaGame.Controllers;

[ApiController]
public class GameController(
    IGameService gameService,
    IRandomService randomService,
    ILogger<GameController> logger) : ControllerBase
{
    // Falls back to IP address so the API still works without a session header.
    // Max 128 chars to prevent oversized strings becoming Redis keys or log noise.
    private string PlayerSessionId =>
        Request.Headers.TryGetValue("X-Player-Id", out var id)
            && !string.IsNullOrWhiteSpace(id)
            && id.ToString().Length <= 128
            ? id.ToString()
            : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

    [HttpGet("/choices")]
    [EnableRateLimiting("read")]
    [ProducesResponseType(typeof(IReadOnlyList<Choice>), 200)]
    public ActionResult<IReadOnlyList<Choice>> GetChoices() =>
        Ok(gameService.GetAllChoices());

    [HttpGet("/choice")]
    [EnableRateLimiting("read")]
    [ProducesResponseType(typeof(Choice), 200)]
    [ProducesResponseType(429)]
    public async Task<ActionResult<Choice>> GetChoice()
    {
        var id = await randomService.GetRandomChoiceIdAsync();
        return Ok(gameService.GetChoiceById(id));
    }

    [HttpPost("/play")]
    [EnableRateLimiting("play")]
    [ProducesResponseType(typeof(PlayResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(429)]
    public async Task<ActionResult<PlayResult>> Play([FromBody] PlayRequest request)
    {
        var computerId = await randomService.GetRandomChoiceIdAsync();
        var result = gameService.DetermineResult(request.Player, computerId);
        await gameService.AddToScoreboardAsync(PlayerSessionId, result);

        logger.LogInformation(
            "Game played: sessionId={SessionId} player={Player} computer={Computer} result={Result}",
            PlayerSessionId, request.Player, computerId, result.Results);

        return Ok(result);
    }

    [HttpGet("/scoreboard")]
    [EnableRateLimiting("read")]
    [ProducesResponseType(typeof(IReadOnlyList<PlayResult>), 200)]
    public async Task<ActionResult<IReadOnlyList<PlayResult>>> GetScoreboard() =>
        Ok(await gameService.GetScoreboardAsync(PlayerSessionId));

    [HttpDelete("/scoreboard")]
    [EnableRateLimiting("play")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ResetScoreboard()
    {
        await gameService.ResetScoreboardAsync(PlayerSessionId);
        return NoContent();
    }
}
