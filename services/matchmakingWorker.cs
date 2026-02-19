namespace MultiplayerMatchmaking.Services;

public class MatchmakingWorker : BackgroundService
{
    private readonly MatchmakingService _mm;
    private readonly ILogger<MatchmakingWorker> _logger;

    public MatchmakingWorker(MatchmakingService mm, ILogger<MatchmakingWorker> logger)
    {
        _mm = mm;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Matchmaking worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var match = _mm.TryCreateMatch();
            if (match != null)
            {
                _logger.LogInformation("Match created {MatchId} with players {P1}, {P2}",
                    match.MatchId, match.Players[0], match.Players[1]);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
