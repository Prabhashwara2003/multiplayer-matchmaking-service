using Microsoft.AspNetCore.Mvc;
using MultiplayerMatchmaking.Services;

namespace MultiplayerMatchmaking.Controllers;

[ApiController]
[Route("player")]

public class PlayerController : ControllerBase
{

    private readonly MatchmakingService _mm;
    public PlayerController(MatchmakingService mm) => _mm = mm;

    [HttpGet("{playerId}")]
    public IActionResult GetPlayer(string playerId)
    {
        var p = _mm.TryGetPlayer(playerId);
        return p != null ? Ok(p) : NotFound();
    }
}
