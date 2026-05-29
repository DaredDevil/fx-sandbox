using FxSandbox.Domain;
using FxSandbox.Features.Orders;

namespace FxSandbox.Services;

public sealed class TradingEngine
{
    private readonly object _lock = new();

    private readonly Dictionary<string, decimal> _rates = new()
    {
        ["USD/EUR"] = 0.9185m,
        ["USD/GBP"] = 0.7890m,
        ["USD/CHF"] = 0.8990m,
    };

    private readonly List<LimitOrder> _orders = [];
    private readonly Dictionary<string, Position> _positions = [];
    private decimal _balance = 10_000m;

    public static readonly IReadOnlyList<string> SupportedPairs =
        ["USD/EUR", "USD/GBP", "USD/CHF"];

    // ── Rates ──────────────────────────────────────────────────────────────

    public void UpdateRate(string pair, decimal newRate)
    {
        lock (_lock) { _rates[pair] = newRate; }
    }

    public decimal GetRate(string pair)
    {
        lock (_lock) { return _rates[pair]; }
    }

    public IReadOnlyDictionary<string, decimal> GetRates()
    {
        lock (_lock) { return new Dictionary<string, decimal>(_rates); }
    }

    // ── Orders ─────────────────────────────────────────────────────────────

    public LimitOrder PlaceOrder(PlaceOrderRequest req)
    {
        var order = new LimitOrder
        {
            Pair = req.Pair,
            Side = req.Side,
            LimitPrice = req.LimitPrice,
            Quantity = req.Quantity,
        };
        lock (_lock) { _orders.Add(order); }
        return order;
    }

    public IReadOnlyList<LimitOrder> GetOrders()
    {
        lock (_lock) { return [.._orders]; }
    }

    // ── Order matching ─────────────────────────────────────────────────────

    public bool TryFillOrder(LimitOrder order, decimal fillRate)
    {
        lock (_lock)
        {
            if (order.Status != OrderStatus.Pending) return false;

            order.Status = OrderStatus.Filled;
            order.FilledAt = DateTime.UtcNow;

            var key = $"{order.Pair}:{order.Side}";

            if (_positions.TryGetValue(key, out var existing))
            {
                var totalQty = existing.Quantity + order.Quantity;
                existing.AverageEntryPrice =
                    (existing.AverageEntryPrice * existing.Quantity + fillRate * order.Quantity) / totalQty;
                existing.Quantity = totalQty;
            }
            else
            {
                _positions[key] = new Position
                {
                    Pair = order.Pair,
                    Side = order.Side,
                    Quantity = order.Quantity,
                    AverageEntryPrice = fillRate,
                };
            }

            return true;
        }
    }

    // ── Positions ──────────────────────────────────────────────────────────

    public IReadOnlyList<PositionDto> GetPositions()
    {
        lock (_lock)
        {
            return _positions.Values.Select(p =>
            {
                var currentRate = _rates[p.Pair];
                var pnl = p.Side == OrderSide.Buy
                    ? (currentRate - p.AverageEntryPrice) * p.Quantity
                    : (p.AverageEntryPrice - currentRate) * p.Quantity;

                return new PositionDto(
                    p.Pair, p.Side.ToString(), p.Quantity,
                    p.AverageEntryPrice, currentRate, Math.Round(pnl, 4));
            }).ToList();
        }
    }

    // ── Account ────────────────────────────────────────────────────────────

    public decimal GetBalance() { lock (_lock) { return _balance; } }
}

public sealed record PositionDto(
    string Pair, string Side, decimal Quantity,
    decimal AverageEntryPrice, decimal CurrentRate, decimal UnrealisedPnl);
