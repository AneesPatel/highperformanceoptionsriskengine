using System.Numerics;
using System.Runtime.CompilerServices;
using OptionsRiskEngine.Models;

namespace OptionsRiskEngine.Core;

public static class MonteCarloSimulator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SimulatePathScalar(
        double spotPrice,
        double drift,
        double diffusion,
        double randomSample)
    {
        return spotPrice * Math.Exp(drift + diffusion * randomSample);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculatePayoff(double spotAtMaturity, double strikePrice)
    {
        return Math.Max(spotAtMaturity - strikePrice, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SimulatePathsVectorized(
        ReadOnlySpan<double> randomSamples,
        Span<double> finalPrices,
        double spotPrice,
        double drift,
        double diffusion)
    {
        int vectorSize = Vector<double>.Count;
        int vectorLength = randomSamples.Length - (randomSamples.Length % vectorSize);

        var spotVec = new Vector<double>(spotPrice);
        var driftVec = new Vector<double>(drift);
        var diffusionVec = new Vector<double>(diffusion);

        double[] expValues = new double[vectorSize];

        for (int i = 0; i < vectorLength; i += vectorSize)
        {
            var randVec = new Vector<double>(randomSamples.Slice(i, vectorSize));
            var exponent = driftVec + diffusionVec * randVec;
            
            exponent.CopyTo(expValues);
            
            for (int j = 0; j < vectorSize; j++)
            {
                expValues[j] = Math.Exp(expValues[j]);
            }
            
            var expVec = new Vector<double>(expValues);
            var resultVec = spotVec * expVec;
            resultVec.CopyTo(finalPrices.Slice(i, vectorSize));
        }

        for (int i = vectorLength; i < randomSamples.Length; i++)
        {
            finalPrices[i] = SimulatePathScalar(spotPrice, drift, diffusion, randomSamples[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CalculatePayoffsVectorized(
        ReadOnlySpan<double> finalPrices,
        Span<double> payoffs,
        double strikePrice)
    {
        int vectorSize = Vector<double>.Count;
        int vectorLength = finalPrices.Length - (finalPrices.Length % vectorSize);

        var strikeVec = new Vector<double>(strikePrice);
        var zeroVec = Vector<double>.Zero;

        for (int i = 0; i < vectorLength; i += vectorSize)
        {
            var priceVec = new Vector<double>(finalPrices.Slice(i, vectorSize));
            var diff = priceVec - strikeVec;
            var payoffVec = Vector.Max(diff, zeroVec);
            payoffVec.CopyTo(payoffs.Slice(i, vectorSize));
        }

        for (int i = vectorLength; i < finalPrices.Length; i++)
        {
            payoffs[i] = CalculatePayoff(finalPrices[i], strikePrice);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateMean(ReadOnlySpan<double> values)
    {
        double sum = 0.0;
        int vectorSize = Vector<double>.Count;
        int vectorLength = values.Length - (values.Length % vectorSize);

        var sumVec = Vector<double>.Zero;

        for (int i = 0; i < vectorLength; i += vectorSize)
        {
            var vec = new Vector<double>(values.Slice(i, vectorSize));
            sumVec += vec;
        }

        Span<double> sumArray = stackalloc double[vectorSize];
        sumVec.CopyTo(sumArray);
        for (int i = 0; i < vectorSize; i++)
        {
            sum += sumArray[i];
        }

        for (int i = vectorLength; i < values.Length; i++)
        {
            sum += values[i];
        }

        return sum / values.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateStandardError(ReadOnlySpan<double> values, double mean)
    {
        double sumSquaredDiff = 0.0;
        int vectorSize = Vector<double>.Count;
        int vectorLength = values.Length - (values.Length % vectorSize);

        var meanVec = new Vector<double>(mean);
        var sumVec = Vector<double>.Zero;

        for (int i = 0; i < vectorLength; i += vectorSize)
        {
            var vec = new Vector<double>(values.Slice(i, vectorSize));
            var diff = vec - meanVec;
            sumVec += diff * diff;
        }

        Span<double> sumArray = stackalloc double[vectorSize];
        sumVec.CopyTo(sumArray);
        for (int i = 0; i < vectorSize; i++)
        {
            sumSquaredDiff += sumArray[i];
        }

        for (int i = vectorLength; i < values.Length; i++)
        {
            double diff = values[i] - mean;
            sumSquaredDiff += diff * diff;
        }

        double variance = sumSquaredDiff / (values.Length - 1);
        return Math.Sqrt(variance / values.Length);
    }
}
