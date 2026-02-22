using System.Collections.Concurrent;
using MultiplayerMatchmaking.Models;

namespace MultiplayerMatchmaking.Services;

public class MatchmakingService
{
    // region -> queue
    private readonly ConcurrentDictionary<string, ConcurrentQueue<PartyQueueEntry>> _queuesByRegion
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, Match> _matches = new();
    private readonly ConcurrentDictionary<string, string> _playerToMatch = new();

    private ConcurrentQueue<PartyQueueEntry> GetQueue(string region)
        => _queuesByRegion.GetOrAdd(region, _ => new ConcurrentQueue<PartyQueueEntry>());

    public int QueueSize => _queuesByRegion.Values.Sum(q => q.Count);

    public bool JoinParty(Dictionary<string, int> players, string region)
    {
        if (players == null || players.Count == 0) return false;
        if (players.Count > 2) return false;
        if (string.IsNullOrWhiteSpace(region)) return false;

        foreach (var p in players.Keys)
            if (_playerToMatch.ContainsKey(p))
                return false;

        var q = GetQueue(region.Trim());

        q.Enqueue(new PartyQueueEntry
        {
            PlayerMmrs = players,
            Region = region.Trim()
        });

        return true;
    }

    public bool LeaveQueue(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return false;

        bool removedAny = false;

        foreach (var kv in _queuesByRegion)
        {
            var q = kv.Value;

            var kept = new List<PartyQueueEntry>();

            while (q.TryDequeue(out var party))
            {
                // If this party contains the player → remove whole party
                if (party.PlayerIds.Contains(playerId))
                {
                    removedAny = true;
                    continue;
                }

                kept.Add(party);
            }

            foreach (var p in kept)
                q.Enqueue(p);
        }

        return removedAny;
    }

    public Match? GetMatchForPlayer(string playerId)
    {
        if (_playerToMatch.TryGetValue(playerId, out var matchId) &&
            _matches.TryGetValue(matchId, out var match))
            return match;

        return null;
    }

    // Called by background worker
    public Match? TryCreateMatch()
    {
        foreach (var kv in _queuesByRegion)
        {
            var region = kv.Key;
            var q = kv.Value;

            if (q.Count == 0) continue;

            var parties = new List<PartyQueueEntry>();
            int totalPlayers = 0;

            var tempList = new List<PartyQueueEntry>();

            while (q.TryDequeue(out var party))
            {
                tempList.Add(party);

                if (totalPlayers + party.Size <= 4)
                {
                    parties.Add(party);
                    totalPlayers += party.Size;
                }

                if (totalPlayers == 4)
                    break;
            }

            if (totalPlayers == 4)
            {
                var originalParties = parties.Select(p => p.PlayerIds).ToList();

                var players = parties.SelectMany(p => p.PlayerIds).ToList();

                var match = new Match
                {
                    MatchId = Guid.NewGuid().ToString("N"),
                    Players = players,
                    Region = region,
                    OriginalParties = originalParties
                };

                _matches[match.MatchId] = match;

                foreach (var p in players)
                    _playerToMatch[p] = match.MatchId;

                return match;
            }

            // Not enough players → requeue all
            foreach (var p in tempList)
                q.Enqueue(p);
        }

        return null;
    }
    public bool AcceptMatch(string playerId)
    {
        if (!_playerToMatch.TryGetValue(playerId, out var matchId))
            return false;

        if (!_matches.TryGetValue(matchId, out var match))
            return false;

        if (match.Status != "Pending")
            return false;

        match.AcceptedPlayers.Add(playerId);

        if (match.AcceptedPlayers.Count == match.Players.Count)
        {
            match.Status = "Confirmed";
        }

        return true;
    }
    public void CleanupExpiredMatches()
    {
        var now = DateTime.UtcNow;

        foreach (var match in _matches.Values)
        {
            if (match.Status == "Pending" && match.ExpiresAtUtc < now)
            {
                match.Status = "Cancelled";

                // Remove player-to-match mapping
                foreach (var player in match.Players)
                    _playerToMatch.TryRemove(player, out _);

                // Requeue original parties
                var q = GetQueue(match.Region);

                foreach (var party in match.OriginalParties)
                {
                    q.Enqueue(new PartyQueueEntry
                    {
                        PlayerIds = party,
                        Region = match.Region
                    });
                }
            }
        }
    }
}
