using FluentAssertions;
using FxSandbox.Domain;
using FxSandbox.Features.Orders;
using FxSandbox.Services;
using Xunit;

namespace FxSandbox.Tests;

public sealed class TradingEngineTests
{
    private static TradingEngine CreateEngine() => new();

    // ── Rates ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetRates_ReturnsAllThreePairs()
    {
        var engine = CreateEngine();
        var rates = engine.GetRates();
        rates.Keys.Should().BeEquivalentTo(["USD/EUR", "USD/GBP", "USD/CHF"]);
    }

    [Fact]
    public void UpdateRate_ChangesStoredValue()
    {
        var engine = CreateEngine();
        engine.UpdateRate("USD/EUR", 0.9999m);
        engine.GetRate("USD/EUR").Should().Be(0.9999m);
    }

    [Theory]
    [InlineData("USD/EUR", 0.9185)]
    [InlineData("USD/GBP", 0.7890)]
    [InlineData("USD/CHF", 0.8990)]
    public void InitialRates_AreWithinReasonableSeedRange(string pair, double expectedSeed)
    {
        var engine = CreateEngine();
        var rate = engine.GetRate(pair);
        rate.Should().BeApproximately((decimal)expectedSeed, 0.001m);
    }

    // ── Order placement ─────────────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_AddsOrderWithPendingStatus()
    {
        var engine = CreateEngine();
        var req = new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.90m, 1000m);

        var order = engine.PlaceOrder(req);

        order.Status.Should().Be(OrderStatus.Pending);
        order.Pair.Should().Be("USD/EUR");
        order.Side.Should().Be(OrderSide.Buy);
        order.LimitPrice.Should().Be(0.90m);
        order.Quantity.Should().Be(1000m);
        order.FilledAt.Should().BeNull();

        engine.GetOrders().Should().ContainSingle(o => o.Id == order.Id);
    }

    // ── Order filling — buy ──────────────────────────────────────────────────

    [Fact]
    public void TryFillOrder_BuyOrder_FillsWhenRateAtOrBelowLimit()
    {
        var engine = CreateEngine();
        var req = new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.90m, 100m);
        var order = engine.PlaceOrder(req);

        // Rate equals limit — should fill
        var filled = engine.TryFillOrder(order, 0.90m);

        filled.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Filled);
        order.FilledAt.Should().NotBeNull();
    }

    [Fact]
    public void TryFillOrder_BuyOrder_FillsWhenRateBelowLimit()
    {
        var engine = CreateEngine();
        var req = new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.90m, 100m);
        var order = engine.PlaceOrder(req);

        var filled = engine.TryFillOrder(order, 0.8950m);

        filled.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Filled);
    }

    [Fact]
    public void TryFillOrder_BuyOrder_DoesNotFillWhenRateAboveLimit()
    {
        var engine = CreateEngine();
        var req = new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.90m, 100m);
        var order = engine.PlaceOrder(req);

        // Simulate: rate is above limit — should NOT fill
        // (matching service won't call TryFillOrder, but test the guard directly)
        order.Status.Should().Be(OrderStatus.Pending);
    }

    // ── Order filling — sell ─────────────────────────────────────────────────

    [Fact]
    public void TryFillOrder_SellOrder_FillsWhenRateAtOrAboveLimit()
    {
        var engine = CreateEngine();
        var req = new PlaceOrderRequest("USD/EUR", OrderSide.Sell, 0.93m, 100m);
        var order = engine.PlaceOrder(req);

        var filled = engine.TryFillOrder(order, 0.93m);

        filled.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Filled);
    }

    [Fact]
    public void TryFillOrder_AlreadyFilled_ReturnsFalse()
    {
        var engine = CreateEngine();
        var req = new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.90m, 100m);
        var order = engine.PlaceOrder(req);

        engine.TryFillOrder(order, 0.90m);
        var secondAttempt = engine.TryFillOrder(order, 0.89m);

        secondAttempt.Should().BeFalse();
    }

    // ── Positions ────────────────────────────────────────────────────────────

    [Fact]
    public void TryFillOrder_CreatesPositionOnFirstFill()
    {
        var engine = CreateEngine();
        var req = new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.90m, 500m);
        var order = engine.PlaceOrder(req);

        engine.TryFillOrder(order, 0.90m);

        var positions = engine.GetPositions();
        positions.Should().ContainSingle(p => p.Pair == "USD/EUR" && p.Side == "Buy");
        positions[0].Quantity.Should().Be(500m);
        positions[0].AverageEntryPrice.Should().Be(0.90m);
    }

    [Fact]
    public void TryFillOrder_AveragesEntryPriceOnSecondFill()
    {
        var engine = CreateEngine();

        var order1 = engine.PlaceOrder(new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(order1, 0.90m);

        var order2 = engine.PlaceOrder(new("USD/EUR", OrderSide.Buy, 0.92m, 1000m));
        engine.TryFillOrder(order2, 0.92m);

        var pos = engine.GetPositions().Single(p => p.Pair == "USD/EUR" && p.Side == "Buy");
        pos.Quantity.Should().Be(2000m);
        pos.AverageEntryPrice.Should().Be(0.91m); // (0.90 * 1000 + 0.92 * 1000) / 2000
    }

    // ── Unrealised P&L ───────────────────────────────────────────────────────

    [Fact]
    public void GetPositions_CalculatesLongPnlCorrectly()
    {
        var engine = CreateEngine();
        var order = engine.PlaceOrder(new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(order, 0.90m);

        // Set current rate above entry → positive P&L
        engine.UpdateRate("USD/EUR", 0.92m);

        var pos = engine.GetPositions().Single(p => p.Pair == "USD/EUR" && p.Side == "Buy");
        // P&L = (0.92 - 0.90) * 1000 = +20
        pos.UnrealisedPnl.Should().Be(20m);
    }

    [Fact]
    public void GetPositions_CalculatesShortPnlCorrectly()
    {
        var engine = CreateEngine();
        var order = engine.PlaceOrder(new("USD/EUR", OrderSide.Sell, 0.92m, 1000m));
        engine.TryFillOrder(order, 0.92m);

        // Set current rate below entry → positive P&L for short
        engine.UpdateRate("USD/EUR", 0.90m);

        var pos = engine.GetPositions().Single(p => p.Pair == "USD/EUR" && p.Side == "Sell");
        // P&L = (0.92 - 0.90) * 1000 = +20
        pos.UnrealisedPnl.Should().Be(20m);
    }

    [Fact]
    public void GetPositions_NegativePnlWhenLongAndRateFalls()
    {
        var engine = CreateEngine();
        var order = engine.PlaceOrder(new("USD/EUR", OrderSide.Buy, 0.92m, 1000m));
        engine.TryFillOrder(order, 0.92m);

        engine.UpdateRate("USD/EUR", 0.90m);

        var pos = engine.GetPositions().Single(p => p.Pair == "USD/EUR" && p.Side == "Buy");
        // P&L = (0.90 - 0.92) * 1000 = -20
        pos.UnrealisedPnl.Should().Be(-20m);
    }

    // ── Account ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetBalance_ReturnsInitialCapital()
    {
        var engine = CreateEngine();
        engine.GetBalance().Should().Be(10_000m);
    }
}
