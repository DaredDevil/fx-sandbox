using FxSandbox.Domain;
using FxSandbox.Features.Orders;
using FxSandbox.Services.Locking;

namespace FxSandbox.Services;

// ── Interface (Dependency Inversion / Open-Closed) ─────────────────────────
// Consumers depend on the abstraction. Swapping the engine (e.g. Redis-backed
// for multi-pod) requires only a new implementation registered in DI.

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
// Thread-safety strategy: ILockProvider (defaults to ReaderWriterLockSlim).
//
//   Read lock  → concurrent; all GET paths acquire this.
//   Write lock → exclusive; all mutations + check-and-mutate acquire this.
//
// Switching to a Redis-backed lock for multi-pod requires only registering a
// different ILockProvider in DI — no changes here.

public sealed class TradingEngine(ILockProvider lockProvider, ILogger<TradingEngine> logger)
    : ITradingEngine, IDisposable
{
    private readonly ILockProvider _lock = lockProvider;
    private readonly ILogger<TradingEngine> _logger = logger;

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
        _lock.EnterWriteLock();
        try { _rates[pair] = newRate; }
        finally { _lock.ExitWriteLock(); }
    }

    public decimal GetRate(string pair)
    {
        _lock.EnterReadLock();
        try { return _rates[pair]; }
        finally { _lock.ExitReadLock(); }
    }

    public IReadOnlyDictionary<string, decimal> GetRates()
    {
        _lock.EnterReadLock();
        try { return new Dictionary<string, decimal>(_rates); }
        finally { _lock.ExitReadLock(); }
    }

    // ── Orders ─────────────────────────────────────────────────────────────

    public PlaceOrderResult PlaceOrder(PlaceOrderRequest req)
    {
        _lock.EnterWriteLock();
        try
        {
            // The entire check-and-reserve is atomic inside the write lock.
            // No two threads can race past either constraint.
            if (req.Side == OrderSide.Buy)
            {
                if (_balance < req.Quantity)
                {
                    _logger.LogWarning(
                        "Order rejected — insufficient balance. Required: {Required}, Available: {Available}",
                        req.Quantity, _balance);
                    return PlaceOrderResult.Fail("Insufficient balance");
                }
                _balance -= req.Quantity;
            }
            else
            {
                var buyQty = _positions.TryGetValue($"{req.Pair}:Buy", out var pos) ? pos.Quantity : 0m;
                var reserved = _pendingSellQty.GetValueOrDefault(req.Pair);
                var available = buyQty - reserved;

                if (available < req.Quantity)
                {
                    _logger.LogWarning(
                        "Order rejected — insufficient position. Pair: {Pair}, Required: {Required}, Available: {Available}",
                        req.Pair, req.Quantity, available);
                    return PlaceOrderResult.Fail("Insufficient position to sell");
                }
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

            _logger.LogInformation(
                "Order placed. Id: {Id}, Pair: {Pair}, Side: {Side}, LimitPrice: {Price}, Quantity: {Qty}",
                order.Id, order.Pair, order.Side, order.LimitPrice, order.Quantity);

            return PlaceOrderResult.Success(order);
        }
        finally { _lock.ExitWriteLock(); }
    }

    public IReadOnlyList<LimitOrder> GetOrders()
    {
        _lock.EnterReadLock();
        try { return [.. _orders]; }
        finally { _lock.ExitReadLock(); }
    }

    public bool CancelOrder(Guid id)
    {
        _lock.EnterWriteLock();
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

            _logger.LogInformation("Order cancelled. Id: {Id}", id);
            return true;
        }
        finally { _lock.ExitWriteLock(); }
    }

    // ── Order matching ─────────────────────────────────────────────────────

    public bool TryFillOrder(LimitOrder order, decimal fillRate)
    {
        _lock.EnterWriteLock();
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
            // BUY: funds reserved at placement — no further balance change.

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

            _logger.LogInformation(
                "Order filled. Id: {Id}, Pair: {Pair}, Side: {Side}, FillRate: {Rate}",
                order.Id, order.Pair, order.Side, fillRate);

            return true;
        }
        finally { _lock.ExitWriteLock(); }
    }

    // ── Positions ──────────────────────────────────────────────────────────

    public IReadOnlyList<PositionDto> GetPositions()
    {
        _lock.EnterReadLock();
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
        finally { _lock.ExitReadLock(); }
    }

    // ── Account ────────────────────────────────────────────────────────────

    public decimal GetBalance()
    {
        _lock.EnterReadLock();
        try { return _balance; }
        finally { _lock.ExitReadLock(); }
    }

    public void Dispose() => _lock.Dispose();
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
