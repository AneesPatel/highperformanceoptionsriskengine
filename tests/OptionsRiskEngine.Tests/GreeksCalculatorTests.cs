using FluentAssertions;
using OptionsRiskEngine.Core;
using OptionsRiskEngine.Models;
using Xunit;

namespace OptionsRiskEngine.Tests;

public class GreeksCalculatorTests
{
    [Fact]
    public void CalculateDelta_ReturnsCorrectValue()
    {
        double priceUp = 10.5;
        double priceDown = 9.5;
        double epsilon = 1.0;

        double delta = GreeksCalculator.CalculateDelta(priceUp, priceDown, epsilon);

        delta.Should().BeApproximately(0.5, 1e-10);
    }

    [Fact]
    public void CalculateGamma_ReturnsCorrectValue()
    {
        double priceUp = 11.0;
        double priceBase = 10.0;
        double priceDown = 9.0;
        double epsilon = 1.0;

        double gamma = GreeksCalculator.CalculateGamma(priceUp, priceBase, priceDown, epsilon);

        gamma.Should().BeApproximately(0.0, 1e-10);
    }

    [Fact]
    public void CalculateVega_ReturnsCorrectValue()
    {
        double priceVolUp = 11.0;
        double priceVolDown = 9.0;
        double epsilon = 0.01;

        double vega = GreeksCalculator.CalculateVega(priceVolUp, priceVolDown, epsilon);

        vega.Should().BeApproximately(100.0, 1e-10);
    }

    [Fact]
    public void CalculateWithGreeks_ReturnsPositivePrice()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = GreeksCalculator.CalculateWithGreeks(parameters, 10000, useAntitheticVariates: true);

        result.Price.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateWithGreeks_DeltaInExpectedRange()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = GreeksCalculator.CalculateWithGreeks(parameters, 5000, useAntitheticVariates: true);

        result.Delta.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void CalculateWithGreeks_GammaIsPositive()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = GreeksCalculator.CalculateWithGreeks(parameters, 5000, useAntitheticVariates: true);

        result.Gamma.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateWithGreeks_VegaIsPositive()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = GreeksCalculator.CalculateWithGreeks(parameters, 5000, useAntitheticVariates: true);

        result.Vega.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateWithGreeksSimdParallel_ReturnsPositivePrice()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = GreeksCalculator.CalculateWithGreeksSimdParallel(parameters, 10000, useAntitheticVariates: true);

        result.Price.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateWithGreeksSimdParallel_ProducesSimilarResults_ToSequentialVersion()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var sequentialResult = GreeksCalculator.CalculateWithGreeks(parameters, 5000, useAntitheticVariates: true);
        var parallelResult = GreeksCalculator.CalculateWithGreeksSimdParallel(parameters, 5000, useAntitheticVariates: true);

        parallelResult.Price.Should().BeApproximately(sequentialResult.Price, sequentialResult.Price * 0.05);
        parallelResult.Delta.Should().BeApproximately(sequentialResult.Delta, 0.05);
        parallelResult.Gamma.Should().BeApproximately(sequentialResult.Gamma, sequentialResult.Gamma * 0.2);
        parallelResult.Vega.Should().BeApproximately(sequentialResult.Vega, sequentialResult.Vega * 0.1);
    }

    [Fact]
    public void CalculateWithGreeks_ThrowsException_WithInvalidParameters()
    {
        var parameters = new OptionParameters(-100.0, 100.0, 1.0, 0.05, 0.2);

        Action act = () => GreeksCalculator.CalculateWithGreeks(parameters, 10000);

        act.Should().Throw<ArgumentException>();
    }
}
