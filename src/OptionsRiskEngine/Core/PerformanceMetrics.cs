using System.Diagnostics;
using OptionsRiskEngine.Models;

namespace OptionsRiskEngine.Core;

public sealed class PerformanceMetrics
{
    public static PerformanceComparison MeasurePerformanceImprovement(
        in OptionParameters parameters,
        int simulationPaths,
        bool useAntitheticVariates = true)
    {
        var sw = Stopwatch.StartNew();
        var scalarResult = ScalarRiskEngine.PriceWithGreeksScalar(parameters, simulationPaths, useAntitheticVariates);
        sw.Stop();
        long scalarTicks = sw.ElapsedTicks;
        double scalarMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        var simdResult = SimdGreeksEngine.CalculateWithAvx2Greeks(parameters, simulationPaths, useAntitheticVariates);
        sw.Stop();
        long simdTicks = sw.ElapsedTicks;
        double simdMs = sw.Elapsed.TotalMilliseconds;

        double throughputImprovement = ((double)scalarTicks / simdTicks - 1.0) * 100.0;
        double speedup = (double)scalarTicks / simdTicks;

        return new PerformanceComparison
        {
            ScalarTimeMs = scalarMs,
            SimdTimeMs = simdMs,
            ThroughputImprovementPercent = throughputImprovement,
            SpeedupFactor = speedup,
            ScalarResult = scalarResult,
            SimdResult = simdResult
        };
    }

    public static VarianceReductionMetrics MeasureAntitheticVariatesEffectiveness(
        in OptionParameters parameters,
        int simulationPaths,
        int iterations = 10)
    {
        double[] standardErrors = new double[iterations];
        double[] antitheticErrors = new double[iterations];

        for (int i = 0; i < iterations; i++)
        {
            var standardResult = SimdGreeksEngine.CalculateWithAvx2Greeks(parameters, simulationPaths, useAntitheticVariates: false);
            standardErrors[i] = standardResult.StandardError;

            var antitheticResult = SimdGreeksEngine.CalculateWithAvx2Greeks(parameters, simulationPaths, useAntitheticVariates: true);
            antitheticErrors[i] = antitheticResult.StandardError;
        }

        double avgStandardError = standardErrors.Average();
        double avgAntitheticError = antitheticErrors.Average();
        double varianceReduction = (1.0 - (avgAntitheticError / avgStandardError)) * 100.0;
        double convergenceSpeedup = Math.Pow(avgStandardError / avgAntitheticError, 2);

        return new VarianceReductionMetrics
        {
            StandardErrorWithoutAntithetic = avgStandardError,
            StandardErrorWithAntithetic = avgAntitheticError,
            VarianceReductionPercent = varianceReduction,
            ConvergenceSpeedupFactor = convergenceSpeedup
        };
    }
}

public readonly struct PerformanceComparison
{
    public required double ScalarTimeMs { get; init; }
    public required double SimdTimeMs { get; init; }
    public required double ThroughputImprovementPercent { get; init; }
    public required double SpeedupFactor { get; init; }
    public required PricingResult ScalarResult { get; init; }
    public required PricingResult SimdResult { get; init; }
}

public readonly struct VarianceReductionMetrics
{
    public required double StandardErrorWithoutAntithetic { get; init; }
    public required double StandardErrorWithAntithetic { get; init; }
    public required double VarianceReductionPercent { get; init; }
    public required double ConvergenceSpeedupFactor { get; init; }
}
