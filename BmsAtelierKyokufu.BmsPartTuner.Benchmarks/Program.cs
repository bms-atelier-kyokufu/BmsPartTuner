using BenchmarkDotNet.Running;

namespace BmsAtelierKyokufu.BmsPartTuner.Benchmarks;

public static class Program
{
    public static void Main()
    {
        _ = BenchmarkRunner.Run<BmsonOptimizationBenchmark>();
    }
}
