using FxSandbox.Domain;

namespace FxSandbox.Services;

public sealed class OrderMatchingService(TradingEngine engine) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
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
                    ? currentRate <= order.LimitPrice   // buy fills when rate drops to limit
                    : currentRate >= order.LimitPrice;  // sell fills when rate rises to limit

                if (shouldFill)
                    engine.TryFillOrder(order, currentRate);
            }
        }
    }
}
