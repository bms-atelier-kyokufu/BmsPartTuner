namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Audio
{
    /// <summary>
    /// <see cref="WaveValidationEdgeCasesTests"/> の動作を検証するテストクラス。
    /// </summary>
    public class WaveValidationEdgeCasesTests
    {
        private const float Tolerance = 0.001f;

        #region Helper Methods

        private static float[] GenerateSineWave(int samples, float frequency = 1f, float amplitude = 1f, float phase = 0f)
        {
            var result = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                result[i] = amplitude * (float)Math.Sin((2 * Math.PI * frequency * i / samples) + phase);
            }
            return result;
        }

        private static void RunPearsonTest(float[] wav1, float[] wav2, Action<float> assertCorrelation)
        {
            var correlation = WaveValidation.CalculatePearsonCorrelation(wav1, wav2);
            assertCorrelation(correlation);
        }

        private static void RunPearsonSimdTest(float[] wav1, float[] wav2, Action<float> assertCorrelation)
        {
            var correlation = WaveValidation.CalculatePearsonCorrelationSIMD(wav1, wav2);
            assertCorrelation(correlation);
        }

        #endregion

        #region Edge Cases - エッジケース

        /// <summary>
        /// CalculatePearsonCorrelation において、条件 VerySmallValues の場合に HandlesCorrectly されることを検証します。
        /// </summary>
        [Fact]
        public void CalculatePearsonCorrelation_VerySmallValues_HandlesCorrectly() =>
            RunPearsonTest(
                [.. Enumerable.Range(0, 100).Select(i => (float)(i * 1e-6))],
                [.. Enumerable.Range(0, 100).Select(i => (float)(i * 1e-6))],
                correlation => Assert.Equal(1.0f, correlation, Tolerance));

        /// <summary>
        /// CalculatePearsonCorrelation において、条件 SingleElement の場合に ReturnsZero されることを検証します。
        /// </summary>
        [Fact]
        public void CalculatePearsonCorrelation_SingleElement_ReturnsZero() =>
            RunPearsonTest([1.0f], [2.0f], correlation => Assert.Equal(0.0f, correlation));

        /// <summary>
        /// CalculatePearsonCorrelation において、条件 TwoElements の場合に CalculatesCorrectly されることを検証します。
        /// </summary>
        [Fact]
        public void CalculatePearsonCorrelation_TwoElements_CalculatesCorrectly() =>
            RunPearsonTest([0f, 1f], [0f, 1f], correlation => Assert.Equal(1.0f, correlation, Tolerance));

        /// <summary>
        /// ProcessRemainderPearson において、条件 WithSpecificRemainder の場合に CalculatesCorrectly されることを検証します。
        /// </summary>
        [Fact]
        public void ProcessRemainderPearson_WithSpecificRemainder_CalculatesCorrectly() =>
            RunPearsonSimdTest(
                [.. Enumerable.Range(0, 17).Select(i => (float)i)],
                [.. Enumerable.Range(0, 17).Select(i => (float)i)],
                correlation => Assert.Equal(1.0f, correlation, Tolerance));

        /// <summary>
        /// CalculatePearsonCorrelationSIMD において、条件 WithZeroAmplitude の場合に ShouldReturnZero されることを検証します。
        /// </summary>
        [Fact]
        public void CalculatePearsonCorrelationSIMD_WithZeroAmplitude_ShouldReturnZero() =>
            RunPearsonSimdTest(new float[100], GenerateSineWave(100), correlation => Assert.Equal(0.0f, correlation));

        /// <summary>
        /// CalculatePearsonCorrelationSIMD において、条件 WithDifferentLengths の場合に ShouldReturnZero されることを検証します。
        /// </summary>
        [Fact]
        public void CalculatePearsonCorrelationSIMD_WithDifferentLengths_ShouldReturnZero() =>
            RunPearsonSimdTest(GenerateSineWave(100), GenerateSineWave(101), correlation => Assert.Equal(0.0f, correlation));

        #endregion

        #region Noise Robustness Tests - ノイズ耐性

        /// <summary>
        /// CalculatePearsonCorrelation において、条件 WithNoise の場合に MaintainsApproximateCorrelation されることを検証します。
        /// </summary>
        [Theory]
        [InlineData(0.001f, 0.99f)]   // 微小ノイズ → 高相関
        [InlineData(0.01f, 0.95f)]    // 小ノイズ → 高相関
        [InlineData(0.1f, 0.7f)]      // 中ノイズ → 中相関
        public void CalculatePearsonCorrelation_WithNoise_MaintainsApproximateCorrelation(
            float noiseLevel, float minExpectedCorrelation)
        {
            var random = new Random(42);
            var original = GenerateSineWave(1000);
            var noisy = original.Select(x => x + (noiseLevel * (float)((random.NextDouble() * 2) - 1))).ToArray();

            RunPearsonTest(original, noisy, correlation =>
                Assert.True(correlation >= minExpectedCorrelation,
                    $"Expected correlation >= {minExpectedCorrelation}, got {correlation}"));
        }

        #endregion
    }
}

