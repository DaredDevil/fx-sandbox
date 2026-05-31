using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FxSandbox.FunctionalTests;

/// <summary>
/// End-to-end trader scenarios exercised through the live HTTP stack.
/// Each test represents a complete user journey rather than an isolated endpoint check.
/// xUnit creates one instance per [Fact], so each test gets its own factory and a
/// fresh TradingEngine — tests cannot bleed state into each other.
/// </summary>
public sealed class TradingScenarioTests : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public TradingScenarioTests() => _client = _factory.CreateClient();

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ── Scenario: place a limit order and verify it appears in the book ───────

    [Fact]
    public async Task Scenario_PlaceBuyOrder_AppearsAsPendingInOrderBook()
    {
        var payload = new { pair = "USD/EUR", side = "Buy", limitPrice = 0.9000m, quantity = 1000m };

        var postResp = await _client.PostAsJsonAsync("/api/orders", payload);
        postResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var ordersResp = await _client.GetAsync("/api/orders");
        var body = await ordersResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var orders = doc.RootElement.EnumerateArray().ToList();
        orders.Should().Contain(o =>
            o.GetProperty("pair").GetString() == "USD/EUR" &&
            o.GetProperty("side").GetString() == "Buy" &&
            o.GetProperty("status").GetString() == "Pending");
    }

    // ── Scenario: cancel a pending order ─────────────────────────────────────

    [Fact]
    public async Task Scenario_CancelPendingOrder_ShowsAsCancelledInBook()
    {
        var payload = new { pair = "USD/GBP", side = "Sell", limitPrice = 0.8500m, quantity = 250m };
        var postResp = await _client.PostAsJsonAsync("/api/orders", payload);
        var created = await postResp.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(created);
        var id = createdDoc.RootElement.GetProperty("id").GetString();

        var deleteResp = await _client.DeleteAsync($"/api/orders/{id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ordersResp = await _client.GetAsync("/api/orders");
        var body = await ordersResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var status = doc.RootElement.EnumerateArray()
            .First(o => o.GetProperty("id").GetString() == id)
            .GetProperty("status").GetString();

        status.Should().Be("Cancelled");
    }

    // ── Scenario: cannot cancel an already-filled order ───────────────────────

    [Fact]
    public async Task Scenario_CancelFilledOrder_Returns404()
    {
        var ratesResp = await _client.GetAsync("/api/rates");
        var ratesBody = await ratesResp.Content.ReadAsStringAsync();
        using var ratesDoc = JsonDocument.Parse(ratesBody);
        var marketRate = ratesDoc.RootElement.EnumerateArray()
            .First(r => r.GetProperty("pair").GetString() == "USD/CHF")
            .GetProperty("value").GetDecimal();

        // Place buy 10% above market — fills on the first matching tick
        // regardless of rate drift (±0.5% per tick from the simulator).
        var limitPrice = Math.Round(marketRate * 1.1m, 6);
        var payload = new { pair = "USD/CHF", side = "Buy", limitPrice, quantity = 100m };
        var postResp = await _client.PostAsJsonAsync("/api/orders", payload);
        var created = await postResp.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(created);
        var id = createdDoc.RootElement.GetProperty("id").GetString();

        // Poll until Filled rather than relying on a fixed delay.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        string status;
        do
        {
            await Task.Delay(100);
            var ordersResp = await _client.GetAsync("/api/orders");
            using var ordersDoc = JsonDocument.Parse(await ordersResp.Content.ReadAsStringAsync());
            status = ordersDoc.RootElement.EnumerateArray()
                .First(o => o.GetProperty("id").GetString() == id)
                .GetProperty("status").GetString()!;
        } while (status != "Filled" && DateTime.UtcNow < deadline);

        status.Should().Be("Filled", "order should have been matched before attempting cancel");

        var deleteResp = await _client.DeleteAsync($"/api/orders/{id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Scenario: account balance is present ──────────────────────────────────

    [Fact]
    public async Task Scenario_NewAccount_HasTenThousandUsdBalance()
    {
        var resp = await _client.GetAsync("/api/account");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("balance").GetDecimal().Should().Be(10_000m);
        doc.RootElement.GetProperty("currency").GetString().Should().Be("USD");
    }

    // ── Scenario: all three pairs are always quoted ───────────────────────────

    [Fact]
    public async Task Scenario_LiveRates_AlwaysQuoteAllThreePairs()
    {
        var resp = await _client.GetAsync("/api/rates");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var pairs = doc.RootElement.EnumerateArray()
            .Select(r => r.GetProperty("pair").GetString())
            .ToList();

        pairs.Should().BeEquivalentTo(["USD/EUR", "USD/GBP", "USD/CHF"]);
    }

    // ── Scenario: rejected order with bad pair returns validation error ────────

    [Fact]
    public async Task Scenario_InvalidOrder_ReturnsValidationProblemDetails()
    {
        var payload = new { pair = "USD/JPY", side = "Buy", limitPrice = 110m, quantity = 1000m };
        var resp = await _client.PostAsJsonAsync("/api/orders", payload);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("errors", out _).Should().BeTrue();
    }
}
