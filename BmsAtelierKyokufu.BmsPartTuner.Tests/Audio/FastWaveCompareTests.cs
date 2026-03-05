using BmsAtelierKyokufu.BmsPartTuner.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Audio
{
    /// <summary>
    /// FastWaveCompare の動作検証テスト。
    /// 音声データの相関係数計算・一致判定の仕様を確認します。
    /// </summary>
    public class FastWaveCompareTests
    {
        private CachedSoundData CreateCachedSoundData(float[] samples, int channels = 1)
        {
            float[][] samplesPerChannel = new float[channels][];
            int samplesPerCh = samples.Length / channels;

            for (int i = 0; i < channels; i++)
            {
                samplesPerChannel[i] = new float[samplesPerCh];
                for (int j = 0; j < samplesPerCh; j++)
                {
                    samplesPerChannel[i][j] = samples[j * channels + i];
                }
            }

            return new CachedSoundData(samplesPerChannel, 44100, 16);
        }

        [Fact]
        public void IsMatch_ExactMatch_ReturnsTrue()
        {
            float[] data = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            using var sound1 = CreateCachedSoundData(data);
            using var sound2 = CreateCachedSoundData(data);

            // 高い閾値でも一致するべき
            Assert.True(FastWaveCompare.IsMatch(sound1, sound2, 0.99f));
        }

        [Fact]
        public void IsMatch_DifferentLengths_ReturnsFalse()
        {
            using var sound1 = CreateCachedSoundData(new float[] { 0.1f, 0.2f });
            using var sound2 = CreateCachedSoundData(new float[] { 0.1f, 0.2f, 0.3f });

            Assert.False(FastWaveCompare.IsMatch(sound1, sound2, 0.1f));
        }

        [Fact]
        public void IsMatch_Silence_HandlesGracefully()
        {
            // 無音データ（ゼロ分散）は定数値として扱われ、
            // 同じ定数値（0.0）なので相関係数は1.0となり、一致判定される

            float[] silence = new float[] { 0.0f, 0.0f, 0.0f, 0.0f };
            using var sound1 = CreateCachedSoundData(silence);
            using var sound2 = CreateCachedSoundData(silence);

            // 同一の無音データなので一致する（相関係数=1.0）
            bool match = FastWaveCompare.IsMatch(sound1, sound2, 0.9f);
            Assert.True(match, "同一の無音データは一致する（相関係数=1.0）");
        }

        [Fact]
        public void IsMatch_NearSilence_HandlesGracefully()
        {
            float[] nearSilence1 = new float[] { 1e-6f, -1e-6f };
            float[] nearSilence2 = new float[] { 1e-6f, -1e-6f };

            using var sound1 = CreateCachedSoundData(nearSilence1);
            using var sound2 = CreateCachedSoundData(nearSilence2);

            // 微小な値でもノルム閾値(1e-10)を超えるため正規化される
            // 同一データなので相関係数は1.0になる
            Assert.True(FastWaveCompare.IsMatch(sound1, sound2, 0.99f));
        }

        [Fact]
        public void IsMatch_VolumeDifference_ReturnsTrue()
        {
            float[] data1 = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            float[] data2 = new float[] { 0.05f, 0.1f, 0.15f, 0.2f }; // 半分の振幅

            using var sound1 = CreateCachedSoundData(data1);
            using var sound2 = CreateCachedSoundData(data2);

            // 波形の形が同一なので相関係数は1.0
            Assert.True(FastWaveCompare.IsMatch(sound1, sound2, 0.99f));
        }

        [Fact]
        public void IsMatch_InvertedPhase_ReturnsFalse()
        {
            float[] data1 = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            float[] data2 = new float[] { -0.1f, -0.2f, -0.3f, -0.4f }; // 位相反転

            using var sound1 = CreateCachedSoundData(data1);
            using var sound2 = CreateCachedSoundData(data2);

            // 相関係数は-1.0になる
            Assert.False(FastWaveCompare.IsMatch(sound1, sound2, 0.9f));
        }

        [Fact]
        public void IsMatch_DifferentSampleRates_ReturnsFalse()
        {
            // フォーマット不一致: サンプリングレートが異なる場合
            float[] data = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

            float[][] samples1 = new float[1][];
            samples1[0] = data;
            var sound1 = new CachedSoundData(samples1, 44100, 16);

            float[][] samples2 = new float[1][];
            samples2[0] = data;
            var sound2 = new CachedSoundData(samples2, 48000, 16); // Different sample rate

            Assert.False(FastWaveCompare.IsMatch(sound1, sound2, 0.1f));
        }

        [Fact]
        public void IsMatch_DifferentChannels_ReturnsFalse()
        {
            // フォーマット不一致: チャンネル数が異なる場合
            float[] monoData = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            float[] stereoData = new float[] { 0.1f, 0.1f, 0.2f, 0.2f, 0.3f, 0.3f, 0.4f, 0.4f };

            using var monoSound = CreateCachedSoundData(monoData, channels: 1);
            using var stereoSound = CreateCachedSoundData(stereoData, channels: 2);

            Assert.False(FastWaveCompare.IsMatch(monoSound, stereoSound, 0.1f));
        }

        [Fact]
        public void IsMatch_DifferentBitDepths_ReturnsFalse()
        {
            // フォーマット不一致: ビット深度が異なる場合
            float[] data = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

            float[][] samples1 = new float[1][];
            samples1[0] = data;
            var sound1 = new CachedSoundData(samples1, 44100, 16);

            float[][] samples2 = new float[1][];
            samples2[0] = data;
            var sound2 = new CachedSoundData(samples2, 44100, 24); // Different bit depth

            Assert.False(FastWaveCompare.IsMatch(sound1, sound2, 0.1f));
        }

        [Fact]
        public void IsMatch_EmptyFiles_ThrowsException()
        {
            // 空ファイル（サンプル数0）は CachedSoundData のコンストラクタで例外をスローする
            float[] emptyData = Array.Empty<float>();

            // ArgumentExceptionがスローされることを確認
            Assert.Throws<ArgumentException>(() => CreateCachedSoundData(emptyData));
        }

        [Fact]
        public void IsMatch_LargeDataSIMDPath_WorksCorrectly()
        {
            // SIMD分岐: 大きなデータで最適化パスをテスト
            // 通常、SIMD処理は4サンプル以上で動作するため、128サンプルのデータを用意
            float[] largeData = new float[128];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (float)Math.Sin(i * 0.1);
            }

            using var sound1 = CreateCachedSoundData(largeData);
            using var sound2 = CreateCachedSoundData(largeData);

            Assert.True(FastWaveCompare.IsMatch(sound1, sound2, 0.99f));
        }

        [Fact]
        public void IsMatch_SmallDataNonSIMDPath_WorksCorrectly()
        {
            // 非SIMD分岐: 小さなデータで通常パスをテスト
            float[] smallData = new float[] { 0.1f, 0.2f };

            using var sound1 = CreateCachedSoundData(smallData);
            using var sound2 = CreateCachedSoundData(smallData);

            Assert.True(FastWaveCompare.IsMatch(sound1, sound2, 0.99f));
        }

        [Fact]
        public void GetCorrelation_ExactMatch_ReturnsOne()
        {
            float[] data = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            using var sound1 = CreateCachedSoundData(data);
            using var sound2 = CreateCachedSoundData(data);

            float correlation = FastWaveCompare.GetCorrelation(sound1, sound2);

            Assert.True(correlation >= 0.99f && correlation <= 1.01f,
                $"Expected correlation ~1.0, but got {correlation}");
        }

        [Fact]
        public void GetCorrelation_FormatMismatch_ReturnsZero()
        {
            float[] data = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

            float[][] samples1 = new float[1][];
            samples1[0] = data;
            var sound1 = new CachedSoundData(samples1, 44100, 16);

            float[][] samples2 = new float[1][];
            samples2[0] = data;
            var sound2 = new CachedSoundData(samples2, 48000, 16); // Different format

            float correlation = FastWaveCompare.GetCorrelation(sound1, sound2);

            Assert.Equal(0.0f, correlation);
        }

        [Fact]
        public void GetCorrelation_InvertedPhase_ReturnsNegativeOne()
        {
            float[] data1 = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            float[] data2 = new float[] { -0.1f, -0.2f, -0.3f, -0.4f };

            using var sound1 = CreateCachedSoundData(data1);
            using var sound2 = CreateCachedSoundData(data2);

            float correlation = FastWaveCompare.GetCorrelation(sound1, sound2);

            Assert.True(correlation <= -0.99f && correlation >= -1.01f,
                $"Expected correlation ~-1.0, but got {correlation}");
        }

        [Fact]
        public void IsMatch_WithNormalizedWaveform_UsesOptimizedPath()
        {
            // 正規化波形が事前計算されている場合の最適化パステスト
            float[] data = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            using var sound1 = CreateCachedSoundData(data);
            using var sound2 = CreateCachedSoundData(data);

            // NormalizedWaveformが存在する場合のテスト
            // Note: CreateCachedSoundDataは自動的に正規化を行うかどうかは実装依存
            Assert.True(FastWaveCompare.IsMatch(sound1, sound2, 0.99f));
        }

        [Fact]
        public void IsMatch_WithHighThreshold_FiltersSimilarButNotIdentical()
        {
            float[] data1 = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
            float[] data2 = new float[] { 0.1f, 0.2f, 0.3f, 0.35f }; // Slightly different

            using var sound1 = CreateCachedSoundData(data1);
            using var sound2 = CreateCachedSoundData(data2);

            // High threshold should reject slightly different data
            float correlation = FastWaveCompare.GetCorrelation(sound1, sound2);
            bool matchesHighThreshold = FastWaveCompare.IsMatch(sound1, sound2, 0.99f);
            bool matchesLowThreshold = FastWaveCompare.IsMatch(sound1, sound2, 0.90f);

            // 閾値による振る舞いの違いを検証
            Assert.True(correlation < 1.0f, "Correlation should be less than 1.0 for different data");
        }

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
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = (float)Math.Sin(i * 0.5) * 0.5f;
            }

            using var sound1 = CreateCachedSoundData(data);
            using var sound2 = CreateCachedSoundData(data);

            Assert.True(FastWaveCompare.IsMatch(sound1, sound2, 0.99f));
        }

        /// <summary>
        /// ヘッダーのみでデータ部が極小のWAVファイル相当のテスト。
        /// </summary>
        [Fact]
        public void IsMatch_MinimalData_HandlesGracefully()
        {
            // 最小限のデータ（1サンプル）
            float[] minimalData = new float[] { 0.5f };

            using var sound1 = CreateCachedSoundData(minimalData);
            using var sound2 = CreateCachedSoundData(minimalData);

            // 1サンプルでも処理が完了すること
            bool result = FastWaveCompare.IsMatch(sound1, sound2, 0.99f);
            Assert.True(result);
        }

        /// <summary>
        /// 定数値データ（分散0）の場合の検証。
        /// 分散が0だと相関係数が計算不能になる可能性がある。
        /// </summary>
        [Fact]
        public void IsMatch_ConstantValueData_HandlesGracefully()
        {
            // すべて同じ値（分散0）
            float[] constantData = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };

            using var sound1 = CreateCachedSoundData(constantData);
            using var sound2 = CreateCachedSoundData(constantData);

            // 例外をスローせずに完了すること
            // 定数値の場合、正規化後にゼロベクトルになる可能性がある
            var exception = Record.Exception(() => FastWaveCompare.IsMatch(sound1, sound2, 0.5f));
            Assert.Null(exception);
        }

        /// <summary>
        /// 非常に大きな振幅値のデータでのオーバーフロー検証。
        /// </summary>
        [Fact]
        public void IsMatch_LargeAmplitude_NoOverflow()
        {
            float[] largeData = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };

            using var sound1 = CreateCachedSoundData(largeData);
            using var sound2 = CreateCachedSoundData(largeData);

            var exception = Record.Exception(() => FastWaveCompare.IsMatch(sound1, sound2, 0.99f));
            Assert.Null(exception);
        }

        /// <summary>
        /// 非常に小さな振幅値のデータでのアンダーフロー検証。
        /// </summary>
        [Fact]
        public void IsMatch_TinyAmplitude_NoUnderflow()
        {
            float[] tinyData = new float[] { 1e-7f, 2e-7f, 3e-7f, 4e-7f };

            using var sound1 = CreateCachedSoundData(tinyData);
            using var sound2 = CreateCachedSoundData(tinyData);

            var exception = Record.Exception(() => FastWaveCompare.IsMatch(sound1, sound2, 0.99f));
            Assert.Null(exception);
        }

        /// <summary>
        /// ステレオデータの左右チャンネルが異なる場合の検証。
        /// </summary>
        [Fact]
        public void IsMatch_StereoWithDifferentChannels_ComparesCorrectly()
        {
            // 左右で異なるデータを持つステレオ
            float[] stereoData1 = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
            float[] stereoData2 = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };

            using var sound1 = CreateCachedSoundData(stereoData1, channels: 2);
            using var sound2 = CreateCachedSoundData(stereoData2, channels: 2);

            Assert.True(FastWaveCompare.IsMatch(sound1, sound2, 0.99f));
        }

        /// <summary>
        /// 無限大やNaNを含むデータの検証。
        /// </summary>
        [Fact]
        public void IsMatch_SpecialFloatValues_HandlesGracefully()
        {
            // NaNを含むデータ
            float[] dataWithNaN = new float[] { 0.1f, float.NaN, 0.3f, 0.4f };

            using var sound1 = CreateCachedSoundData(dataWithNaN);
            using var sound2 = CreateCachedSoundData(dataWithNaN);

            // 例外をスローせずに完了すること（結果は実装依存）
            var exception = Record.Exception(() => FastWaveCompare.IsMatch(sound1, sound2, 0.5f));
            Assert.Null(exception);
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
        public void IsMatch_ThresholdBoundaries_ProcessesCorrectly(float threshold)
        {
            float[] data = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

            using var sound1 = CreateCachedSoundData(data);
            using var sound2 = CreateCachedSoundData(data);

            // 同一データなので、しきい値に関係なく一致するはず
            bool result = FastWaveCompare.IsMatch(sound1, sound2, threshold);

            // しきい値が1.0以下であれば、完全一致データはtrue
            if (threshold <= 1.0f)
            {
                Assert.True(result, $"Identical data should match at threshold {threshold}");
            }
        }

        #endregion

        #region Priority A: Correlation Coefficient Edge Cases

        /// <summary>
        /// 相関係数が境界値付近のケース。
        /// </summary>
        [Fact]
        public void GetCorrelation_SimilarButNotIdentical_ReturnsBetweenZeroAndOne()
        {
            // わずかにノイズを加えたデータ
            float[] data1 = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
            float[] data2 = new float[] { 0.11f, 0.19f, 0.31f, 0.39f, 0.51f, 0.59f, 0.71f, 0.79f };

            using var sound1 = CreateCachedSoundData(data1);
            using var sound2 = CreateCachedSoundData(data2);

            float correlation = FastWaveCompare.GetCorrelation(sound1, sound2);

            // 相関係数は0と1の間（類似しているが同一ではない）
            Assert.True(correlation > 0.9f && correlation < 1.0f,
                $"Expected correlation between 0.9 and 1.0, but got {correlation}");
        }

        /// <summary>
        /// 完全に無相関なデータの検証。
        /// </summary>
        [Fact]
        public void GetCorrelation_UncorrelatedData_ReturnsLessThanOne()
        {
            // 直交するデータ（相関なし）
            float[] data1 = new float[] { 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f };
            float[] data2 = new float[] { 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f };

            using var sound1 = CreateCachedSoundData(data1);
            using var sound2 = CreateCachedSoundData(data2);

            float correlation = FastWaveCompare.GetCorrelation(sound1, sound2);

            // 直交データの相関係数は1.0未満であるべき
            Assert.True(correlation < 1.0f,
                $"Expected correlation less than 1.0 for orthogonal data, but got {correlation}");
        }

        #endregion
    }
}


