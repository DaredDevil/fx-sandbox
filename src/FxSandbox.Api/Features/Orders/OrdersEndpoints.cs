using FxSandbox.Services;

namespace FxSandbox.Features.Orders;

public static class OrdersEndpoints
{
    public static void MapOrdersEndpoints(this WebApplication app)
    {
        app.MapGet("/api/orders", GetOrders)
           .WithTags("Orders")
           .WithName("GetOrders");

        app.MapPost("/api/orders", PlaceOrder)
           .WithTags("Orders")
           .WithName("PlaceOrder")
           .RequireRateLimiting("post-orders");

        app.MapDelete("/api/orders/{id:guid}", CancelOrder)
           .WithTags("Orders")
           .WithName("CancelOrder");
    }

    private static IResult GetOrders(ITradingEngine engine) =>
        Results.Ok(engine.GetOrders().OrderByDescending(o => o.CreatedAt));

    private static IResult PlaceOrder(PlaceOrderRequest request, ITradingEngine engine)
    {
        var validation = new PlaceOrderValidator().Validate(request);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = engine.PlaceOrder(request);
        return result.IsSuccess
            ? Results.Created($"/api/orders/{result.Order!.Id}", result.Order)
            : Results.UnprocessableEntity(new { error = result.Error });
    }

    private static IResult CancelOrder(Guid id, ITradingEngine engine) =>
        engine.CancelOrder(id) ? Results.NoContent() : Results.NotFound();
}
