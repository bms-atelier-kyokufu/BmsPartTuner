using System.Numerics;
using MathNet.Numerics;
using Vector = System.Numerics.Vector;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// 音声波形の類似度を判定するための検証クラス（SIMD最適化版）。
/// 決定係数（R²）およびピアソンの相関係数（ρ）をSIMD（Vector&lt;T&gt;）を用いて高速に計算します。
/// </summary>
public static partial class WaveValidation
{
    /// <summary>
    /// 波形データまたは平均値がほぼ同一とみなす閾値。
    /// </summary>
    private const float IdenticalTolerance = 1e-6f;

    /// <summary>
    /// 分散がゼロであると判定するための閾値。
    /// </summary>
    private const double ZeroVarianceThreshold = 1e-10;

    #region R² (決定係数) 計算

    /// <summary>
    /// 決定係数（R²）を計算（配列版）。
    /// </summary>
    /// <param name="wav1">音声データ配列1。</param>
    /// <param name="wav2">音声データ配列2。</param>
    /// <returns>決定係数（0.0〜1.0）。</returns>
    static public float CalculateRSquared(float[] wav1, float[] wav2)
    {
        if (wav1.Length != wav2.Length || wav1.Length == 0)
            return 0.0F;

        return CalculateRSquaredSIMD(wav1.AsSpan(), wav2.AsSpan());
    }

    /// <summary>
    /// 決定係数（R²）を計算します（Span版・ゼロコピー対応）。
    /// 単一ループで Σx, Σx², Σ(x-y)² を同時にSIMD演算し、高速に処理します。
    /// </summary>
    /// <param name="wav1">音声データSpan1。</param>
    /// <param name="wav2">音声データSpan2。</param>
    /// <returns>決定係数（0.0〜1.0）。</returns>
    static public float CalculateRSquaredSIMD(ReadOnlySpan<float> wav1, ReadOnlySpan<float> wav2)
    {
        if (wav1.Length != wav2.Length || wav1.Length == 0)
            return 0.0F;

        int length = wav1.Length;
        int vectorSize = Vector<float>.Count;
        int vectorizedLength = length - (length % vectorSize);

        (float sumX, float sumX2, float rss) = ProcessVectorized(wav1, wav2, vectorizedLength, vectorSize);

        (sumX, sumX2, rss) = ProcessRemainder(wav1, wav2, vectorizedLength, length, sumX, sumX2, rss);

        double dssd = (double)sumX2 - ((double)sumX * (double)sumX / length);
        float dss = (float)dssd;

        if (dss < ZeroVarianceThreshold)
            return 0.0F;

        float r2 = 1.0F - (rss / dss);

        return Math.Max(0.0F, Math.Min(1.0F, r2));
    }

    /// <summary>
    /// ベクトル化された範囲の処理（R²計算用）。
    /// Vector&lt;T&gt;を使用してSIMD並列演算を実行し、(Σx, Σx², RSS) を返します。
    /// </summary>
    private static (float sumX, float sumX2, float rss) ProcessVectorized(
        ReadOnlySpan<float> wav1,
        ReadOnlySpan<float> wav2,
        int vectorizedLength,
        int vectorSize)
    {
        Vector<float> sumX_vec = Vector<float>.Zero;
        Vector<float> sumX2_vec = Vector<float>.Zero;
        Vector<float> sumDiff2_vec = Vector<float>.Zero;

        for (int i = 0; i < vectorizedLength; i += vectorSize)
        {
            Vector<float> x = new(wav1.Slice(i, vectorSize));
            Vector<float> y = new(wav2.Slice(i, vectorSize));

            Vector<float> diff = x - y;

            sumX_vec += x;
            sumX2_vec += x * x;
            sumDiff2_vec += diff * diff;
        }

        Vector<float> ones = new(1.0f);
        float sumX = Vector.Dot(sumX_vec, ones);
        float sumX2 = Vector.Dot(sumX2_vec, ones);
        float rss = Vector.Dot(sumDiff2_vec, ones);

        return (sumX, sumX2, rss);
    }

    /// <summary>
    /// 端数処理（ベクトル化できなかった残りの要素）（R²計算用）。
    /// </summary>
    /// <returns>(Σx, Σx², RSS)</returns>
    private static (float sumX, float sumX2, float rss) ProcessRemainder(
        ReadOnlySpan<float> wav1,
        ReadOnlySpan<float> wav2,
        int startIndex,
        int length,
        float sumX,
        float sumX2,
        float rss)
    {
        for (int i = startIndex; i < length; i++)
        {
            float x = wav1[i];
            float y = wav2[i];
            float diff = x - y;

            sumX += x;
            sumX2 += x * x;
            rss += diff * diff;
        }

        return (sumX, sumX2, rss);
    }

    #endregion

    #region アライメント計算

    /// <summary>
    /// FFT畳み込み定理を用いたサブミリ秒アライメントのズレ量推定（Phase 2 Measure A）
    /// 事前計算されたFFTスペクトルを利用して高速化します。
    /// </summary>
    public static int CalculateAlignmentOffset(Complex32[] fftShorter, Complex32[] fftLonger)
    {
        return FftAlignmentEngine.CalculateAlignmentOffsetFromCache(fftShorter, fftLonger);
    }

    #endregion
}
