namespace MultiplayerMatchmaking.Models;

public class PlayerProfile
{
    public required string PlayerId { get; init; }
    public int Mmr { get; set; }
    public int MatchesPlayed { get; set; }
}