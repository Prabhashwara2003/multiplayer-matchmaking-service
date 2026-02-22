using Microsoft.AspNetCore.Mvc;
using MultiplayerMatchmaking.Services;

namespace MultiplayerMatchmaking.Controllers;

[ApiController]
[Route("match")]
public class MatchController : ControllerBase
{
    private readonly MatchmakingService _mm;
    public MatchController(MatchmakingService mm) => _mm = mm;

    [HttpGet("{playerId}")]
    public IActionResult Get(string playerId)
    {
        var match = _mm.GetMatchForPlayer(playerId);
        return match != null
            ? Ok(match)
            : NotFound(new { status = "no_match_yet" });
    }
    [HttpPost("{matchId}/accept/{playerId}")]
    public IActionResult Accept(string matchId, string playerId)
    {
        var success = _mm.AcceptMatch(playerId);

        return success
            ? Ok(new { status = "accepted" })
            : BadRequest(new { status = "failed" });
    }
}
