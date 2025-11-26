using BenchmarkDotNet.Running;

namespace OptionsRiskEngine.Benchmarks;

internal class Program
{
    static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<RiskEngineBenchmarks>();
    }
}
