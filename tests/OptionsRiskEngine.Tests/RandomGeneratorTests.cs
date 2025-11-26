using FluentAssertions;
using OptionsRiskEngine.Core;
using Xunit;

namespace OptionsRiskEngine.Tests;

public class RandomGeneratorTests
{
    [Fact]
    public void FillGaussianSamples_FillsAllElements()
    {
        Span<double> samples = stackalloc double[1000];

        RandomGenerator.FillGaussianSamples(samples);

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i].Should().NotBe(0.0);
        }
    }

    [Fact]
    public void FillAntitheticPairs_CreatesNegativePairs()
    {
        Span<double> samples = stackalloc double[1000];

        RandomGenerator.FillAntitheticPairs(samples);

        int halfLength = samples.Length / 2;
        for (int i = 0; i < halfLength; i++)
        {
            samples[i].Should().BeApproximately(-samples[halfLength + i], 1e-10);
        }
    }

    [Fact]
    public void FillGaussianSamples_ProducesApproximatelyNormalDistribution()
    {
        Span<double> samples = stackalloc double[10000];

        RandomGenerator.FillGaussianSamples(samples);

        double sum = 0.0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i];
        }
        double mean = sum / samples.Length;

        double sumSquaredDiff = 0.0;
        for (int i = 0; i < samples.Length; i++)
        {
            double diff = samples[i] - mean;
            sumSquaredDiff += diff * diff;
        }
        double variance = sumSquaredDiff / (samples.Length - 1);
        double stdDev = Math.Sqrt(variance);

        mean.Should().BeInRange(-0.1, 0.1);
        stdDev.Should().BeInRange(0.9, 1.1);
    }

    [Fact]
    public void FillGaussianSamplesVectorized_FillsAllElements()
    {
        Span<double> samples = stackalloc double[1000];

        RandomGenerator.FillGaussianSamplesVectorized(samples);

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i].Should().NotBe(0.0);
        }
    }

    [Fact]
    public void FillGaussianSamplesVectorized_HandlesNonVectorSizedArrays()
    {
        Span<double> samples = stackalloc double[1003];

        RandomGenerator.FillGaussianSamplesVectorized(samples);

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i].Should().NotBe(0.0);
        }
    }
}
