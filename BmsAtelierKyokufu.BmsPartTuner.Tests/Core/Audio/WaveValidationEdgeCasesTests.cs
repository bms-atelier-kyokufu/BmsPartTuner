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

        public static TheoryData<float[], float[], float, bool, string> GetEdgeCaseTestData()
        {
            var data = new TheoryData<float[], float[], float, bool, string>();

            // VerySmallValues (微小値の入力)
            var verySmall = Enumerable.Range(0, 100).Select(i => (float)(i * 1e-6)).ToArray();
            data.Add(verySmall, verySmall, 1.0f, true, "VerySmallValues");

            // SingleElement (要素数が1つのみ)
            data.Add([1.0f], [2.0f], 0.0f, true, "SingleElement");

            // TwoElements (要素数が2つ)
            data.Add([0f, 1f], [0f, 1f], 1.0f, true, "TwoElements");

            // WithSpecificRemainder_SIMD (SIMDのあまり処理)
            var remainderData = Enumerable.Range(0, 17).Select(i => (float)i).ToArray();
            data.Add(remainderData, remainderData, 1.0f, false, "WithSpecificRemainder_SIMD");

            return data;
        }

        /// <summary>
        /// 様々なエッジケース入力における Pearson 相関係数の算出結果を検証します。
        /// </summary>
        [Theory]
        [MemberData(nameof(GetEdgeCaseTestData))]
        public void CalculatePearsonCorrelation_Scenarios_ReturnsExpected(float[] wav1, float[] wav2, float expected, bool useScalar, string scenario)
        {
            void assertFunc(float correlation) => Assert.True(
                Math.Abs(correlation - expected) <= Tolerance,
                $"Scenario '{scenario}' failed. Expected {expected}, got {correlation}");

            if (useScalar)
            {
                RunPearsonTest(wav1, wav2, assertFunc);
            }
            else
            {
                RunPearsonSimdTest(wav1, wav2, assertFunc);
            }
        }

        /// <summary>
        /// 特定のエッジケース（ゼロ振幅、異なる長さ）における SIMD 処理での Pearson 相関係数の算出結果が 0.0 になることを検証します。
        /// </summary>
        [Theory]
        [InlineData(0, 100, "WithZeroAmplitude")]
        [InlineData(100, 101, "WithDifferentLengths")]
        public void CalculatePearsonCorrelationSIMD_SpecialScenarios_ReturnsZero(int len1, int len2, string scenario)
        {
            float[] wav1 = len1 == 0 ? new float[100] : GenerateSineWave(len1);
            float[] wav2 = GenerateSineWave(len2);
            RunPearsonSimdTest(wav1, wav2, correlation => Assert.True(
                Math.Abs(correlation - 0.0f) <= Tolerance,
                $"Scenario '{scenario}' failed. Expected 0.0, got {correlation}"));
        }

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

