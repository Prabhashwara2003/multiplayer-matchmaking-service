namespace MultiplayerMatchmaking.Models;

public class Match
{
    public required string MatchId { get; init; }
    public required List<string> Players { get; init; }
    public required string Region { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
