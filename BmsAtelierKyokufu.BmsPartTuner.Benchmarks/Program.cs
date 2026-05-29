using BenchmarkDotNet.Running;

namespace BmsAtelierKyokufu.BmsPartTuner.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        _ = BenchmarkRunner.Run<BmsonOptimizationBenchmark>();
    }
}
