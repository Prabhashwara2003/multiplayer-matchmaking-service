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

    private readonly ConcurrentDictionary<string, PlayerProfile> _players = new();

    private const int KFactor = 32;

    public int QueueSize => _queuesByRegion.Values.Sum(q => q.Count);

    public bool JoinParty(List<string> playerIds, string region, Dictionary<string, int> playerMmrs)
    {
        if (playerIds == null || playerIds.Count == 0) return false;
        if (playerIds.Count > 2) return false; // limit party size 2
        if (string.IsNullOrWhiteSpace(region)) return false;
        if (playerMmrs == null) return false;

        region = region.Trim();

        // Ensure MMR provided for each playerId (avoids KeyNotFound)
        foreach (var pid in playerIds)
            if (!playerMmrs.ContainsKey(pid))
                return false;

        // Check none already matched
        foreach (var pid in playerIds)
            if (_playerToMatch.ContainsKey(pid))
                return false;

        // Resolve MMRs from SERVER store (client MMR only used on first creation)
        var resolvedMmrs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var pid in playerIds)
        {
            var profile = GetOrCreatePlayer(pid, playerMmrs[pid]); // only uses given MMR if player is new
            resolvedMmrs[pid] = profile.Mmr;                      // authoritative MMR
        }

        var q = GetQueue(region);

        q.Enqueue(new PartyQueueEntry
        {
            PlayerIds = playerIds,
            Region = region,
            PlayerMmrs = resolvedMmrs
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

    public PlayerProfile GetOrCreatePlayer(string playerId, int initialMmr = 1500)
    {
        return _players.GetOrAdd(playerId, id => new PlayerProfile
        {
            PlayerId = id,
            Mmr = initialMmr,
            MatchesPlayed = 0
        });
    }
    public PlayerProfile? TryGetPlayer(string playerId)
    {
        return _players.TryGetValue(playerId, out var p) ? p : null;
    }
    private double ExpectedScore(int ratingA, int ratingB)
    {
        return 1.0 / (1.0 + Math.Pow(10, (ratingB - ratingA) / 400.0));
    }

    public bool ReportMatchResult(string matchId, int winningTeam)
    {
        if (!_matches.TryGetValue(matchId, out var match))
            return false;

        if (match.Status != "Confirmed")
            return false;

        if (winningTeam != 1 && winningTeam != 2)
            return false;

        if (match.Players.Count != 4)
            return false;

        var team1 = match.Players.Take(2).ToList();
        var team2 = match.Players.Skip(2).Take(2).ToList();

        var team1Avg = team1.Select(p => _players[p].Mmr).Average();
        var team2Avg = team2.Select(p => _players[p].Mmr).Average();

        var expected1 = ExpectedScore((int)team1Avg, (int)team2Avg);
        var expected2 = ExpectedScore((int)team2Avg, (int)team1Avg);

        double score1 = winningTeam == 1 ? 1 : 0;
        double score2 = winningTeam == 2 ? 1 : 0;

        foreach (var p in team1)
        {
            var profile = _players[p];
            profile.Mmr += (int)(KFactor * (score1 - expected1));
            profile.MatchesPlayed++;
        }

        foreach (var p in team2)
        {
            var profile = _players[p];
            profile.Mmr += (int)(KFactor * (score2 - expected2));
            profile.MatchesPlayed++;
        }

        match.Status = "Completed";

        foreach (var p in match.Players)
            _playerToMatch.TryRemove(p, out _);

        return true;
    }
}
