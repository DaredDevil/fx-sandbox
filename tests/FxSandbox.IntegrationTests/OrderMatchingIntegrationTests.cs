using FluentAssertions;
using FxSandbox.Domain;
using FxSandbox.Features.Orders;
using FxSandbox.Services;
using Xunit;

namespace FxSandbox.IntegrationTests;

/// <summary>
/// Tests that wire TradingEngine + OrderMatchingService together without HTTP.
/// Verifies the matching loop correctly fills/ignores orders when rates cross the limit.
/// </summary>
public sealed class OrderMatchingIntegrationTests
{
    // The matching service ticks every 500 ms; allow two full ticks as buffer.
    private static readonly TimeSpan TickBuffer = TimeSpan.FromMilliseconds(1200);

    private static LimitOrder Place(TradingEngine engine, PlaceOrderRequest req)
        => engine.PlaceOrder(req).Order!;

    private static async Task<OrderMatchingService> StartMatchingAsync(TradingEngine engine, CancellationToken ct)
    {
        var svc = new OrderMatchingService(engine);
        await svc.StartAsync(ct);
        return svc;
    }

    [Fact]
    public async Task FillsBuyOrder_WhenRateIsAtOrBelowLimit()
    {
        var engine = new TradingEngine();
        engine.UpdateRate("USD/EUR", 0.9200m);
        var order = Place(engine, new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.9200m, 500m));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var svc = await StartMatchingAsync(engine, cts.Token);

        await Task.Delay(TickBuffer, CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Filled);
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FillsSellOrder_WhenRateIsAtOrAboveLimit()
    {
        var engine = new TradingEngine();
        engine.UpdateRate("USD/GBP", 0.7890m);

        // Must have a BUY position before placing a SELL
        var buyOrder = Place(engine, new PlaceOrderRequest("USD/GBP", OrderSide.Buy, 0.7890m, 300m));
        engine.TryFillOrder(buyOrder, 0.7890m);

        var sellOrder = Place(engine, new PlaceOrderRequest("USD/GBP", OrderSide.Sell, 0.7890m, 300m));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var svc = await StartMatchingAsync(engine, cts.Token);

        await Task.Delay(TickBuffer, CancellationToken.None);

        sellOrder.Status.Should().Be(OrderStatus.Filled);
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SkipsCancelledOrder_EvenWhenRateCrossesLimit()
    {
        var engine = new TradingEngine();
        engine.UpdateRate("USD/CHF", 0.8990m);
        var order = Place(engine, new PlaceOrderRequest("USD/CHF", OrderSide.Buy, 0.8990m, 100m));
        engine.CancelOrder(order.Id);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var svc = await StartMatchingAsync(engine, cts.Token);

        await Task.Delay(TickBuffer, CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Cancelled);
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DoesNotFillBuyOrder_WhenRateIsAboveLimit()
    {
        var engine = new TradingEngine();
        engine.UpdateRate("USD/EUR", 0.9500m);
        var order = Place(engine, new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.9000m, 100m));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var svc = await StartMatchingAsync(engine, cts.Token);

        await Task.Delay(TickBuffer, CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Pending);
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void FilledOrder_CreatesPosition_WithCorrectAverageEntry()
    {
        var engine = new TradingEngine();

        var o1 = Place(engine, new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.9100m, 1000m));
        engine.TryFillOrder(o1, 0.9100m);

        var o2 = Place(engine, new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.9300m, 1000m));
        engine.TryFillOrder(o2, 0.9300m);

        var position = engine.GetPositions().Single(p => p.Pair == "USD/EUR" && p.Side == "Buy");
        position.Quantity.Should().Be(2000m);
        position.AverageEntryPrice.Should().Be(0.9200m);
    }
}
