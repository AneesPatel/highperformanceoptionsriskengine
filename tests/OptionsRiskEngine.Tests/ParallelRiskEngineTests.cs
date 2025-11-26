using FluentAssertions;
using OptionsRiskEngine.Core;
using OptionsRiskEngine.Models;
using Xunit;

namespace OptionsRiskEngine.Tests;

public class ParallelRiskEngineTests
{
    [Fact]
    public void PriceWithGreeks_ReturnsPositivePrice()
    {
        var engine = new ParallelRiskEngine();
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = engine.PriceWithGreeks(parameters, 5000, useAntitheticVariates: true);

        result.Price.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PriceWithGreeks_DeltaInExpectedRange()
    {
        var engine = new ParallelRiskEngine();
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = engine.PriceWithGreeks(parameters, 5000, useAntitheticVariates: true);

        result.Delta.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void PriceWithGreeks_GammaIsPositive()
    {
        var engine = new ParallelRiskEngine();
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = engine.PriceWithGreeks(parameters, 5000, useAntitheticVariates: true);

        result.Gamma.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PriceWithGreeks_VegaIsPositive()
    {
        var engine = new ParallelRiskEngine();
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = engine.PriceWithGreeks(parameters, 5000, useAntitheticVariates: true);

        result.Vega.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PriceWithGreeks_WithAntitheticVariates_ReducesStandardError()
    {
        var engine = new ParallelRiskEngine();
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var resultWithAntithetic = engine.PriceWithGreeks(parameters, 5000, useAntitheticVariates: true);
        var resultWithoutAntithetic = engine.PriceWithGreeks(parameters, 5000, useAntitheticVariates: false);

        resultWithAntithetic.StandardError.Should().BeLessThan(resultWithoutAntithetic.StandardError);
    }

    [Fact]
    public void PricePortfolio_PricesMultipleOptions()
    {
        var engine = new ParallelRiskEngine();
        var portfolio = new OptionParameters[]
        {
            new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2),
            new OptionParameters(105.0, 100.0, 1.0, 0.05, 0.2),
            new OptionParameters(110.0, 100.0, 1.0, 0.05, 0.2)
        };

        var results = engine.PricePortfolio(portfolio, 50000, useAntitheticVariates: true);

        results.Should().HaveCount(3);
        foreach (var result in results)
        {
            result.Price.Should().BeGreaterThan(0);
            result.Delta.Should().BeInRange(0.0, 1.0);
            result.Gamma.Should().BeGreaterThan(0);
            result.Vega.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void PricePortfolio_HigherSpotPrice_ProducesHigherPrice()
    {
        var engine = new ParallelRiskEngine();
        var portfolio = new OptionParameters[]
        {
            new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2),
            new OptionParameters(110.0, 100.0, 1.0, 0.05, 0.2)
        };

        var results = engine.PricePortfolio(portfolio, 5000, useAntitheticVariates: true);

        results[1].Price.Should().BeGreaterThan(results[0].Price);
    }

    [Fact]
    public void Constructor_WithSpecificDegreeOfParallelism_UsesSpecifiedValue()
    {
        var engine = new ParallelRiskEngine(maxDegreeOfParallelism: 4);
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = engine.PriceWithGreeks(parameters, 5000, useAntitheticVariates: true);

        result.Price.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PriceWithGreeks_ThrowsException_WithInvalidParameters()
    {
        var engine = new ParallelRiskEngine();
        var parameters = new OptionParameters(-100.0, 100.0, 1.0, 0.05, 0.2);

        Action act = () => engine.PriceWithGreeks(parameters, 5000);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PriceWithGreeks_DeepInTheMoney_DeltaNearOne()
    {
        var engine = new ParallelRiskEngine();
        var parameters = new OptionParameters(150.0, 100.0, 1.0, 0.05, 0.2);

        var result = engine.PriceWithGreeks(parameters, 5000, useAntitheticVariates: true);

        result.Delta.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public void PriceWithGreeks_DeepOutOfTheMoney_DeltaNearZero()
    {
        var engine = new ParallelRiskEngine();
        var parameters = new OptionParameters(50.0, 100.0, 1.0, 0.05, 0.2);

        var result = engine.PriceWithGreeks(parameters, 5000, useAntitheticVariates: true);

        result.Delta.Should().BeLessThan(0.2);
    }
}
