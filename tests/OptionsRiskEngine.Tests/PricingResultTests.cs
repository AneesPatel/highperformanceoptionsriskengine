using FluentAssertions;
using OptionsRiskEngine.Models;
using Xunit;

namespace OptionsRiskEngine.Tests;

public class PricingResultTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var result = new PricingResult(10.5, 0.6, 0.02, 15.3, 0.05);

        result.Price.Should().Be(10.5);
        result.Delta.Should().Be(0.6);
        result.Gamma.Should().Be(0.02);
        result.Vega.Should().Be(15.3);
        result.StandardError.Should().Be(0.05);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var result = new PricingResult(10.5, 0.6, 0.02, 15.3, 0.05);

        var str = result.ToString();

        str.Should().Contain("10.5");
        str.Should().Contain("0.6");
        str.Should().Contain("0.02");
        str.Should().Contain("15.3");
        str.Should().Contain("0.05");
    }

    [Fact]
    public void ToString_FormatsNumbersWithCorrectPrecision()
    {
        var result = new PricingResult(10.123456, 0.654321, 0.0123456, 15.987654, 0.0567890);

        var str = result.ToString();

        str.Should().Contain("10.1235");
        str.Should().Contain("0.6543");
        str.Should().Contain("0.012346");
        str.Should().Contain("15.9877");
        str.Should().Contain("0.056789");
    }
}
