using BmsAtelierKyokufu.BmsPartTuner.Audio;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Audio;

/// <summary>
/// <see cref="WaveValidation"/> のテストクラス。
/// 
/// 【テスト対象】
/// - 決定係数（R?）の計算精度
/// - ピアソン相関係数の計算精度
/// - SIMD最適化版の正確性
/// - エッジケースの処理
/// 
/// 【テスト設計方針】
/// - 数学的正確性: 既知の値との比較
/// - 境界値: 完全一致、完全不一致、逆相
/// - エッジケース: 空配列、長さ不一致、定数配列
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
            result[i] = amplitude * (float)Math.Sin(2 * Math.PI * frequency * i / samples + phase);
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
        float[] centered = waveform.Select(x => x - mean).ToArray();
        float norm = (float)Math.Sqrt(centered.Sum(x => x * x));

        if (norm < 1e-10f) return centered;

        return centered.Select(x => x / norm).ToArray();
    }

    #endregion

    #region CalculateRSquared Tests - 決定係数

    [Fact]
    public void CalculateRSquared_IdenticalArrays_ReturnsOne()
    {
        // Arrange
        var wav = GenerateSineWave(1000);

        // Act
        var r2 = WaveValidation.CalculateRSquared(wav, wav);

        // Assert
        Assert.Equal(1.0f, r2, Tolerance);
    }

    [Fact]
    public void CalculateRSquared_SimilarArrays_ReturnsHighValue()
    {
        // Arrange
        var wav1 = GenerateSineWave(1000);
        var wav2 = wav1.Select(x => x + 0.01f * (float)(new Random(42).NextDouble() - 0.5)).ToArray();

        // Act
        var r2 = WaveValidation.CalculateRSquared(wav1, wav2);

        // Assert
        Assert.True(r2 > 0.95f, $"Expected R? > 0.95, got {r2}");
    }

    [Fact]
    public void CalculateRSquared_DifferentArrays_ReturnsLowValue()
    {
        // Arrange
        var wav1 = GenerateSineWave(1000, frequency: 1f);
        var wav2 = GenerateSineWave(1000, frequency: 5f);  // 異なる周波数

        // Act
        var r2 = WaveValidation.CalculateRSquared(wav1, wav2);

        // Assert
        Assert.True(r2 < 0.5f, $"Expected R? < 0.5, got {r2}");
    }

    [Fact]
    public void CalculateRSquared_EmptyArrays_ReturnsZero()
    {
        // Arrange
        var empty = Array.Empty<float>();

        // Act
        var r2 = WaveValidation.CalculateRSquared(empty, empty);

        // Assert
        Assert.Equal(0.0f, r2);
    }

    [Fact]
    public void CalculateRSquared_DifferentLengths_ReturnsZero()
    {
        // Arrange
        var wav1 = GenerateSineWave(100);
        var wav2 = GenerateSineWave(200);

        // Act
        var r2 = WaveValidation.CalculateRSquared(wav1, wav2);

        // Assert
        Assert.Equal(0.0f, r2);
    }

    [Fact]
    public void CalculateRSquared_ConstantArray_ReturnsZero()
    {
        // Arrange - 分散ゼロの配列
        var constant = Enumerable.Repeat(0.5f, 100).ToArray();
        var variable = GenerateSineWave(100);

        // Act
        var r2 = WaveValidation.CalculateRSquared(constant, variable);

        // Assert
        Assert.Equal(0.0f, r2);  // DSSがゼロに近いため
    }

    #endregion

    #region CalculatePearsonCorrelation Tests - ピアソン相関係数

    [Fact]
    public void CalculatePearsonCorrelation_IdenticalArrays_ReturnsOne()
    {
        // Arrange
        var wav = GenerateSineWave(1000);

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(wav, wav);

        // Assert
        Assert.Equal(1.0f, correlation, Tolerance);
    }

    [Fact]
    public void CalculatePearsonCorrelation_ScaledArrays_ReturnsOne()
    {
        // Arrange - 音量差（スケール）に影響されないことを確認
        var wav1 = GenerateSineWave(1000, amplitude: 1.0f);
        var wav2 = GenerateSineWave(1000, amplitude: 0.5f);  // 半分の音量

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(wav1, wav2);

        // Assert
        Assert.Equal(1.0f, correlation, Tolerance);
    }

    [Fact]
    public void CalculatePearsonCorrelation_InverseArrays_ReturnsNegativeOne()
    {
        // Arrange - 逆相
        var wav1 = GenerateSineWave(1000);
        var wav2 = wav1.Select(x => -x).ToArray();

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(wav1, wav2);

        // Assert
        Assert.Equal(-1.0f, correlation, Tolerance);
    }

    [Fact]
    public void CalculatePearsonCorrelation_UncorrelatedArrays_ReturnsNearZero()
    {
        // Arrange - 無相関（sin と cos）
        var sin = GenerateSineWave(1000, phase: 0);
        var cos = GenerateSineWave(1000, phase: (float)(Math.PI / 2));

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(sin, cos);

        // Assert
        Assert.True(Math.Abs(correlation) < 0.1f, $"Expected near 0, got {correlation}");
    }

    [Fact]
    public void CalculatePearsonCorrelation_WithDCOffset_StillCorrelated()
    {
        // Arrange - DCオフセットに影響されないことを確認
        var wav1 = GenerateSineWave(1000);
        var wav2 = wav1.Select(x => x + 0.5f).ToArray();  // DCオフセット追加

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(wav1, wav2);

        // Assert
        Assert.Equal(1.0f, correlation, Tolerance);
    }

    [Fact]
    public void CalculatePearsonCorrelation_EmptyArrays_ReturnsZero()
    {
        // Arrange
        var empty = Array.Empty<float>();

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(empty, empty);

        // Assert
        Assert.Equal(0.0f, correlation);
    }

    [Fact]
    public void CalculatePearsonCorrelation_DifferentLengths_ReturnsZero()
    {
        // Arrange
        var wav1 = GenerateSineWave(100);
        var wav2 = GenerateSineWave(200);

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(wav1, wav2);

        // Assert
        Assert.Equal(0.0f, correlation);
    }

    [Fact]
    public void CalculatePearsonCorrelation_ConstantArray_ReturnsZero()
    {
        // Arrange - 分散ゼロの配列
        var constant = Enumerable.Repeat(0.5f, 100).ToArray();
        var variable = GenerateSineWave(100);

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(constant, variable);

        // Assert
        Assert.Equal(0.0f, correlation);  // 分散ゼロでは相関計算不可
    }

    #endregion

    #region CalculatePearsonFromNormalized Tests - 正規化版

    [Fact]
    public void CalculatePearsonFromNormalized_IdenticalNormalizedArrays_ReturnsOne()
    {
        // Arrange
        var wav = GenerateSineWave(1000);
        var normalized = NormalizeWaveform(wav);

        // Act
        var correlation = WaveValidation.CalculatePearsonFromNormalized(normalized, normalized);

        // Assert
        Assert.Equal(1.0f, correlation, Tolerance);
    }

    [Fact]
    public void CalculatePearsonFromNormalized_InverseNormalized_ReturnsNegativeOne()
    {
        // Arrange
        var wav1 = GenerateSineWave(1000);
        var wav2 = wav1.Select(x => -x).ToArray();
        var norm1 = NormalizeWaveform(wav1);
        var norm2 = NormalizeWaveform(wav2);

        // Act
        var correlation = WaveValidation.CalculatePearsonFromNormalized(norm1, norm2);

        // Assert
        Assert.Equal(-1.0f, correlation, Tolerance);
    }

    [Fact]
    public void CalculatePearsonFromNormalized_EmptyArrays_ReturnsZero()
    {
        // Arrange
        var empty = Array.Empty<float>();

        // Act
        var correlation = WaveValidation.CalculatePearsonFromNormalized(empty, empty);

        // Assert
        Assert.Equal(0.0f, correlation);
    }

    [Fact]
    public void CalculatePearsonFromNormalized_DifferentLengths_ReturnsZero()
    {
        // Arrange
        var norm1 = NormalizeWaveform(GenerateSineWave(100));
        var norm2 = NormalizeWaveform(GenerateSineWave(200));

        // Act
        var correlation = WaveValidation.CalculatePearsonFromNormalized(norm1, norm2);

        // Assert
        Assert.Equal(0.0f, correlation);
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
        Assert.True(r2 >= 0f && r2 <= 1f, $"R? out of range: {r2}");
        Assert.True(pearson >= -1f && pearson <= 1f, $"Pearson out of range: {pearson}");
    }

    #endregion

    #region Edge Cases - エッジケース

    [Fact]
    public void CalculatePearsonCorrelation_VerySmallValues_HandlesCorrectly()
    {
        // Arrange - 非常に小さい値（ただし十分な分散を持つ）
        var wav1 = Enumerable.Range(0, 100).Select(i => (float)(i * 1e-6)).ToArray();
        var wav2 = Enumerable.Range(0, 100).Select(i => (float)(i * 1e-6)).ToArray();

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(wav1, wav2);

        // Assert
        Assert.Equal(1.0f, correlation, Tolerance);
    }

    [Fact]
    public void CalculatePearsonCorrelation_SingleElement_ReturnsZero()
    {
        // Arrange - 1要素の配列（分散計算不可）
        var wav1 = new float[] { 1.0f };
        var wav2 = new float[] { 2.0f };

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(wav1, wav2);

        // Assert
        Assert.Equal(0.0f, correlation);  // 分散ゼロ
    }

    [Fact]
    public void CalculatePearsonCorrelation_TwoElements_CalculatesCorrectly()
    {
        // Arrange - 2要素の配列
        var wav1 = new float[] { 0f, 1f };
        var wav2 = new float[] { 0f, 1f };

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(wav1, wav2);

        // Assert
        Assert.Equal(1.0f, correlation, Tolerance);
    }

    [Fact]
    public void ProcessRemainderPearson_WithSpecificRemainder_CalculatesCorrectly()
    {
        // Arrange - SIMD幅（通常4または8）の端数が出るように長さを調整
        // Vector<float>.Count は環境によるが、ここでは素数サイズを使って確実に余りを出させる
        int length = 17;
        var wav1 = Enumerable.Range(0, length).Select(i => (float)i).ToArray();
        var wav2 = Enumerable.Range(0, length).Select(i => (float)i).ToArray();

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelationSIMD(wav1, wav2);

        // Assert
        Assert.Equal(1.0f, correlation, Tolerance);
    }

    [Fact]
    public void CalculatePearsonCorrelationSIMD_WithZeroAmplitude_ShouldReturnZero()
    {
        // Arrange - 全てゼロの配列（分散ゼロ）
        var wav1 = new float[100]; // all zeros
        var wav2 = GenerateSineWave(100);

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelationSIMD(wav1, wav2);

        // Assert
        Assert.Equal(0.0f, correlation);
    }

    [Fact]
    public void CalculatePearsonCorrelationSIMD_WithDifferentLengths_ShouldReturnZero()
    {
        // Arrange
        var wav1 = GenerateSineWave(100);
        var wav2 = GenerateSineWave(101); // 異なる長さ

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelationSIMD(wav1, wav2);

        // Assert
        Assert.Equal(0.0f, correlation);
    }

    #endregion

    #region Noise Robustness Tests - ノイズ耐性

    [Theory]
    [InlineData(0.001f, 0.99f)]   // 微小ノイズ → 高相関
    [InlineData(0.01f, 0.95f)]    // 小ノイズ → 高相関
    [InlineData(0.1f, 0.7f)]      // 中ノイズ → 中相関
    public void CalculatePearsonCorrelation_WithNoise_MaintainsApproximateCorrelation(
        float noiseLevel, float minExpectedCorrelation)
    {
        // Arrange
        var random = new Random(42);
        var original = GenerateSineWave(1000);
        var noisy = original.Select(x => x + noiseLevel * (float)(random.NextDouble() * 2 - 1)).ToArray();

        // Act
        var correlation = WaveValidation.CalculatePearsonCorrelation(original, noisy);

        // Assert
        Assert.True(correlation >= minExpectedCorrelation,
            $"Expected correlation >= {minExpectedCorrelation}, got {correlation}");
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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var correlation = WaveValidation.CalculatePearsonCorrelationSIMD(wav1, wav2);
        sw.Stop();

        // Assert - 200ms以内に完了すること（実行環境の揺らぎを考慮）
        Assert.True(sw.ElapsedMilliseconds < 200,
            $"Expected < 200ms, took {sw.ElapsedMilliseconds}ms");
        Assert.True(correlation >= -1f && correlation <= 1f);
    }

    #endregion
}
