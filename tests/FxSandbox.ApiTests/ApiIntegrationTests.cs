using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FxSandbox.ApiTests;

public sealed class ApiIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── GET /api/rates ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRates_Returns200WithThreePairs()
    {
        var response = await _client.GetAsync("/api/rates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task GetRates_ResponseContainsPairAndValue()
    {
        var response = await _client.GetAsync("/api/rates");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var pairs = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("pair").GetString())
            .ToList();

        pairs.Should().BeEquivalentTo(["USD/EUR", "USD/GBP", "USD/CHF"]);
    }

    // ── GET /api/account ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccount_Returns10000Balance()
    {
        var response = await _client.GetAsync("/api/account");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("balance").GetDecimal().Should().Be(10_000m);
        doc.RootElement.GetProperty("currency").GetString().Should().Be("USD");
    }

    // ── GET /api/orders ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrders_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ── POST /api/orders ──────────────────────────────────────────────────────

    [Fact]
    public async Task PostOrder_ValidRequest_Returns201WithOrder()
    {
        var payload = new { pair = "USD/EUR", side = "Buy", limitPrice = 0.90m, quantity = 500m };

        var response = await _client.PostAsJsonAsync("/api/orders", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("pair").GetString().Should().Be("USD/EUR");
        doc.RootElement.GetProperty("status").GetString().Should().Be("Pending");
        doc.RootElement.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostOrder_InvalidPair_Returns400()
    {
        var payload = new { pair = "USD/JPY", side = "Buy", limitPrice = 110m, quantity = 1000m };

        var response = await _client.PostAsJsonAsync("/api/orders", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostOrder_ZeroLimitPrice_Returns400()
    {
        var payload = new { pair = "USD/EUR", side = "Buy", limitPrice = 0m, quantity = 1000m };

        var response = await _client.PostAsJsonAsync("/api/orders", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostOrder_NegativeQuantity_Returns400()
    {
        var payload = new { pair = "USD/EUR", side = "Buy", limitPrice = 0.90m, quantity = -1m };

        var response = await _client.PostAsJsonAsync("/api/orders", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostOrder_AppearsInGetOrders()
    {
        var payload = new { pair = "USD/GBP", side = "Sell", limitPrice = 0.80m, quantity = 200m };

        await _client.PostAsJsonAsync("/api/orders", payload);
        var response = await _client.GetAsync("/api/orders");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var pairs = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("pair").GetString())
            .ToList();

        pairs.Should().Contain("USD/GBP");
    }

    // ── DELETE /api/orders/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteOrder_PendingOrder_Returns204()
    {
        var payload = new { pair = "USD/CHF", side = "Buy", limitPrice = 0.85m, quantity = 100m };
        var postResp = await _client.PostAsJsonAsync("/api/orders", payload);
        var body = await postResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.GetProperty("id").GetString();

        var deleteResp = await _client.DeleteAsync($"/api/orders/{id}");

        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteOrder_UnknownId_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteOrder_CancelledOrderShowsStatusInGetOrders()
    {
        var payload = new { pair = "USD/EUR", side = "Buy", limitPrice = 0.50m, quantity = 100m };
        var postResp = await _client.PostAsJsonAsync("/api/orders", payload);
        var body = await postResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.GetProperty("id").GetString();

        await _client.DeleteAsync($"/api/orders/{id}");

        var ordersResp = await _client.GetAsync("/api/orders");
        var ordersBody = await ordersResp.Content.ReadAsStringAsync();
        using var ordersDoc = JsonDocument.Parse(ordersBody);

        var status = ordersDoc.RootElement.EnumerateArray()
            .First(e => e.GetProperty("id").GetString() == id)
            .GetProperty("status").GetString();

        status.Should().Be("Cancelled");
    }

    // ── GET /api/positions ────────────────────────────────────────────────────

    [Fact]
    public async Task GetPositions_Returns200WithArray()
    {
        var response = await _client.GetAsync("/api/positions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
