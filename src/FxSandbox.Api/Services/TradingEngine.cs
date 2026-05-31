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

    // Tracks how many units of each pair are already reserved by pending SELL orders,
    // preventing a user from committing the same holdings to multiple sells.
    private readonly Dictionary<string, decimal> _pendingSellQty = [];

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

    public PlaceOrderResult PlaceOrder(PlaceOrderRequest req)
    {
        lock (_lock)
        {
            // All checks and mutations are inside the same lock acquisition so
            // concurrent callers cannot race past zero on either constraint.

            if (req.Side == OrderSide.Buy)
            {
                if (_balance < req.Quantity)
                    return PlaceOrderResult.Fail("Insufficient balance");

                _balance -= req.Quantity;
            }
            else
            {
                // SELL: you can only sell what you currently hold minus any quantity
                // already reserved by other pending sell orders for the same pair.
                var buyQty = _positions.TryGetValue($"{req.Pair}:Buy", out var pos) ? pos.Quantity : 0m;
                var reserved = _pendingSellQty.GetValueOrDefault(req.Pair);
                var available = buyQty - reserved;

                if (available < req.Quantity)
                    return PlaceOrderResult.Fail("Insufficient position to sell");

                _pendingSellQty[req.Pair] = reserved + req.Quantity;
            }

            var order = new LimitOrder
            {
                Pair = req.Pair,
                Side = req.Side,
                LimitPrice = req.LimitPrice,
                Quantity = req.Quantity,
            };

            _orders.Add(order);
            return PlaceOrderResult.Success(order);
        }
    }

    public IReadOnlyList<LimitOrder> GetOrders()
    {
        lock (_lock) { return [.. _orders]; }
    }

    public bool CancelOrder(Guid id)
    {
        lock (_lock)
        {
            var order = _orders.FirstOrDefault(o => o.Id == id);
            if (order is null || order.Status != OrderStatus.Pending) return false;

            order.Status = OrderStatus.Cancelled;

            if (order.Side == OrderSide.Buy)
                _balance += order.Quantity;
            else
                _pendingSellQty[order.Pair] = Math.Max(0m,
                    _pendingSellQty.GetValueOrDefault(order.Pair) - order.Quantity);

            return true;
        }
    }

    // ── Order matching ─────────────────────────────────────────────────────

    public bool TryFillOrder(LimitOrder order, decimal fillRate)
    {
        lock (_lock)
        {
            if (order.Status != OrderStatus.Pending) return false;

            order.Status = OrderStatus.Filled;
            order.FilledAt = DateTime.UtcNow;

            if (order.Side == OrderSide.Sell)
            {
                // Release the sell reservation and credit USD proceeds.
                _pendingSellQty[order.Pair] = Math.Max(0m,
                    _pendingSellQty.GetValueOrDefault(order.Pair) - order.Quantity);
                _balance += order.Quantity;
            }
            // BUY: funds were reserved at placement — no further balance change.

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

public sealed record PlaceOrderResult
{
    public LimitOrder? Order { get; init; }
    public string? Error { get; init; }
    public bool IsSuccess => Order is not null;

    public static PlaceOrderResult Success(LimitOrder order) => new() { Order = order };
    public static PlaceOrderResult Fail(string error) => new() { Error = error };
}

public sealed record PositionDto(
    string Pair, string Side, decimal Quantity,
    decimal AverageEntryPrice, decimal CurrentRate, decimal UnrealisedPnl);
