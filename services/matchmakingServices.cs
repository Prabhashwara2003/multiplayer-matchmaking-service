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

    public bool JoinParty(List<string> playerIds, string region,Dictionary<string, int> PlayerMmrs)
    {
        if (playerIds == null || playerIds.Count == 0) return false;
        if (playerIds.Count > 2) return false; // limit party size 2
        if (string.IsNullOrWhiteSpace(region)) return false;

        region = region.Trim();

        // Check none already matched
        foreach (var pid in playerIds)
        {
            if (_playerToMatch.ContainsKey(pid))
                return false;
        }

        var q = GetQueue(region);

        q.Enqueue(new PartyQueueEntry
        {
            PlayerIds = playerIds,
            Region = region,
            PlayerMmrs = PlayerMmrs
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

            if (q.Count == 0)
                continue;

            var parties = new List<PartyQueueEntry>();
            var tempList = new List<PartyQueueEntry>();
            int totalPlayers = 0;

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
                // 🔹 Collect all player MMRs
                var allMmrs = parties
                    .SelectMany(p => p.PlayerMmrs.Values)
                    .ToList();

                int maxMmr = allMmrs.Max();
                int minMmr = allMmrs.Min();
                int diff = maxMmr - minMmr;

                // 🔹 Get strictest tolerance
                int allowedTolerance = parties
                    .Select(p => p.GetDynamicTolerance())
                    .Min();

                if (diff <= allowedTolerance)
                {
                    var originalParties = parties.ToList();

                    var players = parties
                        .SelectMany(p => p.PlayerIds)
                        .ToList();

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
            }

            // 🔹 If not enough players OR MMR diff too high → requeue everything
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

                foreach (var player in match.Players)
                    _playerToMatch.TryRemove(player, out _);

                var q = GetQueue(match.Region);

                foreach (var party in match.OriginalParties)
                {
                    q.Enqueue(party);
                }
            }
        }
    }
}
