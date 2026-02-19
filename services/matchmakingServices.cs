using System.Collections.Concurrent;
using MultiplayerMatchmaking.Models;

namespace MultiplayerMatchmaking.Services;

public class MatchmakingService
{
    private readonly ConcurrentQueue<PlayerQueueEntry> _queue = new();
    private readonly ConcurrentDictionary<string, Match> _matches = new();
    private readonly ConcurrentDictionary<string, string> _playerToMatch = new();

    public int QueueSize => _queue.Count;

    public bool JoinQueue(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return false;
        if (_playerToMatch.ContainsKey(playerId)) return false;
        if (_queue.Any(e => e.PlayerId == playerId)) return false;

        _queue.Enqueue(new PlayerQueueEntry { PlayerId = playerId });
        return true;
    }

    public bool LeaveQueue(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return false;

        var kept = new List<PlayerQueueEntry>();
        bool removed = false;

        while (_queue.TryDequeue(out var entry))
        {
            if (!removed && entry.PlayerId == playerId)
            {
                removed = true;
                continue;
            }
            kept.Add(entry);
        }

        foreach (var e in kept) _queue.Enqueue(e);
        return removed;
    }

    public Match? GetMatchForPlayer(string playerId)
    {
        if (_playerToMatch.TryGetValue(playerId, out var matchId) &&
            _matches.TryGetValue(matchId, out var match))
            return match;

        return null;
    }

    // Called by the background worker
    public Match? TryCreateMatch()
    {
        if (_queue.Count < 2) return null;

        if (!_queue.TryDequeue(out var p1)) return null;
        if (!_queue.TryDequeue(out var p2))
        {
            _queue.Enqueue(p1);
            return null;
        }

        if (_playerToMatch.ContainsKey(p1.PlayerId))
        {
            _queue.Enqueue(p2);
            return null;
        }
        if (_playerToMatch.ContainsKey(p2.PlayerId))
        {
            _queue.Enqueue(p1);
            return null;
        }

        var match = new Match
        {
            MatchId = Guid.NewGuid().ToString("N"),
            Players = new List<string> { p1.PlayerId, p2.PlayerId }
        };

        _matches[match.MatchId] = match;
        _playerToMatch[p1.PlayerId] = match.MatchId;
        _playerToMatch[p2.PlayerId] = match.MatchId;

        return match;
    }
}
