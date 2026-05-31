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
/// </summary>
public sealed class TradingScenarioTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

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
        // Place at exactly the seed rate — the matching service will fill it on next tick
        var ratesResp = await _client.GetAsync("/api/rates");
        var ratesBody = await ratesResp.Content.ReadAsStringAsync();
        using var ratesDoc = JsonDocument.Parse(ratesBody);
        var marketRate = ratesDoc.RootElement.EnumerateArray()
            .First(r => r.GetProperty("pair").GetString() == "USD/CHF")
            .GetProperty("value").GetDecimal();

        // Place buy at market rate so it fills immediately
        var payload = new { pair = "USD/CHF", side = "Buy", limitPrice = marketRate, quantity = 100m };
        var postResp = await _client.PostAsJsonAsync("/api/orders", payload);
        var created = await postResp.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(created);
        var id = createdDoc.RootElement.GetProperty("id").GetString();

        // Give the matching service one tick to fill it
        await Task.Delay(700);

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
