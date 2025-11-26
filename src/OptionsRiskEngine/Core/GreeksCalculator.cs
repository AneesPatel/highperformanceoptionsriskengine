using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using OptionsRiskEngine.Models;

namespace OptionsRiskEngine.Core;

public sealed class GreeksCalculator
{
    private const double DefaultEpsilon = 0.01;
    private const double DefaultVolatilityEpsilon = 0.0001;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateDelta(
        double priceUp,
        double priceDown,
        double epsilon)
    {
        return (priceUp - priceDown) / (2.0 * epsilon);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateGamma(
        double priceUp,
        double priceBase,
        double priceDown,
        double epsilon)
    {
        return (priceUp - 2.0 * priceBase + priceDown) / (epsilon * epsilon);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateVega(
        double priceVolUp,
        double priceVolDown,
        double epsilon)
    {
        return (priceVolUp - priceVolDown) / (2.0 * epsilon);
    }

    public static PricingResult CalculateWithGreeks(
        in OptionParameters parameters,
        int simulationPaths,
        bool useAntitheticVariates = true)
    {
        parameters.Validate();

        double spotEpsilon = parameters.SpotPrice * DefaultEpsilon;
        double volEpsilon = DefaultVolatilityEpsilon;

        double priceBase = PriceOption(parameters, simulationPaths, useAntitheticVariates, out double seBase);
        
        var paramsUp = new OptionParameters(
            parameters.SpotPrice + spotEpsilon,
            parameters.StrikePrice,
            parameters.TimeToMaturity,
            parameters.RiskFreeRate,
            parameters.Volatility);

        var paramsDown = new OptionParameters(
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

        double priceUp = PriceOption(paramsUp, simulationPaths, useAntitheticVariates, out _);
        double priceDown = PriceOption(paramsDown, simulationPaths, useAntitheticVariates, out _);
        double priceVolUp = PriceOption(paramsVolUp, simulationPaths, useAntitheticVariates, out _);
        double priceVolDown = PriceOption(paramsVolDown, simulationPaths, useAntitheticVariates, out _);

        double delta = CalculateDelta(priceUp, priceDown, spotEpsilon);
        double gamma = CalculateGamma(priceUp, priceBase, priceDown, spotEpsilon);
        double vega = CalculateVega(priceVolUp, priceVolDown, volEpsilon);

        return new PricingResult(priceBase, delta, gamma, vega, seBase);
    }

    public static PricingResult CalculateWithGreeksSimdParallel(
        in OptionParameters parameters,
        int simulationPaths,
        bool useAntitheticVariates = true)
    {
        parameters.Validate();

        double spotEpsilon = parameters.SpotPrice * DefaultEpsilon;
        double volEpsilon = DefaultVolatilityEpsilon;

        var paramsArray = new OptionParameters[5];
        paramsArray[0] = parameters;
        paramsArray[1] = new OptionParameters(
            parameters.SpotPrice + spotEpsilon,
            parameters.StrikePrice,
            parameters.TimeToMaturity,
            parameters.RiskFreeRate,
            parameters.Volatility);
        paramsArray[2] = new OptionParameters(
            parameters.SpotPrice - spotEpsilon,
            parameters.StrikePrice,
            parameters.TimeToMaturity,
            parameters.RiskFreeRate,
            parameters.Volatility);
        paramsArray[3] = new OptionParameters(
            parameters.SpotPrice,
            parameters.StrikePrice,
            parameters.TimeToMaturity,
            parameters.RiskFreeRate,
            parameters.Volatility + volEpsilon);
        paramsArray[4] = new OptionParameters(
            parameters.SpotPrice,
            parameters.StrikePrice,
            parameters.TimeToMaturity,
            parameters.RiskFreeRate,
            parameters.Volatility - volEpsilon);

        var prices = new double[5];
        var standardErrors = new double[5];

        Parallel.For(0, 5, i =>
        {
            prices[i] = PriceOption(paramsArray[i], simulationPaths, useAntitheticVariates, out standardErrors[i]);
        });

        double delta = CalculateDelta(prices[1], prices[2], spotEpsilon);
        double gamma = CalculateGamma(prices[1], prices[0], prices[2], spotEpsilon);
        double vega = CalculateVega(prices[3], prices[4], volEpsilon);

        return new PricingResult(prices[0], delta, gamma, vega, standardErrors[0]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double PriceOption(
        in OptionParameters parameters,
        int simulationPaths,
        bool useAntitheticVariates,
        out double standardError)
    {
        double drift = (parameters.RiskFreeRate - 0.5 * parameters.Volatility * parameters.Volatility) * parameters.TimeToMaturity;
        double diffusion = parameters.Volatility * Math.Sqrt(parameters.TimeToMaturity);
        double discountFactor = Math.Exp(-parameters.RiskFreeRate * parameters.TimeToMaturity);

        int actualPaths = useAntitheticVariates ? (simulationPaths / 2) * 2 : simulationPaths;

        const int maxStackSize = 8192;
        bool useStack = actualPaths <= maxStackSize;

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

            double meanPayoff = MonteCarloSimulator.CalculateMean(payoffs);
            standardError = MonteCarloSimulator.CalculateStandardError(payoffs, meanPayoff);

            return meanPayoff * discountFactor;
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

            double meanPayoff = MonteCarloSimulator.CalculateMean(payoffs);
            standardError = MonteCarloSimulator.CalculateStandardError(payoffs, meanPayoff);

            return meanPayoff * discountFactor;
        }
    }
}
