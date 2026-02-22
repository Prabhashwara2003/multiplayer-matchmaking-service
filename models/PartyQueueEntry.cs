namespace MultiplayerMatchmaking.Models;

public class PartyQueueEntry
{
    public required Dictionary<string, int> PlayerMmrs { get; init; }
    public required List<string> PlayerIds { get; init; }
    public required string Region { get; init; }
    public DateTime JoinedAtUtc { get; init; } = DateTime.UtcNow;

    public int Size => PlayerIds.Count;

    public int GetDynamicTolerance()
    {
        const int baseTolerance = 100;
        const int growthRatePerSecond = 10;

        var secondsWaiting = (int)(DateTime.UtcNow - JoinedAtUtc).TotalSeconds;

        return baseTolerance + (secondsWaiting * growthRatePerSecond);
    }
}