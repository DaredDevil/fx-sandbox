using FluentAssertions;
using FxSandbox.Services;
using Xunit;

namespace FxSandbox.UnitTests;

public sealed class RateSimulatorServiceTests
{
    [Theory]
    [InlineData(0.0, 0.999000)]
    [InlineData(0.5, 1.000000)]
    [InlineData(1.0, 1.001000)]
    public void CalculateNextRate_AppliesDeltaBetweenMinusAndPlusOneTenthPercent(
        double randomSample,
        double expectedRate)
    {
        var rate = RateSimulatorService.CalculateNextRate(1.000000m, randomSample);

        rate.Should().Be((decimal)expectedRate);
    }

    [Fact]
    public void CalculateNextRate_RoundsToSixDecimalPlaces()
    {
        var rate = RateSimulatorService.CalculateNextRate(0.918500m, 1.0);

        rate.Should().Be(0.919418m);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void CalculateNextRate_RejectsRandomSampleOutsideZeroToOne(double randomSample)
    {
        var act = () => RateSimulatorService.CalculateNextRate(1.000000m, randomSample);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(randomSample));
    }
}
