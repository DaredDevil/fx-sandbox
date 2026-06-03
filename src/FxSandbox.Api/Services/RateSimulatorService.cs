namespace FxSandbox.Services;

public sealed class RateSimulatorService(ITradingEngine engine, ILogger<RateSimulatorService> logger)
    : BackgroundService
{
    internal const double MinDelta = -0.001;
    internal const double MaxDelta = 0.001;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Rate simulator service started");

        while (!ct.IsCancellationRequested)
        {
            foreach (var pair in TradingEngine.SupportedPairs)
            {
                var current = engine.GetRate(pair);
                var newRate = CalculateNextRate(current, Random.Shared.NextDouble());
                engine.UpdateRate(pair, newRate);
                logger.LogTrace("Rate updated: {Pair} → {Rate}", pair, newRate);
            }

            await Task.Delay(500, ct);
        }

        logger.LogInformation("Rate simulator service stopped");
    }

    internal static decimal CalculateNextRate(decimal current, double randomSample)
    {
        if (randomSample is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(randomSample), "Random sample must be between 0 and 1.");

        var delta = MinDelta + randomSample * (MaxDelta - MinDelta);
        var newRate = current * (decimal)(1.0 + delta);

        return Math.Round(newRate, 6);
    }
}
