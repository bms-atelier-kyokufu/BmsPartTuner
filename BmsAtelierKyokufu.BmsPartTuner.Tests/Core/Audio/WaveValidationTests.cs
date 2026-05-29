using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Audio
{
    /// <summary>
    /// <see cref="WaveValidation"/> のテストクラス。
    ///
    /// 【テスト対象】
    /// - 決定係数（R2）の計算精度
    /// - ピアソン相関係数の計算精度
    /// </summary>
    public class WaveValidationTests
    {
        private const float Tolerance = 0.001f;

        #region Helper Methods

        /// <summary>
        /// 正弦波を生成
        /// </summary>
        private static float[] GenerateSineWave(int samples, float frequency = 1f, float amplitude = 1f, float phase = 0f)
        {
            var result = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                result[i] = amplitude * (float)Math.Sin((2 * Math.PI * frequency * i / samples) + phase);
            }
            return result;
        }

        /// <summary>
        /// 正規化波形を生成（平均0、ノルム1）
        /// </summary>
        private static float[] NormalizeWaveform(float[] waveform)
        {
            if (waveform.Length == 0) return waveform;

            float mean = waveform.Average();
            float[] centered = [.. waveform.Select(x => x - mean)];
            float norm = (float)Math.Sqrt(centered.Sum(x => x * x));

            if (norm < 1e-10f) return centered;

            return [.. centered.Select(x => x / norm)];
        }

        #endregion

        #region CalculateRSquared Tests

        public static TheoryData<float[], float[], float, float> GetRSquaredTestData()
        {
            var data = new TheoryData<float[], float[], float, float>();
            var identical = GenerateSineWave(1000);
            data.AddCase(
                wav1: identical,
                wav2: identical,
                minExpected: 1.0f - Tolerance,
                maxExpected: 1.0f + Tolerance
            );

            var similar1 = GenerateSineWave(1000);
            var similar2 = similar1.Select(x => x + (0.01f * (float)(new Random(42).NextDouble() - 0.5))).ToArray();
            data.AddCase(
                wav1: similar1,
                wav2: similar2,
                minExpected: 0.95f,
                maxExpected: 1.01f
            );

            var diff1 = GenerateSineWave(1000, frequency: 1f);
            var diff2 = GenerateSineWave(1000, frequency: 5f);
            data.AddCase(
                wav1: diff1,
                wav2: diff2,
                minExpected: -0.01f,
                maxExpected: 0.5f
            );

            data.AddCase(
                wav1: [],
                wav2: [],
                minExpected: 0.0f,
                maxExpected: 0.0f
            );
            data.AddCase(
                wav1: GenerateSineWave(100),
                wav2: GenerateSineWave(200),
                minExpected: 0.0f,
                maxExpected: 0.0f
            );

            var constant = Enumerable.Repeat(0.5f, 100).ToArray();
            var variable = GenerateSineWave(100);
            data.AddCase(
                wav1: constant,
                wav2: variable,
                minExpected: 0.0f,
                maxExpected: 0.0f
            );
            return data;
        }

        [Theory]
        [MemberData(nameof(GetRSquaredTestData))]
        public void CalculateRSquared_BehaviorTests(float[] wav1, float[] wav2, float minExpected, float maxExpected)
        {
            var r2 = WaveValidation.CalculateRSquared(wav1, wav2);
            Assert.InRange(r2, minExpected, maxExpected);
        }

        #endregion

        #region CalculatePearsonCorrelation Tests

        public static TheoryData<float[], float[], float, float> GetPearsonTestData()
        {
            var data = new TheoryData<float[], float[], float, float>();
            var identical = GenerateSineWave(1000);
            data.AddCase(
                wav1: identical,
                wav2: identical,
                minExpected: 1.0f - Tolerance,
                maxExpected: 1.0f + Tolerance
            );

            var scaled1 = GenerateSineWave(1000, amplitude: 1.0f);
            var scaled2 = GenerateSineWave(1000, amplitude: 0.5f);
            data.AddCase(
                wav1: scaled1,
                wav2: scaled2,
                minExpected: 1.0f - Tolerance,
                maxExpected: 1.0f + Tolerance
            );

            var inverse1 = GenerateSineWave(1000);
            var inverse2 = inverse1.Select(x => -x).ToArray();
            data.AddCase(
                wav1: inverse1,
                wav2: inverse2,
                minExpected: -1.0f - Tolerance,
                maxExpected: -1.0f + Tolerance
            );

            var sin = GenerateSineWave(1000, phase: 0);
            var cos = GenerateSineWave(1000, phase: (float)(Math.PI / 2));
            data.AddCase(
                wav1: sin,
                wav2: cos,
                minExpected: -0.1f,
                maxExpected: 0.1f
            );

            var dc1 = GenerateSineWave(1000);
            var dc2 = dc1.Select(x => x + 0.5f).ToArray();
            data.AddCase(
                wav1: dc1,
                wav2: dc2,
                minExpected: 1.0f - Tolerance,
                maxExpected: 1.0f + Tolerance
            );

            data.AddCase(
                wav1: [],
                wav2: [],
                minExpected: 0.0f,
                maxExpected: 0.0f
            );
            data.AddCase(
                wav1: GenerateSineWave(100),
                wav2: GenerateSineWave(200),
                minExpected: 0.0f,
                maxExpected: 0.0f
            );

            var constant = Enumerable.Repeat(0.5f, 100).ToArray();
            var variable = GenerateSineWave(100);
            data.AddCase(
                wav1: constant,
                wav2: variable,
                minExpected: 0.0f,
                maxExpected: 0.0f
            );
            return data;
        }

        [Theory]
        [MemberData(nameof(GetPearsonTestData))]
        public void CalculatePearsonCorrelation_BehaviorTests(float[] wav1, float[] wav2, float minExpected, float maxExpected)
        {
            var correlation = WaveValidation.CalculatePearsonCorrelation(wav1, wav2);
            Assert.InRange(correlation, minExpected, maxExpected);
        }

        #endregion

        #region CalculatePearsonFromNormalized Tests

        public static TheoryData<float[], float[], float, float> GetPearsonNormalizedTestData()
        {
            var data = new TheoryData<float[], float[], float, float>();
            var wav1 = GenerateSineWave(1000);
            var norm1 = NormalizeWaveform(wav1);
            data.AddCase(
                wav1: norm1,
                wav2: norm1,
                minExpected: 1.0f - Tolerance,
                maxExpected: 1.0f + Tolerance
            );

            var wav2 = wav1.Select(x => -x).ToArray();
            var norm2 = NormalizeWaveform(wav2);
            data.AddCase(
                wav1: norm1,
                wav2: norm2,
                minExpected: -1.0f - Tolerance,
                maxExpected: -1.0f + Tolerance
            );

            data.AddCase(
                wav1: [],
                wav2: [],
                minExpected: 0.0f,
                maxExpected: 0.0f
            );

            var normDiff1 = NormalizeWaveform(GenerateSineWave(100));
            var normDiff2 = NormalizeWaveform(GenerateSineWave(200));
            data.AddCase(
                wav1: normDiff1,
                wav2: normDiff2,
                minExpected: 0.0f,
                maxExpected: 0.0f
            );
            return data;
        }

        [Theory]
        [MemberData(nameof(GetPearsonNormalizedTestData))]
        public void CalculatePearsonFromNormalized_BehaviorTests(float[] wav1, float[] wav2, float minExpected, float maxExpected)
        {
            var correlation = WaveValidation.CalculatePearsonFromNormalized(wav1, wav2);
            Assert.InRange(correlation, minExpected, maxExpected);
        }

        [Fact]
        public void CalculatePearsonFromNormalized_MatchesStandardPearson()
        {
            // Arrange - 正規化版と標準版が同じ結果を返すことを確認
            var wav1 = GenerateSineWave(1000);
            var wav2 = GenerateSineWave(1000, phase: 0.5f);
            var norm1 = NormalizeWaveform(wav1);
            var norm2 = NormalizeWaveform(wav2);

            // Act
            var normalizedResult = WaveValidation.CalculatePearsonFromNormalized(norm1, norm2);
            var standardResult = WaveValidation.CalculatePearsonCorrelation(wav1, wav2);

            // Assert
            Assert.Equal(standardResult, normalizedResult, 0.01f);  // 若干の誤差を許容
        }

        #endregion
    }
}
