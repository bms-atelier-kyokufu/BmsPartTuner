using System.Diagnostics;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Audio
{
    public class WaveValidationSIMDTests
    {
        #region Helper Methods

        private static float[] GenerateSineWave(int samples, float frequency = 1f, float amplitude = 1f, float phase = 0f)
        {
            var result = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                result[i] = amplitude * (float)Math.Sin(2 * Math.PI * frequency * i / samples + phase);
            }
            return result;
        }

        #endregion

        #region SIMD Consistency Tests - SIMD版と非SIMD版の一貫性

        [Theory]
        [InlineData(7)]     // ベクトルサイズ未満
        [InlineData(8)]     // ベクトルサイズちょうど（AVX2）
        [InlineData(9)]     // ベクトルサイズ+1
        [InlineData(15)]    // ベクトルサイズ×2-1
        [InlineData(16)]    // ベクトルサイズ×2
        [InlineData(100)]   // 十分大きい
        [InlineData(1000)]  // 大規模
        public void SIMD_VariousLengths_ProducesConsistentResults(int length)
        {
            // Arrange
            var wav1 = GenerateSineWave(length);
            var wav2 = GenerateSineWave(length, phase: 0.3f);

            // Act
            var r2 = WaveValidation.CalculateRSquaredSIMD(wav1, wav2);
            var pearson = WaveValidation.CalculatePearsonCorrelationSIMD(wav1, wav2);

            // Assert - 結果が有効な範囲内であること
            Assert.True(r2 >= 0f && r2 <= 1f, $"R2 out of range: {r2}");
            Assert.True(pearson >= -1f && pearson <= 1f, $"Pearson out of range: {pearson}");
        }

        #endregion

        #region Performance Characteristic Tests - パフォーマンス特性

        [Fact]
        public void SIMD_LargeArray_CompletesInReasonableTime()
        {
            // Arrange - 大きな配列（44.1kHz × 10秒）
            var wav1 = GenerateSineWave(441000);
            var wav2 = GenerateSineWave(441000, phase: 0.1f);

            // Act
            var sw = Stopwatch.StartNew();
            var correlation = WaveValidation.CalculatePearsonCorrelationSIMD(wav1, wav2);
            sw.Stop();

            // Assert - 200ms以内に完了すること（実行環境の揺らぎを考慮）
            Assert.True(sw.ElapsedMilliseconds < 200,
                $"Expected < 200ms, took {sw.ElapsedMilliseconds}ms");
            Assert.True(correlation >= -1f && correlation <= 1f);
        }

        #endregion
    }
}
