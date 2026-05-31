using FluentValidation;
using FxSandbox.Domain;
using FxSandbox.Services;

namespace FxSandbox.Features.Orders;

public sealed record PlaceOrderRequest(
    string Pair,
    OrderSide Side,
    decimal LimitPrice,
    decimal Quantity);

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.Pair)
            .NotEmpty()
            .Must(p => TradingEngine.SupportedPairs.Contains(p))
            .WithMessage($"Pair must be one of: {string.Join(", ", TradingEngine.SupportedPairs)}");

        RuleFor(x => x.LimitPrice)
            .GreaterThan(0)
            .WithMessage("Limit price must be greater than zero.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than zero.");
    }
}
