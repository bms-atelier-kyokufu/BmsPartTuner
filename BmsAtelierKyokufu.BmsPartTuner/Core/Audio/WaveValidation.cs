using System;
using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using GenerateSimdBatchUnrollAttribute = BmsAtelierKyokufu.BmsPartTuner.Core.Attributes.GenerateSimdBatchUnrollAttribute;
using Vector = System.Numerics.Vector;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// 音声波形の類似度を判定するための検証クラス（SIMD最適化版）。
/// 決定係数（R²）およびピアソンの相関係数（ρ）をSIMD（Vector&lt;T&gt;）を用いて高速に計算します。
/// </summary>
public static partial class WaveValidation
{
    #region パブリックメソッド

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

        if (dss < 1e-10f)
            return 0.0F;

        float r2 = 1.0F - (rss / dss);

        return Math.Max(0.0F, Math.Min(1.0F, r2));
    }

    /// <summary>
    /// FFT畳み込み定理を用いたサブミリ秒アライメントのズレ量推定（Phase 2 Measure A）
    /// 事前計算されたFFTスペクトルを利用して高速化します。
    /// </summary>
    public static int CalculateAlignmentOffset(Complex32[] fftShorter, Complex32[] fftLonger)
    {
        return FftAlignmentEngine.CalculateAlignmentOffsetFromCache(fftShorter, fftLonger);
    }

    /// <summary>
    /// 波形1と波形2のピアソンの相関係数（ρ）をSIMDで計算します（配列版）。
    /// 音量（スケール）の違いに強く、波形の「形状」の相似性を評価します。
    /// </summary>
    /// <param name="wav1">音声データ配列1。</param>
    /// <param name="wav2">音声データ配列2。</param>
    /// <returns>相関係数（-1.0〜1.0、通常は0.0〜1.0）。</returns>
    static public float CalculatePearsonCorrelation(float[] wav1, float[] wav2)
    {
        if (wav1.Length != wav2.Length || wav1.Length == 0)
            return 0.0F;

        return CalculatePearsonCorrelationSIMD(wav1.AsSpan(), wav2.AsSpan());
    }

    /// <summary>
    /// ピアソンの相関係数を計算します（Span版・ゼロコピー対応・SIMD最適化・1パス計算）。
    /// 1パスでΣx, Σy, Σx², Σy², Σxyを計算し、高速に処理します。
    /// </summary>
    /// <param name="wav1">音声データSpan1。</param>
    /// <param name="wav2">音声データSpan2。</param>
    /// <returns>相関係数（-1.0〜1.0、通常は0.0〜1.0）。</returns>
    static public float CalculatePearsonCorrelationSIMD(ReadOnlySpan<float> wav1, ReadOnlySpan<float> wav2)
    {
        if (wav1.Length != wav2.Length || wav1.Length == 0)
            return 0.0F;

        int length = wav1.Length;

        // Guard for minimal data: if data is too short and identical, return 1.0 immediately
        // This prevents NaN/0.0 results from variance calculation when both arrays have zero variance
        if (length < 4)
        {
            // Check if data is identical at binary level
            bool identical = true;
            for (int i = 0; i < length; i++)
            {
                if (Math.Abs(wav1[i] - wav2[i]) > 1e-6f)
                {
                    identical = false;
                    break;
                }
            }
            if (identical) return 1.0F;
        }

        int vectorSize = Vector<float>.Count;
        int vectorizedLength = length - (length % vectorSize);

        ProcessVectorizedPearson(wav1, wav2, vectorizedLength, vectorSize,
            out float sumX, out float sumY, out float sumX2, out float sumY2, out float sumXY);

        (sumX, sumY, sumX2, sumY2, sumXY) = ProcessRemainderPearson(
            wav1, wav2, vectorizedLength, length, sumX, sumY, sumX2, sumY2, sumXY);

        double meanX = sumX / length;
        double meanY = sumY / length;

        double covXY = (sumXY / length) - (meanX * meanY);

        double varX = (sumX2 / length) - (meanX * meanX);
        double varY = (sumY2 / length) - (meanY * meanY);

        // If both variances are near zero, check if data is identical
        if (varX < 1e-10 && varY < 1e-10)
        {
            // Both arrays have zero variance - they are constant values
            // If the constant values are the same, correlation is 1.0
            // Check the mean values
            if (Math.Abs(meanX - meanY) < 1e-6)
                return 1.0F;
            else
                return 0.0F;
        }

        if (varX < 1e-10 || varY < 1e-10)
            return 0.0F;

        double stdDevX = Math.Sqrt(varX);
        double stdDevY = Math.Sqrt(varY);
        double correlation = covXY / (stdDevX * stdDevY);

        return (float)Math.Max(-1.0, Math.Min(1.0, correlation));
    }

    /// <summary>
    /// Phase 2: 正規化済み波形のドット積でピアソン相関係数を高速に計算します（配列版）。
    /// </summary>
    /// <param name="normalizedWav1">正規化済み波形1。</param>
    /// <param name="normalizedWav2">正規化済み波形2。</param>
    /// <returns>相関係数（-1.0〜1.0）。</returns>
    static public float CalculatePearsonFromNormalized(float[] normalizedWav1, float[] normalizedWav2)
    {
        if (normalizedWav1.Length != normalizedWav2.Length || normalizedWav1.Length == 0)
            return 0.0F;

        return CalculatePearsonFromNormalizedSIMD(normalizedWav1.AsSpan(), normalizedWav2.AsSpan());
    }

    /// <summary>
    /// Phase 2: 正規化済み波形のドット積でピアソン相関係数を高速に計算します（Span版・SIMD最適化）。
    /// </summary>
    /// <param name="normalizedWav1">正規化済み波形Span1。</param>
    /// <param name="normalizedWav2">正規化済み波形Span2。</param>
    /// <returns>相関係数（-1.0〜1.0）。</returns>
    [GenerateSimdBatchUnroll(UnrollFactor = 4, LogicType = "PearsonNormalized")]
    public static partial float CalculatePearsonFromNormalizedSIMD(ReadOnlySpan<float> normalizedWav1, ReadOnlySpan<float> normalizedWav2);



    /// <summary>
    /// Phase 2: キャッシュされた波形（事前正規化または生データ）の有音区間（ActiveRegion）の重なりを用いてピアソン相関係数を計算します（SIMD最適化）。
    /// 事前計算や無音区間のスキップにより、極めて高速かつ省メモリに処理を行います。
    /// </summary>
    static public float CalculatePearsonForCachedDataSIMD(BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData data1, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData data2, int channel = 0)
    {
        // 1. 符号ビットLSHによる O(1) 事前棄却（Early Pruning）
        if (data1 is IAudioStatisticalData statData1 && data2 is IAudioStatisticalData statData2)
        {
            var lsh1 = statData1.GetLsh(channel);
            var lsh2 = statData2.GetLsh(channel);
            var mask1 = statData1.GetLshMask(channel);
            var mask2 = statData2.GetLshMask(channel);

            if (!lsh1.IsEmpty && !lsh2.IsEmpty)
            {
                int checkLen = Math.Min(lsh1.Length, lsh2.Length);
                int diffCount = 0;
                int validCount = 0;

                for (int k = 0; k < checkLen; k++)
                {
                    ulong validMask = mask1[k] & mask2[k]; // 両方とも有効な波形であるビットのみ
                    if (validMask != 0)
                    {
                        ulong xor = (lsh1[k] ^ lsh2[k]) & validMask;
                        diffCount += System.Numerics.BitOperations.PopCount(xor);
                        validCount += System.Numerics.BitOperations.PopCount(validMask);
                    }
                }

                // 有効なビット数が十分にあり、かつ30%以上符号が異なっていれば明らかに別物として棄却
                if (validCount > 64 && diffCount > validCount * 0.3)
                {
                    return 0.0f;
                }
            }
        }

        var regions1 = data1.GetActiveRegions()[channel];
        var regions2 = data2.GetActiveRegions()[channel];

        if (regions1 == null || regions2 == null || regions1.Count == 0 || regions2.Count == 0)
            return 0.0F;

        int i = 0, j = 0;
        double totalDotProduct = 0;

        // 累積和用の変数
        double totalSumX = 0, totalSumY = 0;
        double totalSumX2 = 0, totalSumY2 = 0;
        int totalN = 0;

        int vectorSize = Vector<float>.Count;
        Vector<float> ones = new(1.0f);

        while (i < regions1.Count && j < regions2.Count)
        {
            var r1 = regions1[i];
            var r2 = regions2[j];

            int overlapStart = Math.Max(r1.Offset, r2.Offset);
            int overlapEnd = Math.Min(r1.Offset + r1.Length, r2.Offset + r2.Length);

            if (overlapStart < overlapEnd)
            {
                int len = overlapEnd - overlapStart;
                int offset1 = overlapStart - r1.Offset;
                int offset2 = overlapStart - r2.Offset;

                // 累積和の加算 (O(1)で交差範囲のみ取得)
                if (!data1.IsPreNormalized && data1 is IAudioStatisticalData stat1)
                {
                    totalSumX += stat1.GetRangeSum(channel, r1.Offset + offset1, len);
                    totalSumX2 += stat1.GetRangeSumSq(channel, r1.Offset + offset1, len);
                }
                if (!data2.IsPreNormalized && data2 is IAudioStatisticalData stat2)
                {
                    totalSumY += stat2.GetRangeSum(channel, r2.Offset + offset2, len);
                    totalSumY2 += stat2.GetRangeSumSq(channel, r2.Offset + offset2, len);
                }
                totalN += len;

                ReadOnlySpan<float> span1 = data1.IsPreNormalized
                    ? new ReadOnlySpan<float>(r1.Data, offset1, len)
                    : data1.GetRawSpan(channel, r1.Offset + offset1, len);

                ReadOnlySpan<float> span2 = data2.IsPreNormalized
                    ? new ReadOnlySpan<float>(r2.Data, offset2, len)
                    : data2.GetRawSpan(channel, r2.Offset + offset2, len);

                int vectorizedLength = len - (len % vectorSize);
                Vector<float> dotProduct_vec = Vector<float>.Zero;

                for (int k = 0; k < vectorizedLength; k += vectorSize)
                {
                    Vector<float> x = new(span1.Slice(k, vectorSize));
                    Vector<float> y = new(span2.Slice(k, vectorSize));
                    dotProduct_vec += x * y;
                }

                double dotProduct = Vector.Dot(dotProduct_vec, ones);

                for (int k = vectorizedLength; k < len; k++)
                {
                    dotProduct += span1[k] * span2[k];
                }

                totalDotProduct += dotProduct;
            }

            // 終了が早い方のポインタを進める
            if (r1.Offset + r1.Length < r2.Offset + r2.Length)
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        if (totalN == 0) return 0.0f;

        if (data1.IsPreNormalized && data2.IsPreNormalized)
        {
            // 事前正規化方式：ドット積がそのまま相関係数となる
            return (float)Math.Max(-1.0, Math.Min(1.0, totalDotProduct));
        }
        else if (data1.IsPreNormalized != data2.IsPreNormalized)
        {
            // 混合方式（Mixed-SIMD）: 正規化(X) と 生データ(Y) の直接比較
            // r = D / sqrt(ΣY² - (ΣY)²/N)
            int fileN = data1.IsPreNormalized ? (data2.TotalSamples / data2.Channels) : (data1.TotalSamples / data1.Channels);
            if (fileN == 0) return 0.0f;

            double sumY = 0;
            double sumY2 = 0;
            if (data1.IsPreNormalized && data2 is IAudioStatisticalData s2)
            {
                sumY = s2.GetChannelSum(channel);
                sumY2 = s2.GetChannelSumSq(channel);
            }
            else if (!data1.IsPreNormalized && data1 is IAudioStatisticalData s1)
            {
                sumY = s1.GetChannelSum(channel);
                sumY2 = s1.GetChannelSumSq(channel);
            }

            double varSumY = sumY2 - (sumY * sumY / fileN);
            if (varSumY <= 1e-10) return 0.0f;

            double correlation = totalDotProduct / Math.Sqrt(varSumY);
            return (float)Math.Max(-1.0, Math.Min(1.0, correlation));
        }
        else
        {
            // ポインタ方式（生データ同士）：生データの積和 (Σxy) から1パス用の補正計算を行う
            int fileN = data1.TotalSamples / data1.Channels;
            if (fileN == 0) return 0.0f;

            double fullSumX = data1 is IAudioStatisticalData fs1 ? fs1.GetChannelSum(channel) : 0;
            double fullSumY = data2 is IAudioStatisticalData fs2 ? fs2.GetChannelSum(channel) : 0;
            double fullSumX2 = data1 is IAudioStatisticalData fs3 ? fs3.GetChannelSumSq(channel) : 0;
            double fullSumY2 = data2 is IAudioStatisticalData fs4 ? fs4.GetChannelSumSq(channel) : 0;

            double meanX = fullSumX / fileN;
            double meanY = fullSumY / fileN;

            double covXY = (totalDotProduct / fileN) - (meanX * meanY);
            double varX = (fullSumX2 / fileN) - (meanX * meanX);
            double varY = (fullSumY2 / fileN) - (meanY * meanY);

            if (varX < 1e-10 || varY < 1e-10) return 0.0f;

            double correlation = covXY / Math.Sqrt(varX * varY);
            return (float)Math.Max(-1.0, Math.Min(1.0, correlation));
        }
    }

    #endregion

    #region プライベートメソッド

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

    /// <summary>
    /// ベクトル化された範囲の処理（ピアソン相関係数計算用）。
    /// Vector&lt;T&gt;を使用してSIMD並列演算を実行し、(ΣX, ΣY, ΣX², ΣY², ΣXY) を返します。
    /// </summary>
    [BmsAtelierKyokufu.BmsPartTuner.Core.Attributes.GenerateSimdBatchUnroll(UnrollFactor = 4, LogicType = "PearsonStats")]
    private static partial void ProcessVectorizedPearson(
        ReadOnlySpan<float> wav1,
        ReadOnlySpan<float> wav2,
        int vectorizedLength,
        int vectorSize,
        out float sumX, out float sumY, out float sumX2, out float sumY2, out float sumXY);


    /// <summary>
    /// 端数処理（ベクトル化できなかった残りの要素）（ピアソン相関係数計算用）。
    /// </summary>
    /// <returns>(ΣX, ΣY, ΣX², ΣY², ΣXY)</returns>
    private static (float sumX, float sumY, float sumX2, float sumY2, float sumXY) ProcessRemainderPearson(
        ReadOnlySpan<float> wav1,
        ReadOnlySpan<float> wav2,
        int startIndex,
        int length,
        float sumX,
        float sumY,
        float sumX2,
        float sumY2,
        float sumXY)
    {
        for (int i = startIndex; i < length; i++)
        {
            float x = wav1[i];
            float y = wav2[i];

            sumX += x;
            sumY += y;
            sumX2 += x * x;
            sumY2 += y * y;
            sumXY += x * y;
        }

        return (sumX, sumY, sumX2, sumY2, sumXY);
    }

    #endregion
}
