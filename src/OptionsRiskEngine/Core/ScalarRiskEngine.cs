using System.Runtime.CompilerServices;
using OptionsRiskEngine.Models;

namespace OptionsRiskEngine.Core;

public sealed class ScalarRiskEngine
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PricingResult PriceWithGreeksScalar(
        in OptionParameters parameters,
        int simulationPaths,
        bool useAntitheticVariates = true)
    {
        parameters.Validate();

        double spotEpsilon = parameters.SpotPrice * 0.01;
        double volEpsilon = 0.0001;

        double priceBase = PriceOptionScalar(parameters, simulationPaths, useAntitheticVariates, out double seBase);

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

        double priceUp = PriceOptionScalar(paramsUp, simulationPaths, useAntitheticVariates, out _);
        double priceDown = PriceOptionScalar(paramsDown, simulationPaths, useAntitheticVariates, out _);
        double priceVolUp = PriceOptionScalar(paramsVolUp, simulationPaths, useAntitheticVariates, out _);
        double priceVolDown = PriceOptionScalar(paramsVolDown, simulationPaths, useAntitheticVariates, out _);

        double delta = (priceUp - priceDown) / (2.0 * spotEpsilon);
        double gamma = (priceUp - 2.0 * priceBase + priceDown) / (spotEpsilon * spotEpsilon);
        double vega = (priceVolUp - priceVolDown) / (2.0 * volEpsilon);

        return new PricingResult(priceBase, delta, gamma, vega, seBase);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double PriceOptionScalar(
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

        double sum = 0.0;
        double sumSquared = 0.0;

        if (useStack)
        {
            Span<double> randomSamples = stackalloc double[actualPaths];
            
            if (useAntitheticVariates)
            {
                RandomGenerator.FillAntitheticPairs(randomSamples);
            }
            else
            {
                RandomGenerator.FillGaussianSamples(randomSamples);
            }

            for (int i = 0; i < actualPaths; i++)
            {
                double finalPrice = MonteCarloSimulator.SimulatePathScalar(
                    parameters.SpotPrice,
                    drift,
                    diffusion,
                    randomSamples[i]);

                double payoff = MonteCarloSimulator.CalculatePayoff(finalPrice, parameters.StrikePrice);
                double discountedPayoff = payoff * discountFactor;

                sum += discountedPayoff;
                sumSquared += discountedPayoff * discountedPayoff;
            }
        }
        else
        {
            double[] randomSamples = new double[actualPaths];
            
            if (useAntitheticVariates)
            {
                RandomGenerator.FillAntitheticPairs(randomSamples);
            }
            else
            {
                RandomGenerator.FillGaussianSamples(randomSamples);
            }

            for (int i = 0; i < actualPaths; i++)
            {
                double finalPrice = MonteCarloSimulator.SimulatePathScalar(
                    parameters.SpotPrice,
                    drift,
                    diffusion,
                    randomSamples[i]);

                double payoff = MonteCarloSimulator.CalculatePayoff(finalPrice, parameters.StrikePrice);
                double discountedPayoff = payoff * discountFactor;

                sum += discountedPayoff;
                sumSquared += discountedPayoff * discountedPayoff;
            }
        }

        double mean = sum / actualPaths;
        double variance = (sumSquared / actualPaths) - (mean * mean);
        standardError = Math.Sqrt(variance / actualPaths);

        return mean;
    }
}
