using FxSandbox.Domain;

namespace FxSandbox.Services;

public sealed class OrderMatchingService(ITradingEngine engine, ILogger<OrderMatchingService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Order matching service started");

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(500, ct);

            var rates = engine.GetRates();
            var pending = engine.GetOrders()
                .Where(o => o.Status == OrderStatus.Pending)
                .ToList();

            foreach (var order in pending)
            {
                if (!rates.TryGetValue(order.Pair, out var currentRate)) continue;

                var shouldFill = order.Side == OrderSide.Buy
                    ? currentRate <= order.LimitPrice
                    : currentRate >= order.LimitPrice;

                if (shouldFill)
                {
                    if (engine.TryFillOrder(order, currentRate))
                        logger.LogDebug(
                            "Matched order {Id}: {Side} {Pair} @ {Rate}",
                            order.Id, order.Side, order.Pair, currentRate);
                }
            }
        }

        logger.LogInformation("Order matching service stopped");
    }
}
