using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Scenarios
{
    /// <summary>
    /// シミュレーションエンジンの統合テスト（インメモリ完結版）。
    /// ファイルI/Oを使用せず、メモリ内で音声データを生成してテストを実行します。
    /// </summary>
    public class OptimizationScenarioTests
    {
        #region Test Helpers (In-Memory Audio Data Factory)

        private static float CalculateRms(float[] samples)
        {
            double sum = 0;
            foreach (var s in samples) sum += s * s;
            return (float)Math.Sqrt(sum / samples.Length);
        }

        private static float[] NormalizeToRms(float[] samples, float targetRms)
        {
            float currentRms = CalculateRms(samples);
            if (currentRms == 0) return samples;
            float scale = targetRms / currentRms;
            return samples.Select(s => s * scale).ToArray();
        }

        /// <summary>
        /// メモリ内音声データを生成するヘルパーメソッド。
        /// 実際の.wavファイルを読み込まずにテストを実行します。
        /// </summary>
        private static CachedSoundData CreateMockAudioData(float[] samples)
        {
            float[][] channels = new float[1][];
            channels[0] = samples;
            return new CachedSoundData(channels, 44100, 16);
        }

        #endregion

        [Fact]
        public void RunParallelSimulation_IdenticalAndDifferentFiles_GroupsCorrectly()
        {
            // Arrange: インメモリでサンプル音声データを生成
            const int sampleCount = 1000;
            const float targetRms = 0.5f;

            // ファイルA: ベースとなるサイン波
            var samplesA = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samplesA[i] = (float)Math.Sin(i * 0.1);
            samplesA = NormalizeToRms(samplesA, targetRms);

            // ファイルB: Aと完全に同一
            var samplesB = samplesA.ToArray();

            // ファイルC: Aに微小ノイズを追加（相関係数が0.90〜0.99の範囲に収まるように調整）
            var samplesC = samplesA.ToArray();
            var rand = new Random(123);
            for (int i = 0; i < sampleCount; i++)
                samplesC[i] += (float)(rand.NextDouble() * 0.2 - 0.1);
            samplesC = NormalizeToRms(samplesC, targetRms);

            // ファイルD: 全く異なる波形（コサイン波）
            var samplesD = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samplesD[i] = (float)Math.Cos(i * 0.2);
            samplesD = NormalizeToRms(samplesD, targetRms);

            var fileList = new List<WavFiles>
            {
                new WavFiles { NumInteger = 1, Name = "A.wav", CachedData = CreateMockAudioData(samplesA) },
                new WavFiles { NumInteger = 2, Name = "B.wav", CachedData = CreateMockAudioData(samplesB) },
                new WavFiles { NumInteger = 3, Name = "C.wav", CachedData = CreateMockAudioData(samplesC) },
                new WavFiles { NumInteger = 4, Name = "D.wav", CachedData = CreateMockAudioData(samplesD) }
            };

            var engine = new SimulationEngine(fileList, 1, 4);

            // Act: しきい値0.90〜1.0の範囲でシミュレーション実行
            var results = engine.RunParallelSimulationDetailed(0.90f, 1.0f, 0.01f, null);

            // Assert: しきい値0.99の場合
            // A=Bは確実にマージされるが、Cはノイズ次第で2〜3ファイルに収束
            var res99 = results.FirstOrDefault(r => Math.Abs(r.Threshold - 0.99f) < 0.001);
            Assert.NotNull(res99);
            Assert.InRange(res99.FileCount, 2, 3);

            // Assert: しきい値0.90の場合
            // A=B=Cはマージされ、Dは別グループ。合計2ファイル
            var res90 = results.FirstOrDefault(r => Math.Abs(r.Threshold - 0.90f) < 0.001);
            Assert.NotNull(res90);
            Assert.Equal(2, res90.FileCount);
        }
    }
}
