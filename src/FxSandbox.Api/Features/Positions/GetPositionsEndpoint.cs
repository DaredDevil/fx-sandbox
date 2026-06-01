using FxSandbox.Services;

namespace FxSandbox.Features.Positions;

public static class PositionsEndpoints
{
    public static void MapPositionsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/positions", Handle)
           .WithTags("Positions")
           .WithName("GetPositions");
    }

    private static IResult Handle(ITradingEngine engine) =>
        Results.Ok(engine.GetPositions());
}
