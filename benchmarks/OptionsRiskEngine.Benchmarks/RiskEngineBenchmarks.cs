using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OptionsRiskEngine.Core;
using OptionsRiskEngine.Models;

namespace OptionsRiskEngine.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class RiskEngineBenchmarks
{
    private OptionParameters testParameters;
    private ParallelRiskEngine parallelEngine;

    [GlobalSetup]
    public void Setup()
    {
        testParameters = new OptionParameters(
            spotPrice: 100.0,
            strikePrice: 100.0,
            timeToMaturity: 1.0,
            riskFreeRate: 0.05,
            volatility: 0.2);

        parallelEngine = new ParallelRiskEngine();
    }

    [Benchmark(Description = "Scalar Logic - 100K Paths")]
    public PricingResult ScalarEngine_100K()
    {
        return ScalarRiskEngine.PriceWithGreeksScalar(testParameters, 100_000, useAntitheticVariates: true);
    }

    [Benchmark(Description = "SIMD Risk Engine - 100K Paths")]
    public PricingResult SimdEngine_100K()
    {
        return GreeksCalculator.CalculateWithGreeksSimdParallel(testParameters, 100_000, useAntitheticVariates: true);
    }

    [Benchmark(Description = "Parallel SIMD Engine - 100K Paths")]
    public PricingResult ParallelSimdEngine_100K()
    {
        return parallelEngine.PriceWithGreeks(testParameters, 100_000, useAntitheticVariates: true);
    }

    [Benchmark(Description = "Scalar Logic - 1M Paths")]
    public PricingResult ScalarEngine_1M()
    {
        return ScalarRiskEngine.PriceWithGreeksScalar(testParameters, 1_000_000, useAntitheticVariates: true);
    }

    [Benchmark(Description = "SIMD Risk Engine - 1M Paths")]
    public PricingResult SimdEngine_1M()
    {
        return GreeksCalculator.CalculateWithGreeksSimdParallel(testParameters, 1_000_000, useAntitheticVariates: true);
    }

    [Benchmark(Description = "Parallel SIMD Engine - 1M Paths", Baseline = true)]
    public PricingResult ParallelSimdEngine_1M()
    {
        return parallelEngine.PriceWithGreeks(testParameters, 1_000_000, useAntitheticVariates: true);
    }

    [Benchmark(Description = "Portfolio Pricing - 10 Options")]
    public PricingResult[] PortfolioPricing()
    {
        var portfolio = new OptionParameters[10];
        for (int i = 0; i < 10; i++)
        {
            portfolio[i] = new OptionParameters(
                spotPrice: 100.0 + i * 5,
                strikePrice: 100.0,
                timeToMaturity: 1.0,
                riskFreeRate: 0.05,
                volatility: 0.2);
        }

        return parallelEngine.PricePortfolio(portfolio, 100_000, useAntitheticVariates: true);
    }
}
