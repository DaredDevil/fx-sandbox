using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using FxSandbox.Features.Account;
using FxSandbox.Features.Orders;
using FxSandbox.Features.Positions;
using FxSandbox.Features.Rates;
using FxSandbox.Services;
using FxSandbox.Services.Locking;

var builder = WebApplication.CreateBuilder(args);

// ── Swagger / OpenAPI (dev only) ────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new() { Title = "FX Sandbox API", Version = "v1" });
});

// ── Serialisation ───────────────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ── Problem Details (RFC 7807 error responses) ──────────────────────────────
builder.Services.AddProblemDetails();

// ── Domain services ─────────────────────────────────────────────────────────
builder.Services.AddSingleton<ILockProvider, LocalLockProvider>();
builder.Services.AddSingleton<ITradingEngine, TradingEngine>();
builder.Services.AddHostedService<RateSimulatorService>();
builder.Services.AddHostedService<OrderMatchingService>();

// ── CORS ────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',')
    ?? ["http://localhost:5173"];

builder.Services.AddCors(opts => opts.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins)
          .AllowAnyMethod()
          .AllowAnyHeader()));

// ── Rate limiting ───────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(opts =>
{
    // Global: 100 requests/minute per IP across all endpoints
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10,
            }));

    // Named: tighter limit on order placement to prevent abuse
    opts.AddFixedWindowLimiter("post-orders", o =>
    {
        o.PermitLimit = 20;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });

    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// ── Exception handling ──────────────────────────────────────────────────────
// UseExceptionHandler() with no argument + AddProblemDetails() produces
// application/problem+json for all unhandled exceptions in .NET 8.
app.UseExceptionHandler();

// ── Security headers ────────────────────────────────────────────────────────
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["X-XSS-Protection"] = "0"; // disable legacy XSS auditor
    await next();
});

// ── Rate limiting ───────────────────────────────────────────────────────────
app.UseRateLimiter();

// ── CORS ────────────────────────────────────────────────────────────────────
app.UseCors();

// ── Swagger UI (dev only — not exposed in production) ───────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", "FX Sandbox v1"));
}

// ── Static files (React build in production) ────────────────────────────────
if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// ── API endpoints (Vertical Slice registration) ─────────────────────────────
app.MapRatesEndpoints();
app.MapOrdersEndpoints();
app.MapPositionsEndpoints();
app.MapAccountEndpoints();

// ── SPA fallback ────────────────────────────────────────────────────────────
if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
    app.MapFallbackToFile("index.html");

app.Run();

// Make Program accessible to test projects (WebApplicationFactory<Program>)
public partial class Program { }
