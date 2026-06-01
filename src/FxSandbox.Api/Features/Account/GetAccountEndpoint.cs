using FxSandbox.Services;

namespace FxSandbox.Features.Account;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/api/account", Handle)
           .WithTags("Account")
           .WithName("GetAccount");
    }

    private static IResult Handle(ITradingEngine engine) =>
        Results.Ok(new { balance = engine.GetBalance(), currency = "USD" });
}
