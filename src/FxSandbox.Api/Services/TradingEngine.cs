using FxSandbox.Domain;
using FxSandbox.Features.Orders;

namespace FxSandbox.Services;

// ── Interface (Dependency Inversion / Open-Closed) ─────────────────────────
// Consumers depend on the abstraction; the concrete class can be swapped or
// mocked without touching callers. Adding a new engine variant requires only
// a new implementation, not edits to existing code.

public interface ITradingEngine
{
    void UpdateRate(string pair, decimal newRate);
    decimal GetRate(string pair);
    IReadOnlyDictionary<string, decimal> GetRates();
    PlaceOrderResult PlaceOrder(PlaceOrderRequest req);
    IReadOnlyList<LimitOrder> GetOrders();
    bool CancelOrder(Guid id);
    bool TryFillOrder(LimitOrder order, decimal fillRate);
    IReadOnlyList<PositionDto> GetPositions();
    decimal GetBalance();
}

// ── Implementation ──────────────────────────────────────────────────────────
// Thread-safety strategy: ReaderWriterLockSlim
//
//   Read lock  → multiple threads may hold it concurrently (all GET paths).
//   Write lock → one thread at a time, exclusive (mutations + check-and-mutate).
//
// Why not a plain lock()?  A mutex serialises every GET request behind every
// other request.  With 100 k concurrent users, reads dominate — RWLockSlim
// lets them proceed in parallel.  Writes (order placement, fill, cancel) are
// infrequent by comparison and still fully serialised, so the balance /
// reservation invariants that prevent overdraft remain atomic.

public sealed class TradingEngine : ITradingEngine, IDisposable
{
    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);

    private readonly Dictionary<string, decimal> _rates = new()
    {
        ["USD/EUR"] = 0.9185m,
        ["USD/GBP"] = 0.7890m,
        ["USD/CHF"] = 0.8990m,
    };

    private readonly List<LimitOrder> _orders = [];
    private readonly Dictionary<string, Position> _positions = [];
    private readonly Dictionary<string, decimal> _pendingSellQty = [];
    private decimal _balance = 10_000m;

    public static readonly IReadOnlyList<string> SupportedPairs =
        ["USD/EUR", "USD/GBP", "USD/CHF"];

    // ── Rates ──────────────────────────────────────────────────────────────

    public void UpdateRate(string pair, decimal newRate)
    {
        _rwLock.EnterWriteLock();
        try { _rates[pair] = newRate; }
        finally { _rwLock.ExitWriteLock(); }
    }

    public decimal GetRate(string pair)
    {
        _rwLock.EnterReadLock();
        try { return _rates[pair]; }
        finally { _rwLock.ExitReadLock(); }
    }

    public IReadOnlyDictionary<string, decimal> GetRates()
    {
        _rwLock.EnterReadLock();
        try { return new Dictionary<string, decimal>(_rates); }
        finally { _rwLock.ExitReadLock(); }
    }

    // ── Orders ─────────────────────────────────────────────────────────────

    public PlaceOrderResult PlaceOrder(PlaceOrderRequest req)
    {
        _rwLock.EnterWriteLock();
        try
        {
            // Check-and-reserve are atomic within the write lock: concurrent
            // callers cannot race past zero on either constraint.
            if (req.Side == OrderSide.Buy)
            {
                if (_balance < req.Quantity)
                    return PlaceOrderResult.Fail("Insufficient balance");

                _balance -= req.Quantity;
            }
            else
            {
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
        finally { _rwLock.ExitWriteLock(); }
    }

    public IReadOnlyList<LimitOrder> GetOrders()
    {
        _rwLock.EnterReadLock();
        try { return [.. _orders]; }
        finally { _rwLock.ExitReadLock(); }
    }

    public bool CancelOrder(Guid id)
    {
        _rwLock.EnterWriteLock();
        try
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
        finally { _rwLock.ExitWriteLock(); }
    }

    // ── Order matching ─────────────────────────────────────────────────────

    public bool TryFillOrder(LimitOrder order, decimal fillRate)
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (order.Status != OrderStatus.Pending) return false;

            order.Status = OrderStatus.Filled;
            order.FilledAt = DateTime.UtcNow;

            if (order.Side == OrderSide.Sell)
            {
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
        finally { _rwLock.ExitWriteLock(); }
    }

    // ── Positions ──────────────────────────────────────────────────────────

    public IReadOnlyList<PositionDto> GetPositions()
    {
        _rwLock.EnterReadLock();
        try
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
        finally { _rwLock.ExitReadLock(); }
    }

    // ── Account ────────────────────────────────────────────────────────────

    public decimal GetBalance()
    {
        _rwLock.EnterReadLock();
        try { return _balance; }
        finally { _rwLock.ExitReadLock(); }
    }

    public void Dispose() => _rwLock.Dispose();
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
