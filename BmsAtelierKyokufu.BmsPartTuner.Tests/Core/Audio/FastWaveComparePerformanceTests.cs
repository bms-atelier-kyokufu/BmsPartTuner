using System.Diagnostics;
using BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;
using Xunit.Abstractions;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Audio
{
    /// <summary>
    /// <see cref="FastWaveCompare"/> の実行パフォーマンスおよび性能退化を検証。
    /// </summary>
    /// <param name="output">テスト実行時の診断情報を出力するヘルパー。</param>
    public class FastWaveComparePerformanceTests(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;

        /// <summary>
        /// <see cref="FastWaveCompare.IsMatch"/> の波形比較処理能力のベンチマークテストを行い、性能退化を検証。
        /// </summary>
        /// <remarks>
        /// 実行環境（CPUクロック、負荷状況）の違いによる影響を排除するため、特定の負荷処理（平方根計算）を基準とした相対実行時間比率（Ratio）を用いてアサーションを実行。
        /// </remarks>
        [Fact]
        [Trait("Category", "Benchmark")]
        public void Benchmark_FastWaveCompare_IsMatch()
        {
            // 1. テストデータの生成
            const int length = 44100; // 1秒分
            float[] data1 = new float[length];
            float[] data2 = new float[length];
            for (int i = 0; i < length; i++)
            {
                data1[i] = (float)Math.Sin(i * 0.05);
                data2[i] = (float)Math.Sin((i * 0.05) + 0.01);
            }

            // MockCachedSoundDataのコンストラクタでfilePathを空文字にすることで、canCacheがfalseとなりキャッシュを無効化する。
            // これにより毎回実際の相関計算が行われる。
            using var sound1 = new MockCachedSoundData([data1], 44100, 16, "");
            using var sound2 = new MockCachedSoundData([data2], 44100, 16, "");

            // ウォームアップ（JITコンパイルと最適化を促す）
            for (int i = 0; i < 1000; i++)
            {
                FastWaveCompare.IsMatch(sound1, sound2, 0.99f);
            }

            // 2. キャリブレーション（CPUクロックや実行環境の差異を吸収するための共通負荷）
            var calibSw = Stopwatch.StartNew();
            double calibSum = 0;
            for (int i = 0; i < 5_000_000; i++)
            {
                calibSum += Math.Sqrt(i);
            }
            calibSw.Stop();
            long calibMs = Math.Max(1, calibSw.ElapsedMilliseconds);

            // 3. ベンチマーク実行
            var benchSw = Stopwatch.StartNew();
            const int iterations = 2000;
            bool lastResult = false;
            for (int i = 0; i < iterations; i++)
            {
                lastResult = FastWaveCompare.IsMatch(sound1, sound2, 0.99f);
            }
            benchSw.Stop();
            long benchMs = benchSw.ElapsedMilliseconds;

            double actualRatio = (double)benchMs / calibMs;

            // 期待値の基準レシオ（事前の実測値から定義）
            // 処理向上やハードの違いによる乖離を防ぐため、相対的な実行比率で判定
            const double BaselineRatio = 100.0;//理想は62ぐらい

            _output.WriteLine($"[Perf] Calibration Time: {calibMs} ms (Sum: {calibSum})");
            _output.WriteLine($"[Perf] Benchmark Time: {benchMs} ms for {iterations} iterations");
            _output.WriteLine($"[Perf] IsMatch Result: {lastResult}");
            _output.WriteLine($"[Perf] Actual Ratio (Bench / Calib): {actualRatio:F4}");
            _output.WriteLine($"[Perf] Baseline Ratio: {BaselineRatio:F4}");

            if (actualRatio > BaselineRatio * 1.10)
            {
                Assert.Fail($"Performance regression detected! Actual ratio: {actualRatio:F4}, Baseline ratio: {BaselineRatio:F4} (exceeded by more than 10%)");
            }
            else if (actualRatio > BaselineRatio * 1.05)
            {
                _output.WriteLine($"[WARNING] Performance regression detected! Actual ratio: {actualRatio:F4}, Baseline ratio: {BaselineRatio:F4} (exceeded by more than 5%)");
            }
        }
    }
}
