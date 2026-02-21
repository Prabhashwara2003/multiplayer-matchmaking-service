using Microsoft.AspNetCore.Mvc;
using MultiplayerMatchmaking.Services;

namespace MultiplayerMatchmaking.Controllers;

[ApiController]
[Route("queue")]
public class QueueController : ControllerBase
{
    private readonly MatchmakingService _mm;
    public QueueController(MatchmakingService mm) => _mm = mm;

    public record JoinRequest(List<string> PlayerIds, string Region);
    public record LeaveRequest(string PlayerId);

    [HttpPost("join")]
    public IActionResult Join([FromBody] JoinRequest req)
    {
        var ok = _mm.JoinParty(req.PlayerIds, req.Region);
        return ok
            ? Ok(new { status = "queued", queueSize = _mm.QueueSize })
            : BadRequest(new { status = "failed", reason = "already queued or matched (or invalid id)" });
    }

    [HttpPost("leave")]
    public IActionResult Leave([FromBody] LeaveRequest req)
    {
        var ok = _mm.LeaveQueue(req.PlayerId);
        return ok
            ? Ok(new { status = "left", queueSize = _mm.QueueSize })
            : NotFound(new { status = "not_found_in_queue" });
    }
}
