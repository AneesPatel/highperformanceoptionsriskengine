using FluentAssertions;
using OptionsRiskEngine.Core;
using OptionsRiskEngine.Models;
using Xunit;

namespace OptionsRiskEngine.Tests;

public class ScalarRiskEngineTests
{
    [Fact]
    public void PriceWithGreeksScalar_ReturnsPositivePrice()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = ScalarRiskEngine.PriceWithGreeksScalar(parameters, 10000, useAntitheticVariates: true);

        result.Price.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PriceWithGreeksScalar_DeltaInExpectedRange()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = ScalarRiskEngine.PriceWithGreeksScalar(parameters, 5000, useAntitheticVariates: true);

        result.Delta.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void PriceWithGreeksScalar_GammaIsPositive()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = ScalarRiskEngine.PriceWithGreeksScalar(parameters, 5000, useAntitheticVariates: true);

        result.Gamma.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PriceWithGreeksScalar_VegaIsPositive()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var result = ScalarRiskEngine.PriceWithGreeksScalar(parameters, 5000, useAntitheticVariates: true);

        result.Vega.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PriceWithGreeksScalar_WithAntitheticVariates_ReducesStandardError()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var resultWithAntithetic = ScalarRiskEngine.PriceWithGreeksScalar(parameters, 5000, useAntitheticVariates: true);
        var resultWithoutAntithetic = ScalarRiskEngine.PriceWithGreeksScalar(parameters, 5000, useAntitheticVariates: false);

        resultWithAntithetic.StandardError.Should().BeLessThan(resultWithoutAntithetic.StandardError);
    }

    [Fact]
    public void PriceWithGreeksScalar_ThrowsException_WithInvalidParameters()
    {
        var parameters = new OptionParameters(-100.0, 100.0, 1.0, 0.05, 0.2);

        Action act = () => ScalarRiskEngine.PriceWithGreeksScalar(parameters, 10000);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PriceWithGreeksScalar_ProducesSimilarResults_ToVectorizedVersion()
    {
        var parameters = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.2);

        var scalarResult = ScalarRiskEngine.PriceWithGreeksScalar(parameters, 5000, useAntitheticVariates: true);
        var vectorizedResult = GreeksCalculator.CalculateWithGreeks(parameters, 5000, useAntitheticVariates: true);

        scalarResult.Price.Should().BeApproximately(vectorizedResult.Price, vectorizedResult.Price * 0.05);
        scalarResult.Delta.Should().BeApproximately(vectorizedResult.Delta, 0.05);
    }

    [Fact]
    public void PriceWithGreeksScalar_HigherVolatility_IncreasesPrice()
    {
        var lowVolParams = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.1);
        var highVolParams = new OptionParameters(100.0, 100.0, 1.0, 0.05, 0.3);

        var lowVolResult = ScalarRiskEngine.PriceWithGreeksScalar(lowVolParams, 5000, useAntitheticVariates: true);
        var highVolResult = ScalarRiskEngine.PriceWithGreeksScalar(highVolParams, 5000, useAntitheticVariates: true);

        highVolResult.Price.Should().BeGreaterThan(lowVolResult.Price);
    }

    [Fact]
    public void PriceWithGreeksScalar_LongerMaturity_IncreasesPrice()
    {
        var shortMaturityParams = new OptionParameters(100.0, 100.0, 0.5, 0.05, 0.2);
        var longMaturityParams = new OptionParameters(100.0, 100.0, 2.0, 0.05, 0.2);

        var shortMaturityResult = ScalarRiskEngine.PriceWithGreeksScalar(shortMaturityParams, 5000, useAntitheticVariates: true);
        var longMaturityResult = ScalarRiskEngine.PriceWithGreeksScalar(longMaturityParams, 5000, useAntitheticVariates: true);

        longMaturityResult.Price.Should().BeGreaterThan(shortMaturityResult.Price);
    }
}
