High-Performance Monte Carlo Options Risk Engine
Scalar Baseline: 98.73 ms → 10.1M paths/second
AVX2 SIMD: 49.99 ms → 20.0M paths/second (1.98x speedup)
Parallel SIMD (16c): ~35 ms → 28.6M paths/second (2.82x speedup)
Zero GC Collections: confirmed (500K paths across 100 iterations)
Stack Allocation: up to 8,192 elements (65KB)
Deterministic Latency: No GC pauses during pricing


- **Platform:** .NET 8.0 (C# 12)
- **SIMD:** AVX2 (`System.Runtime.Intrinsics.X86`)
- **Memory:** `stackalloc`, `Span<T>`, readonly structs
- **Parallelization:** `Parallel.For` with `Partitioner`
- **Testing:** xUnit + FluentAssertions (54 unit tests)
- **Benchmarking:** BenchmarkDotNet

using OptionsRiskEngine.Core;

var perfMetrics = PerformanceMetrics.MeasurePerformanceImprovement(
    parameters, 
    simulationPaths: 1_000_000, 
    useAntitheticVariates: true
);
