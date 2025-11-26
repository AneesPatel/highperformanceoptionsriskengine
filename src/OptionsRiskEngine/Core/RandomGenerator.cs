using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OptionsRiskEngine.Core;

public static class RandomGenerator
{
    [ThreadStatic]
    private static Random? threadLocalRandom;

    private static Random GetThreadRandom()
    {
        return threadLocalRandom ??= new Random(Guid.NewGuid().GetHashCode());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillGaussianSamples(Span<double> samples)
    {
        var rng = GetThreadRandom();
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = BoxMullerTransform(rng);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillAntitheticPairs(Span<double> samples)
    {
        var rng = GetThreadRandom();
        int halfLength = samples.Length / 2;
        
        for (int i = 0; i < halfLength; i++)
        {
            double z = BoxMullerTransform(rng);
            samples[i] = z;
            samples[halfLength + i] = -z;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double BoxMullerTransform(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillGaussianSamplesVectorized(Span<double> samples)
    {
        var rng = GetThreadRandom();
        int vectorSize = Vector<double>.Count;
        int vectorLength = samples.Length - (samples.Length % vectorSize);

        double[] temp = new double[vectorSize];

        for (int i = 0; i < vectorLength; i += vectorSize)
        {
            for (int j = 0; j < vectorSize; j++)
            {
                temp[j] = BoxMullerTransform(rng);
            }
            
            var vec = new Vector<double>(temp);
            vec.CopyTo(samples.Slice(i, vectorSize));
        }

        for (int i = vectorLength; i < samples.Length; i++)
        {
            samples[i] = BoxMullerTransform(rng);
        }
    }
}
