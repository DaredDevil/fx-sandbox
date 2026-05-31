namespace FxSandbox.Services;

public sealed class RateSimulatorService(ITradingEngine engine) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var pair in TradingEngine.SupportedPairs)
            {
                var current = engine.GetRate(pair);
                // Random walk: Δ ∈ [-0.005, +0.005]
                var delta = Random.Shared.NextDouble() * 0.010 - 0.005;
                var newRate = current * (decimal)(1.0 + delta);
                engine.UpdateRate(pair, Math.Round(newRate, 6));
            }

            await Task.Delay(500, ct);
        }
    }
}
