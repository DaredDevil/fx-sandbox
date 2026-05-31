using FluentAssertions;
using FxSandbox.Domain;
using FxSandbox.Features.Orders;
using FxSandbox.Services;
using Xunit;

namespace FxSandbox.UnitTests;

public sealed class TradingEngineTests
{
    private static TradingEngine CreateEngine() => new();

    // Helper: place an order and assert it succeeded (for tests focused elsewhere).
    private static LimitOrder Place(TradingEngine engine, PlaceOrderRequest req)
    {
        var result = engine.PlaceOrder(req);
        result.IsSuccess.Should().BeTrue("test setup expects a successful placement");
        return result.Order!;
    }

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

        var order = Place(engine, req);

        order.Status.Should().Be(OrderStatus.Pending);
        order.Pair.Should().Be("USD/EUR");
        order.Side.Should().Be(OrderSide.Buy);
        order.LimitPrice.Should().Be(0.90m);
        order.Quantity.Should().Be(1000m);
        order.FilledAt.Should().BeNull();

        engine.GetOrders().Should().ContainSingle(o => o.Id == order.Id);
    }

    [Fact]
    public void PlaceOrder_BuyOrder_ReservesBalanceImmediately()
    {
        var engine = CreateEngine();
        Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 3000m));
        engine.GetBalance().Should().Be(7_000m);
    }

    [Fact]
    public void PlaceOrder_SellOrder_DoesNotChangeBalance()
    {
        var engine = CreateEngine();
        var buy = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 5000m));
        engine.TryFillOrder(buy, 0.90m);

        Place(engine, new("USD/EUR", OrderSide.Sell, 0.95m, 3000m));

        // Only the BUY reservation should have reduced balance; SELL placement does not.
        engine.GetBalance().Should().Be(5_000m);
    }

    [Fact]
    public void PlaceOrder_InsufficientBalance_ReturnsFailure()
    {
        var engine = CreateEngine();
        var result = engine.PlaceOrder(new("USD/EUR", OrderSide.Buy, 0.90m, 15_000m));
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.Order.Should().BeNull();
    }

    [Fact]
    public void PlaceOrder_ExactBalance_Succeeds()
    {
        var engine = CreateEngine();
        var result = engine.PlaceOrder(new("USD/EUR", OrderSide.Buy, 0.90m, 10_000m));
        result.IsSuccess.Should().BeTrue();
        engine.GetBalance().Should().Be(0m);
    }

    [Fact]
    public void PlaceOrder_ZeroBalance_RejectsNewBuyOrder()
    {
        var engine = CreateEngine();
        Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 10_000m)); // drain balance
        var result = engine.PlaceOrder(new("USD/EUR", OrderSide.Buy, 0.90m, 1m));
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void PlaceOrder_SellOrder_NoPosition_ReturnsFailure()
    {
        var engine = CreateEngine();
        var result = engine.PlaceOrder(new("USD/EUR", OrderSide.Sell, 0.95m, 500m));
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PlaceOrder_SellOrder_ExceedsPosition_ReturnsFailure()
    {
        var engine = CreateEngine();
        var buyOrder = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(buyOrder, 0.90m);

        var result = engine.PlaceOrder(new("USD/EUR", OrderSide.Sell, 0.95m, 1001m));
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void PlaceOrder_SellOrder_ExactPosition_Succeeds()
    {
        var engine = CreateEngine();
        var buyOrder = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(buyOrder, 0.90m);

        var result = engine.PlaceOrder(new("USD/EUR", OrderSide.Sell, 0.95m, 1000m));
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void PlaceOrder_MultipleSellOrders_CannotDoubleSpendPosition()
    {
        var engine = CreateEngine();
        var buyOrder = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(buyOrder, 0.90m);

        Place(engine, new("USD/EUR", OrderSide.Sell, 0.95m, 600m)); // 600 reserved
        var second = engine.PlaceOrder(new("USD/EUR", OrderSide.Sell, 0.95m, 500m)); // only 400 left
        second.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void CancelOrder_SellOrder_ReleasesReservation()
    {
        var engine = CreateEngine();
        var buyOrder = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(buyOrder, 0.90m);

        var sellOrder = Place(engine, new("USD/EUR", OrderSide.Sell, 0.95m, 1000m));
        engine.CancelOrder(sellOrder.Id);

        // After cancel, the full position should be available again
        var second = engine.PlaceOrder(new("USD/EUR", OrderSide.Sell, 0.95m, 1000m));
        second.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PlaceOrder_ConcurrentSellOrders_NeverExceedPosition()
    {
        var engine = CreateEngine();
        var buyOrder = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(buyOrder, 0.90m); // position = 1000

        // 20 concurrent sell attempts of 100 each — only 10 can succeed (1000 / 100)
        var results = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => engine.PlaceOrder(new("USD/EUR", OrderSide.Sell, 0.95m, 100m)))));

        results.Count(r => r.IsSuccess).Should().Be(10);
        results.Count(r => !r.IsSuccess).Should().Be(10);
    }

    [Fact]
    public async Task PlaceOrder_ConcurrentBuyOrders_BalanceNeverGoesNegative()
    {
        var engine = CreateEngine(); // $10,000
        // 200 concurrent attempts each for $100 — only 100 can succeed
        var results = await Task.WhenAll(
            Enumerable.Range(0, 200)
                .Select(_ => Task.Run(() => engine.PlaceOrder(new("USD/EUR", OrderSide.Buy, 0.90m, 100m)))));

        engine.GetBalance().Should().BeGreaterThanOrEqualTo(0m);
        results.Count(r => r.IsSuccess).Should().Be(100);
        results.Count(r => !r.IsSuccess).Should().Be(100);
    }

    // ── Order filling — buy ──────────────────────────────────────────────────

    [Fact]
    public void TryFillOrder_BuyOrder_FillsWhenRateAtOrBelowLimit()
    {
        var engine = CreateEngine();
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 100m));

        var filled = engine.TryFillOrder(order, 0.90m);

        filled.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Filled);
        order.FilledAt.Should().NotBeNull();
    }

    [Fact]
    public void TryFillOrder_BuyOrder_FillsWhenRateBelowLimit()
    {
        var engine = CreateEngine();
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 100m));

        var filled = engine.TryFillOrder(order, 0.8950m);

        filled.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Filled);
    }

    [Fact]
    public void TryFillOrder_BuyOrder_DoesNotChangeBalanceOnFill()
    {
        var engine = CreateEngine();
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        var balanceAfterPlacement = engine.GetBalance(); // already reserved

        engine.TryFillOrder(order, 0.90m);

        engine.GetBalance().Should().Be(balanceAfterPlacement);
    }

    [Fact]
    public void TryFillOrder_BuyOrder_DoesNotFillWhenRateAboveLimit()
    {
        var engine = CreateEngine();
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 100m));

        order.Status.Should().Be(OrderStatus.Pending);
    }

    // ── Order filling — sell ─────────────────────────────────────────────────

    [Fact]
    public void TryFillOrder_SellOrder_FillsWhenRateAtOrAboveLimit()
    {
        var engine = CreateEngine();
        var buy = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 100m));
        engine.TryFillOrder(buy, 0.90m);
        var order = Place(engine, new("USD/EUR", OrderSide.Sell, 0.93m, 100m));

        var filled = engine.TryFillOrder(order, 0.93m);

        filled.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Filled);
    }

    [Fact]
    public void TryFillOrder_SellOrder_AddsQuantityToBalance()
    {
        var engine = CreateEngine();
        var buy = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 500m));
        engine.TryFillOrder(buy, 0.90m);
        var order = Place(engine, new("USD/EUR", OrderSide.Sell, 0.92m, 500m));

        engine.TryFillOrder(order, 0.92m);

        // Started $10,000. BUY reserved $500 → $9,500. SELL fill adds $500 → $10,000.
        engine.GetBalance().Should().Be(10_000m);
    }

    [Fact]
    public void TryFillOrder_AlreadyFilled_ReturnsFalse()
    {
        var engine = CreateEngine();
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 100m));

        engine.TryFillOrder(order, 0.90m);
        var secondAttempt = engine.TryFillOrder(order, 0.89m);

        secondAttempt.Should().BeFalse();
    }

    [Fact]
    public void TryFillOrder_FailedFill_DoesNotChangeBalance()
    {
        var engine = CreateEngine();
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(order, 0.90m);

        var balanceAfterFirstFill = engine.GetBalance();
        engine.TryFillOrder(order, 0.89m); // already filled — should be no-op

        engine.GetBalance().Should().Be(balanceAfterFirstFill);
    }

    // ── Cancel order ─────────────────────────────────────────────────────────

    [Fact]
    public void CancelOrder_PendingOrder_SetsCancelledStatus()
    {
        var engine = CreateEngine();
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 100m));

        var result = engine.CancelOrder(order.Id);

        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void CancelOrder_BuyOrder_RefundsReservationToBalance()
    {
        var engine = CreateEngine();
        Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 4000m));
        var order2 = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 3000m));
        engine.GetBalance().Should().Be(3_000m);

        engine.CancelOrder(order2.Id);

        engine.GetBalance().Should().Be(6_000m);
    }

    [Fact]
    public void CancelOrder_SellOrder_DoesNotChangeBalance()
    {
        var engine = CreateEngine();
        var buy = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 5000m));
        engine.TryFillOrder(buy, 0.90m);
        var order = Place(engine, new("USD/EUR", OrderSide.Sell, 0.95m, 3000m));

        engine.CancelOrder(order.Id);

        // BUY deducted $5,000 at placement; SELL cancel should not affect balance.
        engine.GetBalance().Should().Be(5_000m);
    }

    [Fact]
    public void CancelOrder_UnknownId_ReturnsFalse()
    {
        var engine = CreateEngine();

        var result = engine.CancelOrder(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public void CancelOrder_FilledOrder_ReturnsFalse()
    {
        var engine = CreateEngine();
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 100m));
        engine.TryFillOrder(order, 0.90m);

        var result = engine.CancelOrder(order.Id);

        result.Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Filled);
    }

    // ── Positions ────────────────────────────────────────────────────────────

    [Fact]
    public void TryFillOrder_CreatesPositionOnFirstFill()
    {
        var engine = CreateEngine();
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 500m));

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

        var order1 = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(order1, 0.90m);

        var order2 = Place(engine, new("USD/EUR", OrderSide.Buy, 0.92m, 1000m));
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
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(order, 0.90m);

        engine.UpdateRate("USD/EUR", 0.92m);

        var pos = engine.GetPositions().Single(p => p.Pair == "USD/EUR" && p.Side == "Buy");
        // P&L = (0.92 - 0.90) * 1000 = +20
        pos.UnrealisedPnl.Should().Be(20m);
    }

    [Fact]
    public void GetPositions_CalculatesShortPnlCorrectly()
    {
        var engine = CreateEngine();
        var buy = Place(engine, new("USD/EUR", OrderSide.Buy, 0.90m, 1000m));
        engine.TryFillOrder(buy, 0.90m);
        var order = Place(engine, new("USD/EUR", OrderSide.Sell, 0.92m, 1000m));
        engine.TryFillOrder(order, 0.92m);

        engine.UpdateRate("USD/EUR", 0.90m);

        var pos = engine.GetPositions().Single(p => p.Pair == "USD/EUR" && p.Side == "Sell");
        // P&L = (0.92 - 0.90) * 1000 = +20
        pos.UnrealisedPnl.Should().Be(20m);
    }

    [Fact]
    public void GetPositions_NegativePnlWhenLongAndRateFalls()
    {
        var engine = CreateEngine();
        var order = Place(engine, new("USD/EUR", OrderSide.Buy, 0.92m, 1000m));
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
