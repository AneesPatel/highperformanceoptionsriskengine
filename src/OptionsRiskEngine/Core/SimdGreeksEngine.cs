using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using OptionsRiskEngine.Models;

namespace OptionsRiskEngine.Core;

public sealed class SimdGreeksEngine
{
    private const double DefaultEpsilon = 0.01;
    private const double DefaultVolatilityEpsilon = 0.0001;
    private const int MaxStackSize = 8192;

    public static PricingResult CalculateWithAvx2Greeks(
        in OptionParameters parameters,
        int simulationPaths,
        bool useAntitheticVariates = true)
    {
        parameters.Validate();

        if (!Avx2.IsSupported)
        {
            return GreeksCalculator.CalculateWithGreeks(parameters, simulationPaths, useAntitheticVariates);
        }

        double spotEpsilon = parameters.SpotPrice * DefaultEpsilon;
        double volEpsilon = DefaultVolatilityEpsilon;

        int actualPaths = useAntitheticVariates ? (simulationPaths / 2) * 2 : simulationPaths;
        bool useStack = actualPaths <= MaxStackSize;

        if (useStack)
        {
            return CalculateGreeksWithStackAlloc(parameters, actualPaths, useAntitheticVariates, 
                spotEpsilon, volEpsilon);
        }
        else
        {
            return CalculateGreeksWithHeapAlloc(parameters, actualPaths, useAntitheticVariates,
                spotEpsilon, volEpsilon);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PricingResult CalculateGreeksWithStackAlloc(
        in OptionParameters parameters,
        int actualPaths,
        bool useAntitheticVariates,
        double spotEpsilon,
        double volEpsilon)
    {
        Span<double> basePayoffs = stackalloc double[actualPaths];
        Span<double> upPayoffs = stackalloc double[actualPaths];
        Span<double> downPayoffs = stackalloc double[actualPaths];
        Span<double> volUpPayoffs = stackalloc double[actualPaths];
        Span<double> volDownPayoffs = stackalloc double[actualPaths];

        SimulateAllScenariosAvx2(
            parameters, actualPaths, useAntitheticVariates,
            spotEpsilon, volEpsilon,
            basePayoffs, upPayoffs, downPayoffs, volUpPayoffs, volDownPayoffs);

        return ComputeGreeksFromPayoffs(basePayoffs, upPayoffs, downPayoffs, volUpPayoffs, volDownPayoffs,
            parameters.RiskFreeRate, parameters.TimeToMaturity, spotEpsilon, volEpsilon);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PricingResult CalculateGreeksWithHeapAlloc(
        in OptionParameters parameters,
        int actualPaths,
        bool useAntitheticVariates,
        double spotEpsilon,
        double volEpsilon)
    {
        double[] basePayoffs = new double[actualPaths];
        double[] upPayoffs = new double[actualPaths];
        double[] downPayoffs = new double[actualPaths];
        double[] volUpPayoffs = new double[actualPaths];
        double[] volDownPayoffs = new double[actualPaths];

        SimulateAllScenariosAvx2(
            parameters, actualPaths, useAntitheticVariates,
            spotEpsilon, volEpsilon,
            basePayoffs, upPayoffs, downPayoffs, volUpPayoffs, volDownPayoffs);

        return ComputeGreeksFromPayoffs(basePayoffs, upPayoffs, downPayoffs, volUpPayoffs, volDownPayoffs,
            parameters.RiskFreeRate, parameters.TimeToMaturity, spotEpsilon, volEpsilon);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void SimulateAllScenariosAvx2(
        in OptionParameters parameters,
        int actualPaths,
        bool useAntitheticVariates,
        double spotEpsilon,
        double volEpsilon,
        Span<double> basePayoffs,
        Span<double> upPayoffs,
        Span<double> downPayoffs,
        Span<double> volUpPayoffs,
        Span<double> volDownPayoffs)
    {
        double drift = (parameters.RiskFreeRate - 0.5 * parameters.Volatility * parameters.Volatility) * parameters.TimeToMaturity;
        double diffusion = parameters.Volatility * Math.Sqrt(parameters.TimeToMaturity);

        double[] randomSamples = new double[actualPaths];
        
        if (useAntitheticVariates)
        {
            RandomGenerator.FillAntitheticPairs(randomSamples);
        }
        else
        {
            RandomGenerator.FillGaussianSamples(randomSamples);
        }

        if (Avx2.IsSupported)
        {
            SimulateWithAvx2Intrinsics(
                parameters.SpotPrice, parameters.SpotPrice + spotEpsilon, parameters.SpotPrice - spotEpsilon,
                drift, diffusion,
                parameters.Volatility + volEpsilon, parameters.Volatility - volEpsilon,
                parameters.TimeToMaturity, parameters.RiskFreeRate,
                parameters.StrikePrice,
                randomSamples,
                basePayoffs, upPayoffs, downPayoffs, volUpPayoffs, volDownPayoffs);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void SimulateWithAvx2Intrinsics(
        double spotBase, double spotUp, double spotDown,
        double driftBase, double diffusionBase,
        double volUp, double volDown,
        double timeToMaturity, double riskFreeRate,
        double strikePrice,
        Span<double> randomSamples,
        Span<double> basePayoffs,
        Span<double> upPayoffs,
        Span<double> downPayoffs,
        Span<double> volUpPayoffs,
        Span<double> volDownPayoffs)
    {
        double driftVolUp = (riskFreeRate - 0.5 * volUp * volUp) * timeToMaturity;
        double diffusionVolUp = volUp * Math.Sqrt(timeToMaturity);
        double driftVolDown = (riskFreeRate - 0.5 * volDown * volDown) * timeToMaturity;
        double diffusionVolDown = volDown * Math.Sqrt(timeToMaturity);

        int avxVectorSize = Vector256<double>.Count;
        int vectorLength = randomSamples.Length - (randomSamples.Length % avxVectorSize);

        var spotBaseVec = Vector256.Create(spotBase);
        var spotUpVec = Vector256.Create(spotUp);
        var spotDownVec = Vector256.Create(spotDown);
        var strikeVec = Vector256.Create(strikePrice);
        var zeroVec = Vector256<double>.Zero;

        var driftBaseVec = Vector256.Create(driftBase);
        var diffusionBaseVec = Vector256.Create(diffusionBase);
        var driftVolUpVec = Vector256.Create(driftVolUp);
        var diffusionVolUpVec = Vector256.Create(diffusionVolUp);
        var driftVolDownVec = Vector256.Create(driftVolDown);
        var diffusionVolDownVec = Vector256.Create(diffusionVolDown);

        // Allocate temporary buffers outside the loop to avoid stack overflow
        double* expBase = stackalloc double[avxVectorSize];
        double* expUp = stackalloc double[avxVectorSize];
        double* expDown = stackalloc double[avxVectorSize];
        double* expVolUp = stackalloc double[avxVectorSize];
        double* expVolDown = stackalloc double[avxVectorSize];

        fixed (double* pRandom = randomSamples)
        fixed (double* pBase = basePayoffs)
        fixed (double* pUp = upPayoffs)
        fixed (double* pDown = downPayoffs)
        fixed (double* pVolUp = volUpPayoffs)
        fixed (double* pVolDown = volDownPayoffs)
        {
            for (int i = 0; i < vectorLength; i += avxVectorSize)
            {
                var randVec = Avx.LoadVector256(pRandom + i);

                var exponentBase = Avx.Add(driftBaseVec, Avx.Multiply(diffusionBaseVec, randVec));
                var exponentUp = Avx.Add(driftBaseVec, Avx.Multiply(diffusionBaseVec, randVec));
                var exponentDown = Avx.Add(driftBaseVec, Avx.Multiply(diffusionBaseVec, randVec));
                var exponentVolUp = Avx.Add(driftVolUpVec, Avx.Multiply(diffusionVolUpVec, randVec));
                var exponentVolDown = Avx.Add(driftVolDownVec, Avx.Multiply(diffusionVolDownVec, randVec));

                // Store to temporary arrays for Math.Exp
                Avx.Store(expBase, exponentBase);
                Avx.Store(expUp, exponentUp);
                Avx.Store(expDown, exponentDown);
                Avx.Store(expVolUp, exponentVolUp);
                Avx.Store(expVolDown, exponentVolDown);

                // Apply Math.Exp
                for (int j = 0; j < avxVectorSize; j++)
                {
                    expBase[j] = Math.Exp(expBase[j]);
                    expUp[j] = Math.Exp(expUp[j]);
                    expDown[j] = Math.Exp(expDown[j]);
                    expVolUp[j] = Math.Exp(expVolUp[j]);
                    expVolDown[j] = Math.Exp(expVolDown[j]);
                }

                // Calculate final prices
                var priceBase = Avx.Multiply(spotBaseVec, Avx.LoadVector256(expBase));
                var priceUp = Avx.Multiply(spotUpVec, Avx.LoadVector256(expUp));
                var priceDown = Avx.Multiply(spotDownVec, Avx.LoadVector256(expDown));
                var priceVolUp = Avx.Multiply(spotBaseVec, Avx.LoadVector256(expVolUp));
                var priceVolDown = Avx.Multiply(spotBaseVec, Avx.LoadVector256(expVolDown));

                // Calculate payoffs
                var payoffBase = Avx.Max(Avx.Subtract(priceBase, strikeVec), zeroVec);
                var payoffUp = Avx.Max(Avx.Subtract(priceUp, strikeVec), zeroVec);
                var payoffDown = Avx.Max(Avx.Subtract(priceDown, strikeVec), zeroVec);
                var payoffVolUp = Avx.Max(Avx.Subtract(priceVolUp, strikeVec), zeroVec);
                var payoffVolDown = Avx.Max(Avx.Subtract(priceVolDown, strikeVec), zeroVec);

                Avx.Store(pBase + i, payoffBase);
                Avx.Store(pUp + i, payoffUp);
                Avx.Store(pDown + i, payoffDown);
                Avx.Store(pVolUp + i, payoffVolUp);
                Avx.Store(pVolDown + i, payoffVolDown);
            }
        }

        for (int i = vectorLength; i < randomSamples.Length; i++)
        {
            double z = randomSamples[i];

            double priceBase = spotBase * Math.Exp(driftBase + diffusionBase * z);
            double priceUp = spotUp * Math.Exp(driftBase + diffusionBase * z);
            double priceDown = spotDown * Math.Exp(driftBase + diffusionBase * z);
            double priceVolUp = spotBase * Math.Exp(driftVolUp + diffusionVolUp * z);
            double priceVolDown = spotBase * Math.Exp(driftVolDown + diffusionVolDown * z);

            basePayoffs[i] = Math.Max(priceBase - strikePrice, 0.0);
            upPayoffs[i] = Math.Max(priceUp - strikePrice, 0.0);
            downPayoffs[i] = Math.Max(priceDown - strikePrice, 0.0);
            volUpPayoffs[i] = Math.Max(priceVolUp - strikePrice, 0.0);
            volDownPayoffs[i] = Math.Max(priceVolDown - strikePrice, 0.0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PricingResult ComputeGreeksFromPayoffs(
        ReadOnlySpan<double> basePayoffs,
        ReadOnlySpan<double> upPayoffs,
        ReadOnlySpan<double> downPayoffs,
        ReadOnlySpan<double> volUpPayoffs,
        ReadOnlySpan<double> volDownPayoffs,
        double riskFreeRate,
        double timeToMaturity,
        double spotEpsilon,
        double volEpsilon)
    {
        double discountFactor = Math.Exp(-riskFreeRate * timeToMaturity);

        double meanBase = MonteCarloSimulator.CalculateMean(basePayoffs) * discountFactor;
        double meanUp = MonteCarloSimulator.CalculateMean(upPayoffs) * discountFactor;
        double meanDown = MonteCarloSimulator.CalculateMean(downPayoffs) * discountFactor;
        double meanVolUp = MonteCarloSimulator.CalculateMean(volUpPayoffs) * discountFactor;
        double meanVolDown = MonteCarloSimulator.CalculateMean(volDownPayoffs) * discountFactor;

        double se = MonteCarloSimulator.CalculateStandardError(basePayoffs, MonteCarloSimulator.CalculateMean(basePayoffs)) * discountFactor;

        double delta = (meanUp - meanDown) / (2.0 * spotEpsilon);
        double gamma = (meanUp - 2.0 * meanBase + meanDown) / (spotEpsilon * spotEpsilon);
        double vega = (meanVolUp - meanVolDown) / (2.0 * volEpsilon);

        return new PricingResult(meanBase, delta, gamma, vega, se);
    }
}
