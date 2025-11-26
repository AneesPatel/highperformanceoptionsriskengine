using FluentAssertions;
using OptionsRiskEngine.Core;
using Xunit;

namespace OptionsRiskEngine.Tests;

public class MonteCarloSimulatorTests
{
    [Fact]
    public void SimulatePathScalar_ReturnsPositivePrice()
    {
        double spotPrice = 100.0;
        double drift = 0.025;
        double diffusion = 0.2;
        double randomSample = 0.5;

        double result = MonteCarloSimulator.SimulatePathScalar(spotPrice, drift, diffusion, randomSample);

        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimulatePathScalar_WithZeroVolatility_ReturnsExpectedPrice()
    {
        double spotPrice = 100.0;
        double drift = 0.05;
        double diffusion = 0.0;
        double randomSample = 0.5;

        double result = MonteCarloSimulator.SimulatePathScalar(spotPrice, drift, diffusion, randomSample);

        double expected = spotPrice * Math.Exp(drift);
        result.Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void CalculatePayoff_ReturnsZero_WhenSpotBelowStrike()
    {
        double spotAtMaturity = 95.0;
        double strikePrice = 100.0;

        double result = MonteCarloSimulator.CalculatePayoff(spotAtMaturity, strikePrice);

        result.Should().Be(0.0);
    }

    [Fact]
    public void CalculatePayoff_ReturnsIntrinsicValue_WhenSpotAboveStrike()
    {
        double spotAtMaturity = 105.0;
        double strikePrice = 100.0;

        double result = MonteCarloSimulator.CalculatePayoff(spotAtMaturity, strikePrice);

        result.Should().Be(5.0);
    }

    [Fact]
    public void SimulatePathsVectorized_FillsAllPrices()
    {
        Span<double> randomSamples = stackalloc double[1000];
        Span<double> finalPrices = stackalloc double[1000];
        RandomGenerator.FillGaussianSamples(randomSamples);

        MonteCarloSimulator.SimulatePathsVectorized(
            randomSamples,
            finalPrices,
            100.0,
            0.025,
            0.2);

        for (int i = 0; i < finalPrices.Length; i++)
        {
            finalPrices[i].Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void CalculatePayoffsVectorized_ProducesCorrectPayoffs()
    {
        Span<double> finalPrices = stackalloc double[100];
        Span<double> payoffs = stackalloc double[100];
        
        for (int i = 0; i < 50; i++)
        {
            finalPrices[i] = 95.0;
        }
        for (int i = 50; i < 100; i++)
        {
            finalPrices[i] = 110.0;
        }

        MonteCarloSimulator.CalculatePayoffsVectorized(finalPrices, payoffs, 100.0);

        for (int i = 0; i < 50; i++)
        {
            payoffs[i].Should().Be(0.0);
        }
        for (int i = 50; i < 100; i++)
        {
            payoffs[i].Should().Be(10.0);
        }
    }

    [Fact]
    public void CalculateMean_ReturnsCorrectAverage()
    {
        Span<double> values = stackalloc double[5];
        values[0] = 1.0;
        values[1] = 2.0;
        values[2] = 3.0;
        values[3] = 4.0;
        values[4] = 5.0;

        double result = MonteCarloSimulator.CalculateMean(values);

        result.Should().BeApproximately(3.0, 1e-10);
    }

    [Fact]
    public void CalculateStandardError_ReturnsPositiveValue()
    {
        Span<double> values = stackalloc double[100];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = i;
        }

        double mean = MonteCarloSimulator.CalculateMean(values);
        double result = MonteCarloSimulator.CalculateStandardError(values, mean);

        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateStandardError_WithIdenticalValues_ReturnsZero()
    {
        Span<double> values = stackalloc double[100];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = 5.0;
        }

        double mean = MonteCarloSimulator.CalculateMean(values);
        double result = MonteCarloSimulator.CalculateStandardError(values, mean);

        result.Should().BeApproximately(0.0, 1e-10);
    }
}
