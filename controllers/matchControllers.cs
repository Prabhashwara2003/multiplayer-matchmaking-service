using Microsoft.AspNetCore.Mvc;
using MultiplayerMatchmaking.Services;

namespace MultiplayerMatchmaking.Controllers;

[ApiController]
[Route("match")]
public class MatchController : ControllerBase
{
    private readonly MatchmakingService _mm;
    public MatchController(MatchmakingService mm) => _mm = mm;

    public record MatchResultRequest(int WinningTeam);

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

    [HttpPost("{matchId}/result")]
    public IActionResult ReportResult(string matchId, [FromBody] MatchResultRequest req)
    {
        var ok = _mm.ReportMatchResult(matchId, req.WinningTeam);
        return ok
            ? Ok(new { status = "mmr_updated" })
            : BadRequest(new { status = "invalid_match_or_state" });
    }
}
