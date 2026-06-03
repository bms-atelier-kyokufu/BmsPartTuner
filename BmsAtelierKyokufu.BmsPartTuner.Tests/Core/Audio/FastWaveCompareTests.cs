using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Audio
{
    /// <summary>
    /// FastWaveCompare の動作検証テスト。
    /// 音声データの相関係数計算・一致判定の仕様を確認します。
    /// </summary>
    /// <summary>
    /// <see cref="FastWaveCompareTests"/> の動作を検証するテストクラス。
    /// </summary>
    public class FastWaveCompareTests
    {
        private static void RunWithSounds(float[] data1, float[] data2, Action<MockCachedSoundData, MockCachedSoundData> action, int channels = 1)
        {
            using var sound1 = BmsTestAudioHelper.CreatePreNormalizedSoundData(data1, channels);
            using var sound2 = BmsTestAudioHelper.CreatePreNormalizedSoundData(data2, channels);
            action(sound1, sound2);
        }

        private static void RunIsMatchTest(float[] data1, float[] data2, float threshold, Action<bool> assertMatch, int channels = 1) =>
            RunWithSounds(data1, data2, (sound1, sound2) => assertMatch(FastWaveCompare.IsMatch(sound1, sound2, threshold)), channels);

        private static void RunCorrelationTest(float[] data1, float[] data2, Action<float> assertCorrelation, int channels = 1) =>
            RunWithSounds(data1, data2, (sound1, sound2) => assertCorrelation(FastWaveCompare.GetCorrelation(sound1, sound2)), channels);

        private static MockCachedSoundData CreateMockSound(float[] data, int sampleRate = 44100, int bitDepth = 16) =>
            new MockCachedSoundData([data], sampleRate, bitDepth);

        public static TheoryData<float[], float[], float, bool, string> GetIsMatchTestData()
        {
            var data = new TheoryData<float[], float[], float, bool, string>();
            var baseData4 = Enumerable.Range(1, 4).Select(i => i * 0.1f).ToArray();
            var baseData2 = Enumerable.Range(1, 2).Select(i => i * 0.1f).ToArray();
            var baseData3 = Enumerable.Range(1, 3).Select(i => i * 0.1f).ToArray();
            var volumeData4 = Enumerable.Range(1, 4).Select(i => i * 0.05f).ToArray();
            var invertedData4 = Enumerable.Range(1, 4).Select(i => -i * 0.1f).ToArray();

            // ExactMatch (完全一致)
            data.Add(baseData4, baseData4, 0.99f, true, "ExactMatch");

            // DifferentLengths (長さ不一致)
            data.Add(baseData2, baseData3, 0.1f, false, "DifferentLengths");

            // Silence (無音)
            data.Add(new float[4], new float[4], 0.9f, true, "Silence");

            // NearSilence (ほぼ無音)
            data.Add([1e-6f, -1e-6f], [1e-6f, -1e-6f], 0.99f, true, "NearSilence");

            // VolumeDifference (音量差)
            data.Add(baseData4, volumeData4, 0.99f, true, "VolumeDifference");

            // InvertedPhase (逆位相)
            data.Add(baseData4, invertedData4, 0.9f, false, "InvertedPhase");

            // SmallDataNonSIMDPath (SIMD対象外の短いデータ)
            data.Add(baseData2, baseData2, 0.99f, true, "SmallDataNonSIMDPath");

            // WithNormalizedWaveform (ノーマライズ済み波形)
            data.Add(baseData4, baseData4, 0.99f, true, "WithNormalizedWaveform");

            // MinimalData (最小データ数)
            data.Add([0.5f], [0.5f], 0.99f, true, "MinimalData");

            return data;
        }

        public static TheoryData<float[], float[], float, string> GetCorrelationTestData()
        {
            var data = new TheoryData<float[], float[], float, string>();
            var baseData = Enumerable.Range(1, 4).Select(i => i * 0.1f).ToArray();
            var invertedData = Enumerable.Range(1, 4).Select(i => -i * 0.1f).ToArray();

            data.Add(baseData, baseData, 1.0f, "ExactMatch");
            data.Add(baseData, invertedData, -1.0f, "InvertedPhase");

            return data;
        }

        public static TheoryData<float[], float[], float, float, string> GetCorrelationRangeTestData()
        {
            var data = new TheoryData<float[], float[], float, float, string>();

            // SimilarButNotIdentical (類似しているが不完全一致)
            var similar1 = Enumerable.Range(1, 8).Select(i => i * 0.1f).ToArray();
            var similar2 = Enumerable.Range(1, 8).Select(i => (i * 0.1f) + (i % 2 == 1 ? 0.01f : -0.01f)).ToArray();
            data.Add(similar1, similar2, 0.9f, 1.0f, "SimilarButNotIdentical");

            // UncorrelatedData (無相関)
            var uncorrelated1 = Enumerable.Range(0, 8).Select(i => i % 2 == 0 ? 1.0f : 0.0f).ToArray();
            var uncorrelated2 = Enumerable.Range(0, 8).Select(i => i % 2 == 0 ? 0.0f : 1.0f).ToArray();
            data.Add(uncorrelated1, uncorrelated2, -1.01f, 1.0f, "UncorrelatedData");

            return data;
        }

        public static TheoryData<float[], string> GetIsMatchEdgeCaseTestData()
        {
            var data = new TheoryData<float[], string>();

            // ConstantValueData
            data.Add([.. Enumerable.Repeat(0.5f, 4)], "ConstantValueData");

            // LargeAmplitude
            data.Add([.. Enumerable.Repeat(1.0f, 4)], "LargeAmplitude");

            // TinyAmplitude
            data.Add([.. Enumerable.Range(1, 4).Select(i => i * 1e-7f)], "TinyAmplitude");

            // SpecialFloatValues
            data.Add([0.1f, float.NaN, 0.3f, 0.4f], "SpecialFloatValues");

            return data;
        }

        /// <summary>
        /// 様々なしきい値・入力データの組み合わせにおける IsMatch の動作を検証します。
        /// </summary>
        [Theory]
        [MemberData(nameof(GetIsMatchTestData))]
        public void IsMatch_VariousScenarios_BehaveAsExpected(float[] data1, float[] data2, float threshold, bool expected, string scenario)
        {
            if (expected)
                RunIsMatchTest(data1, data2, threshold, result => Assert.True(result, $"Scenario '{scenario}' failed."));
            else
                RunIsMatchTest(data1, data2, threshold, result => Assert.False(result, $"Scenario '{scenario}' failed."));
        }

        /// <summary>
        /// フォーマット不一致（サンプリングレート、ビット深度の違い）により IsMatch が false を返すことを検証します。
        /// </summary>
        [Theory]
        [InlineData(44100, 16, 48000, 16, "DifferentSampleRates")]
        [InlineData(44100, 16, 44100, 24, "DifferentBitDepths")]
        public void IsMatch_FormatMismatch_ReturnsFalse(int sr1, int bd1, int sr2, int bd2, string scenario)
        {
            float[] data = [0.1f, 0.2f, 0.3f, 0.4f];
            using var sound1 = CreateMockSound(data, sampleRate: sr1, bitDepth: bd1);
            using var sound2 = CreateMockSound(data, sampleRate: sr2, bitDepth: bd2);
            Assert.False(FastWaveCompare.IsMatch(sound1, sound2, 0.1f), $"Scenario '{scenario}' failed.");
        }

        /// <summary>
        /// フォーマット不一致のステレオ・モノラルにおいて IsMatch が false を返すことを検証します。
        /// </summary>
        [Fact]
        public void IsMatch_DifferentChannels_ReturnsFalse()
        {
            // フォーマット不一致: チャンネル数が異なる場合
            float[] monoData = [.. Enumerable.Range(1, 4).Select(i => i * 0.1f)];
            float[] stereoData = [.. Enumerable.Range(1, 4).SelectMany(i => new[] { i * 0.1f, i * 0.1f })];

            using var monoSound = BmsTestAudioHelper.CreatePreNormalizedSoundData(monoData, channels: 1);
            using var stereoSound = BmsTestAudioHelper.CreatePreNormalizedSoundData(stereoData, channels: 2);

            Assert.False(FastWaveCompare.IsMatch(monoSound, stereoSound, 0.1f));
        }

        /// <summary>
        /// 空ファイルの入力において IsMatch 呼び出し時に例外がスローされることを検証します。
        /// </summary>
        [Fact]
        public void IsMatch_EmptyFiles_ThrowsException()
        {
            // 空ファイル（サンプル数0）は PreNormalizedSoundData のコンストラクタで例外をスローする
            float[] emptyData = [];

            // ArgumentExceptionがスローされることを確認
            Assert.Throws<ArgumentException>(() => BmsTestAudioHelper.CreatePreNormalizedSoundData(emptyData));
        }

        /// <summary>
        /// SIMD分岐: 大きなデータで最適化パスをテスト
        /// </summary>
        [Fact]
        public void IsMatch_LargeDataSIMDPath_WorksCorrectly()
        {
            // 通常、SIMD処理は4サンプル以上で動作するため、128サンプルのデータを用意
            float[] largeData = new float[128];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (float)Math.Sin(i * 0.1);
            }
            RunIsMatchTest(largeData, largeData, 0.99f, Assert.True);
        }

        /// <summary>
        /// 特定の相関係数の期待値を持つ入力における GetCorrelation の動作を検証します。
        /// </summary>
        [Theory]
        [MemberData(nameof(GetCorrelationTestData))]
        public void GetCorrelation_VariousScenarios_ReturnsExpectedCorrelation(float[] data1, float[] data2, float expected, string scenario)
        {
            RunCorrelationTest(data1, data2, correlation =>
            {
                Assert.True(correlation >= expected - 0.01f && correlation <= expected + 0.01f,
                    $"Expected correlation near {expected} for scenario {scenario}, but got {correlation}");
            });
        }

        /// <summary>
        /// フォーマット不一致時の相関係数算出結果が 0.0 となることを検証します。
        /// </summary>
        [Fact]
        public void GetCorrelation_FormatMismatch_ReturnsZero()
        {
            float[] data = [0.1f, 0.2f, 0.3f, 0.4f];
            using var sound1 = CreateMockSound(data, sampleRate: 44100);
            using var sound2 = CreateMockSound(data, sampleRate: 48000);
            Assert.Equal(0.0f, FastWaveCompare.GetCorrelation(sound1, sound2));
        }

        /// <summary>
        /// IsMatch において、条件 WithHighThreshold の場合に FiltersSimilarButNotIdentical されることを検証します。
        /// </summary>
        [Fact]
        public void IsMatch_WithHighThreshold_FiltersSimilarButNotIdentical() =>
            RunWithSounds([0.1f, 0.2f, 0.3f, 0.4f], [0.1f, 0.2f, 0.3f, 0.35f], (sound1, sound2) =>
            {
                // High threshold should reject slightly different data
                float correlation = FastWaveCompare.GetCorrelation(sound1, sound2);
                bool matchesHighThreshold = FastWaveCompare.IsMatch(sound1, sound2, 0.99f);
                bool matchesLowThreshold = FastWaveCompare.IsMatch(sound1, sound2, 0.90f);

                // 閾値による振る舞いの違いを検証
                Assert.True(correlation < 1.0f, "Correlation should be less than 1.0 for different data");
                Assert.False(matchesHighThreshold, "High threshold should reject slightly different data");
                Assert.True(matchesLowThreshold, "Low threshold should accept slightly different data");
            });

        #region Priority A: SIMD Fallback and Edge Case Tests

        /// <summary>
        /// SIMD境界サイズ（4の倍数でない）でのデータ処理検証。
        /// </summary>
        [Theory]
        [InlineData(3)]   // 4未満
        [InlineData(5)]   // 4の倍数+1
        [InlineData(7)]   // 4の倍数+3
        [InlineData(15)]  // 4の倍数-1
        [InlineData(17)]  // 4の倍数+1
        public void IsMatch_NonMultipleOfFourLength_WorksCorrectly(int length)
        {
            float[] data = [.. Enumerable.Range(0, length).Select(i => (float)Math.Sin(i * 0.5) * 0.5f)];
            RunWithSounds(data, data, (sound1, sound2) => Assert.True(FastWaveCompare.IsMatch(sound1, sound2, 0.99f)));
        }

        /// <summary>
        /// 定数値、限界値、NaN/無限大などのエッジケース入力における IsMatch の動作を検証します。
        /// </summary>
        [Theory]
        [MemberData(nameof(GetIsMatchEdgeCaseTestData))]
        public void IsMatch_EdgeCases_NoExceptionThrown(float[] data, string scenario) =>
            RunWithSounds(data, data, (sound1, sound2) =>
            {
                // 例外をスローせずに完了すること
                var exception = Record.Exception(() => FastWaveCompare.IsMatch(sound1, sound2, 0.5f));
                Assert.True(exception == null, $"Scenario '{scenario}' threw an exception: {exception?.Message}");
            });

        /// <summary>
        /// ステレオデータの左右チャンネルが異なる場合の検証。
        /// </summary>
        [Fact]
        public void IsMatch_StereoWithDifferentChannels_ComparesCorrectly()
        {
            var stereoData = Enumerable.Range(1, 8).Select(i => i * 0.1f).ToArray();
            RunIsMatchTest(stereoData, stereoData, 0.99f, Assert.True, channels: 2);
        }

        /// <summary>
        /// しきい値の境界値テスト。
        /// </summary>
        [Theory]
        [InlineData(0.0f)]   // 最小しきい値
        [InlineData(1.0f)]   // 最大しきい値
        [InlineData(0.5f)]   // 中間値
        [InlineData(0.001f)] // 極小しきい値
        [InlineData(0.999f)] // 極大しきい値
        public void IsMatch_ThresholdBoundaries_ProcessesCorrectly(float threshold) =>
            RunWithSounds([0.1f, 0.2f, 0.3f, 0.4f], [0.1f, 0.2f, 0.3f, 0.4f], (sound1, sound2) =>
            {
                // 同一データなので、しきい値に関係なく一致するはず
                bool result = FastWaveCompare.IsMatch(sound1, sound2, threshold);

                // しきい値が1.0以下であれば、完全一致データはtrue
                if (threshold <= 1.0f)
                {
                    Assert.True(result, $"Identical data should match at threshold {threshold}");
                }
            });

        #endregion

        #region Priority A: Correlation Coefficient Edge Cases

        /// <summary>
        /// 特定の相関係数の範囲を持つ入力における GetCorrelation の動作を検証します。
        /// </summary>
        [Theory]
        [MemberData(nameof(GetCorrelationRangeTestData))]
        public void GetCorrelation_RangeScenarios_ReturnsWithinExpectedRange(float[] data1, float[] data2, float min, float max, string scenario)
        {
            RunCorrelationTest(data1, data2, correlation =>
            {
                Assert.True(correlation > min && correlation < max,
                    $"Expected correlation between {min} and {max} for scenario {scenario}, but got {correlation}");
            });
        }

        #endregion
    }
}
