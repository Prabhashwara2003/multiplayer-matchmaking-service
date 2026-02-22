namespace MultiplayerMatchmaking.Models;

public class Match
{
    public required string MatchId { get; init; }
    public required List<string> Players { get; init; }
    public required string Region { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public HashSet<string> AcceptedPlayers { get; init; } = new();
    public string Status { get; set; } = "Pending"; // Pending | Confirmed | Cancelled
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddSeconds(120);
    public required List<List<string>> OriginalParties { get; init; }

}
