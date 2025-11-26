using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OptionsRiskEngine.Models;

namespace OptionsRiskEngine.Core;

public sealed class ParallelRiskEngine
{
    private readonly int maxDegreeOfParallelism;

    public ParallelRiskEngine(int? maxDegreeOfParallelism = null)
    {
        this.maxDegreeOfParallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;
    }

    public PricingResult PriceWithGreeks(
        in OptionParameters parameters,
        int totalSimulationPaths,
        bool useAntitheticVariates = true)
    {
        parameters.Validate();

        int pathsPerPartition = totalSimulationPaths / maxDegreeOfParallelism;
        int remainder = totalSimulationPaths % maxDegreeOfParallelism;

        double spotEpsilon = parameters.SpotPrice * 0.01;
        double volEpsilon = 0.0001;

        var paramsBase = parameters;
        var paramsSpotUp = new OptionParameters(
            parameters.SpotPrice + spotEpsilon,
            parameters.StrikePrice,
            parameters.TimeToMaturity,
            parameters.RiskFreeRate,
            parameters.Volatility);
        var paramsSpotDown = new OptionParameters(
            parameters.SpotPrice - spotEpsilon,
            parameters.StrikePrice,
            parameters.TimeToMaturity,
            parameters.RiskFreeRate,
            parameters.Volatility);
        var paramsVolUp = new OptionParameters(
            parameters.SpotPrice,
            parameters.StrikePrice,
            parameters.TimeToMaturity,
            parameters.RiskFreeRate,
            parameters.Volatility + volEpsilon);
        var paramsVolDown = new OptionParameters(
            parameters.SpotPrice,
            parameters.StrikePrice,
            parameters.TimeToMaturity,
            parameters.RiskFreeRate,
            parameters.Volatility - volEpsilon);

        var partitioner = Partitioner.Create(0, maxDegreeOfParallelism);

        var priceResults = new ConcurrentBag<(double price, double sumSquared, int count)>();
        var priceUpResults = new ConcurrentBag<(double price, double sumSquared, int count)>();
        var priceDownResults = new ConcurrentBag<(double price, double sumSquared, int count)>();
        var priceVolUpResults = new ConcurrentBag<(double price, double sumSquared, int count)>();
        var priceVolDownResults = new ConcurrentBag<(double price, double sumSquared, int count)>();

        Parallel.ForEach(partitioner, range =>
        {
            for (int i = range.Item1; i < range.Item2; i++)
            {
                int paths = pathsPerPartition + (i < remainder ? 1 : 0);

                var result = SimulatePartition(paramsBase, paths, useAntitheticVariates);
                priceResults.Add(result);

                var resultUp = SimulatePartition(paramsSpotUp, paths, useAntitheticVariates);
                priceUpResults.Add(resultUp);

                var resultDown = SimulatePartition(paramsSpotDown, paths, useAntitheticVariates);
                priceDownResults.Add(resultDown);

                var resultVolUp = SimulatePartition(paramsVolUp, paths, useAntitheticVariates);
                priceVolUpResults.Add(resultVolUp);

                var resultVolDown = SimulatePartition(paramsVolDown, paths, useAntitheticVariates);
                priceVolDownResults.Add(resultVolDown);
            }
        });

        var (priceBase, seBase) = AggregateResults(priceResults);
        var (priceUp, _) = AggregateResults(priceUpResults);
        var (priceDown, _) = AggregateResults(priceDownResults);
        var (priceVolUp, _) = AggregateResults(priceVolUpResults);
        var (priceVolDown, _) = AggregateResults(priceVolDownResults);

        double delta = (priceUp - priceDown) / (2.0 * spotEpsilon);
        double gamma = (priceUp - 2.0 * priceBase + priceDown) / (spotEpsilon * spotEpsilon);
        double vega = (priceVolUp - priceVolDown) / (2.0 * volEpsilon);

        return new PricingResult(priceBase, delta, gamma, vega, seBase);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double price, double sumSquared, int count) SimulatePartition(
        in OptionParameters parameters,
        int paths,
        bool useAntitheticVariates)
    {
        double drift = (parameters.RiskFreeRate - 0.5 * parameters.Volatility * parameters.Volatility) * parameters.TimeToMaturity;
        double diffusion = parameters.Volatility * Math.Sqrt(parameters.TimeToMaturity);
        double discountFactor = Math.Exp(-parameters.RiskFreeRate * parameters.TimeToMaturity);

        int actualPaths = useAntitheticVariates ? (paths / 2) * 2 : paths;

        const int maxStackSize = 8192;
        bool useStack = actualPaths <= maxStackSize;

        double sum = 0.0;
        double sumSquared = 0.0;

        if (useStack)
        {
            Span<double> randomSamples = stackalloc double[actualPaths];
            Span<double> finalPrices = stackalloc double[actualPaths];
            Span<double> payoffs = stackalloc double[actualPaths];

            if (useAntitheticVariates)
            {
                RandomGenerator.FillAntitheticPairs(randomSamples);
            }
            else
            {
                RandomGenerator.FillGaussianSamples(randomSamples);
            }

            MonteCarloSimulator.SimulatePathsVectorized(
                randomSamples,
                finalPrices,
                parameters.SpotPrice,
                drift,
                diffusion);

            MonteCarloSimulator.CalculatePayoffsVectorized(
                finalPrices,
                payoffs,
                parameters.StrikePrice);

            for (int i = 0; i < actualPaths; i++)
            {
                double discountedPayoff = payoffs[i] * discountFactor;
                sum += discountedPayoff;
                sumSquared += discountedPayoff * discountedPayoff;
            }
        }
        else
        {
            double[] randomSamples = new double[actualPaths];
            double[] finalPrices = new double[actualPaths];
            double[] payoffs = new double[actualPaths];

            if (useAntitheticVariates)
            {
                RandomGenerator.FillAntitheticPairs(randomSamples);
            }
            else
            {
                RandomGenerator.FillGaussianSamples(randomSamples);
            }

            MonteCarloSimulator.SimulatePathsVectorized(
                randomSamples,
                finalPrices,
                parameters.SpotPrice,
                drift,
                diffusion);

            MonteCarloSimulator.CalculatePayoffsVectorized(
                finalPrices,
                payoffs,
                parameters.StrikePrice);

            for (int i = 0; i < actualPaths; i++)
            {
                double discountedPayoff = payoffs[i] * discountFactor;
                sum += discountedPayoff;
                sumSquared += discountedPayoff * discountedPayoff;
            }
        }

        return (sum, sumSquared, actualPaths);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double price, double standardError) AggregateResults(
        ConcurrentBag<(double price, double sumSquared, int count)> results)
    {
        double totalSum = 0.0;
        double totalSumSquared = 0.0;
        int totalCount = 0;

        foreach (var (price, sumSquared, count) in results)
        {
            totalSum += price;
            totalSumSquared += sumSquared;
            totalCount += count;
        }

        double mean = totalSum / totalCount;
        double variance = (totalSumSquared / totalCount) - (mean * mean);
        double standardError = Math.Sqrt(variance / totalCount);

        return (mean, standardError);
    }

    public PricingResult[] PricePortfolio(
        OptionParameters[] portfolio,
        int simulationPathsPerOption,
        bool useAntitheticVariates = true)
    {
        var results = new PricingResult[portfolio.Length];

        Parallel.For(0, portfolio.Length, i =>
        {
            results[i] = PriceWithGreeks(portfolio[i], simulationPathsPerOption, useAntitheticVariates);
        });

        return results;
    }
}
