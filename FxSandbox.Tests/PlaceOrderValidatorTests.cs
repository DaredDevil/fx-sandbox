using FluentAssertions;
using FxSandbox.Domain;
using FxSandbox.Features.Orders;
using Xunit;

namespace FxSandbox.Tests;

public sealed class PlaceOrderValidatorTests
{
    private readonly PlaceOrderValidator _validator = new();

    [Theory]
    [InlineData("USD/EUR")]
    [InlineData("USD/GBP")]
    [InlineData("USD/CHF")]
    public void ValidRequest_PassesValidation(string pair)
    {
        var req = new PlaceOrderRequest(pair, OrderSide.Buy, 0.90m, 1000m);
        _validator.Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void InvalidPair_FailsValidation()
    {
        var req = new PlaceOrderRequest("USD/JPY", OrderSide.Buy, 0.90m, 1000m);
        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Pair");
    }

    [Fact]
    public void ZeroLimitPrice_FailsValidation()
    {
        var req = new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0m, 1000m);
        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "LimitPrice");
    }

    [Fact]
    public void NegativeQuantity_FailsValidation()
    {
        var req = new PlaceOrderRequest("USD/EUR", OrderSide.Buy, 0.90m, -100m);
        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Quantity");
    }
}
