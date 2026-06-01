using FxSandbox.Services;

namespace FxSandbox.Features.Account;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/api/account", GetAccount)
           .WithTags("Account")
           .WithName("GetAccount");

        app.MapPost("/api/reset", Reset)
           .WithTags("Account")
           .WithName("Reset");
    }

    private static IResult GetAccount(ITradingEngine engine) =>
        Results.Ok(new { balance = engine.GetBalance(), currency = "USD" });

    private static IResult Reset(ITradingEngine engine)
    {
        engine.Reset();
        return Results.Ok(new { balance = engine.GetBalance(), currency = "USD" });
    }
}
