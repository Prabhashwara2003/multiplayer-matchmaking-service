namespace MultiplayerMatchmaking.Models;

public class PlayerQueueEntry
{
    public required string PlayerId { get; init; }
    public DateTime JoinedAtUtc { get; init; } = DateTime.UtcNow;
}
