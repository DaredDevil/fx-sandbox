using System.Text.Json.Serialization;
using FxSandbox.Features.Orders;
using FxSandbox.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<TradingEngine>();
builder.Services.AddHostedService<RateSimulatorService>();
builder.Services.AddHostedService<OrderMatchingService>();

var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',')
    ?? ["http://localhost:5173"];

builder.Services.AddCors(opts => opts.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins)
          .AllowAnyMethod()
          .AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

// Serve React static files when wwwroot is present (production/Docker)
if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// ── Rates ──────────────────────────────────────────────────────────────────

app.MapGet("/api/rates", (TradingEngine engine) =>
{
    var rates = engine.GetRates();
    return Results.Ok(rates.Select(kvp => new
    {
        pair = kvp.Key,
        value = kvp.Value,
        updatedAt = DateTime.UtcNow,
    }));
});

// ── Orders ─────────────────────────────────────────────────────────────────

app.MapGet("/api/orders", (TradingEngine engine) =>
    Results.Ok(engine.GetOrders().OrderByDescending(o => o.CreatedAt)));

app.MapPost("/api/orders", (PlaceOrderRequest request, TradingEngine engine) =>
{
    var validation = new PlaceOrderValidator().Validate(request);
    if (!validation.IsValid)
        return Results.ValidationProblem(validation.ToDictionary());

    var order = engine.PlaceOrder(request);
    return Results.Created($"/api/orders/{order.Id}", order);
});

app.MapDelete("/api/orders/{id:guid}", (Guid id, TradingEngine engine) =>
    engine.CancelOrder(id) ? Results.NoContent() : Results.NotFound());

// ── Positions ──────────────────────────────────────────────────────────────

app.MapGet("/api/positions", (TradingEngine engine) =>
    Results.Ok(engine.GetPositions()));

// ── Account ────────────────────────────────────────────────────────────────

app.MapGet("/api/account", (TradingEngine engine) =>
    Results.Ok(new { balance = engine.GetBalance(), currency = "USD" }));

// SPA fallback: serve index.html for any non-API route so React router works
if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
    app.MapFallbackToFile("index.html");

app.Run();

// Make Program accessible to the test project
public partial class Program { }
