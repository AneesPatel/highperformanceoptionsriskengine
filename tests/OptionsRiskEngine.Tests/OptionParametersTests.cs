using FluentAssertions;
using OptionsRiskEngine.Core;
using OptionsRiskEngine.Models;
using Xunit;

namespace OptionsRiskEngine.Tests;

public class OptionParametersTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var parameters = new OptionParameters(100.0, 105.0, 1.0, 0.05, 0.2);

        parameters.SpotPrice.Should().Be(100.0);
        parameters.StrikePrice.Should().Be(105.0);
        parameters.TimeToMaturity.Should().Be(1.0);
        parameters.RiskFreeRate.Should().Be(0.05);
        parameters.Volatility.Should().Be(0.2);
    }

    [Fact]
    public void Validate_ThrowsException_WhenSpotPriceIsNegative()
    {
        var parameters = new OptionParameters(-100.0, 105.0, 1.0, 0.05, 0.2);

        Action act = () => parameters.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("SpotPrice");
    }

    [Fact]
    public void Validate_ThrowsException_WhenStrikePriceIsZero()
    {
        var parameters = new OptionParameters(100.0, 0.0, 1.0, 0.05, 0.2);

        Action act = () => parameters.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("StrikePrice");
    }

    [Fact]
    public void Validate_ThrowsException_WhenTimeToMaturityIsNegative()
    {
        var parameters = new OptionParameters(100.0, 105.0, -1.0, 0.05, 0.2);

        Action act = () => parameters.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("TimeToMaturity");
    }

    [Fact]
    public void Validate_ThrowsException_WhenRiskFreeRateIsNegative()
    {
        var parameters = new OptionParameters(100.0, 105.0, 1.0, -0.05, 0.2);

        Action act = () => parameters.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("RiskFreeRate");
    }

    [Fact]
    public void Validate_ThrowsException_WhenVolatilityIsZero()
    {
        var parameters = new OptionParameters(100.0, 105.0, 1.0, 0.05, 0.0);

        Action act = () => parameters.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("Volatility");
    }

    [Fact]
    public void Validate_DoesNotThrow_WhenParametersAreValid()
    {
        var parameters = new OptionParameters(100.0, 105.0, 1.0, 0.05, 0.2);

        Action act = () => parameters.Validate();

        act.Should().NotThrow();
    }
}
