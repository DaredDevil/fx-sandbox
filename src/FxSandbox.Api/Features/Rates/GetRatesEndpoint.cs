using FxSandbox.Services;

namespace FxSandbox.Features.Rates;

public static class RatesEndpoints
{
    public static void MapRatesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rates", Handle)
           .WithTags("Rates")
           .WithName("GetRates");
    }

    private static IResult Handle(ITradingEngine engine)
    {
        var rates = engine.GetRates();
        return Results.Ok(rates.Select(kvp => new
        {
            pair = kvp.Key,
            value = kvp.Value,
            updatedAt = DateTime.UtcNow,
        }));
    }
}
