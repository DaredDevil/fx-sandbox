namespace FxSandbox.Services;

public sealed class RateSimulatorService(ITradingEngine engine, ILogger<RateSimulatorService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Rate simulator service started");

        while (!ct.IsCancellationRequested)
        {
            foreach (var pair in TradingEngine.SupportedPairs)
            {
                var current = engine.GetRate(pair);
                var delta = Random.Shared.NextDouble() * 0.010 - 0.005;
                var newRate = current * (decimal)(1.0 + delta);
                engine.UpdateRate(pair, Math.Round(newRate, 6));
                logger.LogTrace("Rate updated: {Pair} → {Rate}", pair, newRate);
            }

            await Task.Delay(500, ct);
        }

        logger.LogInformation("Rate simulator service stopped");
    }
}
