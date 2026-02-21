namespace MultiplayerMatchmaking.Models;

public class PartyQueueEntry
{
    public required List<string> PlayerIds { get; init; }
    public required string Region { get; init; }
    public DateTime JoinedAtUtc { get; init; } = DateTime.UtcNow;

    public int Size => PlayerIds.Count;
}