using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GenerateSimdBatchUnrollAttribute = BmsAtelierKyokufu.BmsPartTuner.Core.Attributes.GenerateSimdBatchUnrollAttribute;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

public static partial class WaveValidation
{
    #region ピアソンの相関係数計算 (アクティブ区間の重なり)

    /// <summary>
    /// Phase 2: 正規化済み波形のドット積でピアソン相関係数を高速に計算します（配列版）。
    /// </summary>
    static public float CalculatePearsonFromNormalized(float[] normalizedWav1, float[] normalizedWav2)
    {
        if (normalizedWav1.Length != normalizedWav2.Length || normalizedWav1.Length == 0)
            return 0.0F;

        return CalculatePearsonFromNormalizedSIMD(normalizedWav1.AsSpan(), normalizedWav2.AsSpan());
    }

    /// <summary>
    /// Phase 2: 正規化済み波形のドット積でピアソン相関係数を高速に計算します（Span版・SIMD最適化）。
    /// </summary>
    [GenerateSimdBatchUnroll(UnrollFactor = 4, LogicType = "PearsonNormalized")]
    public static partial float CalculatePearsonFromNormalizedSIMD(ReadOnlySpan<float> normalizedWav1, ReadOnlySpan<float> normalizedWav2);

    /// <summary>
    /// 2つのSpanのドット積をSIMD（ループアンロール）を用いて高速に計算します。
    /// </summary>
    [GenerateSimdBatchUnroll(UnrollFactor = 4, LogicType = "DotProduct")]
    private static partial float CalculateDotProductSIMD(ReadOnlySpan<float> span1, ReadOnlySpan<float> span2);

    /// <summary>
    /// Phase 2: キャッシュされた波形（事前正規化または生データ）の有音区間（ActiveRegion）の重なりを用いてピアソン相関係数を計算します（SIMD最適化）。
    /// 事前計算や無音区間のスキップにより、極めて高速かつ省メモリに処理を行います。
    /// </summary>
    static public float CalculatePearsonForCachedDataSIMD(ICachedSoundData data1, ICachedSoundData data2, int channel = 0)
    {
        // 1. 符号ビットLSHによる O(1) 事前棄却（Early Pruning）
        if (TryEarlyPruning(data1, data2, channel))
            return 0.0f;

        var regions1 = data1.GetActiveRegions()[channel];
        var regions2 = data2.GetActiveRegions()[channel];

        if (regions1 == null || regions2 == null || regions1.Count == 0 || regions2.Count == 0)
            return 0.0F;

        PearsonOverlapStats stats = default;

        AccumulateOverlapStats(data1, data2, channel, regions1, regions2, ref stats);

        if (stats.TotalN == 0) return 0.0f;

        return ComputeFinalCorrelation(data1, data2, channel, ref stats);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryEarlyPruning(ICachedSoundData data1, ICachedSoundData data2, int channel)
    {
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

                ref ulong lsh1Ref = ref MemoryMarshal.GetReference(lsh1);
                ref ulong lsh2Ref = ref MemoryMarshal.GetReference(lsh2);
                ref ulong mask1Ref = ref MemoryMarshal.GetReference(mask1);
                ref ulong mask2Ref = ref MemoryMarshal.GetReference(mask2);

                for (int k = 0; k < checkLen; k++)
                {
                    ulong validMask = Unsafe.Add(ref mask1Ref, k) & Unsafe.Add(ref mask2Ref, k);
                    ulong xor = (Unsafe.Add(ref lsh1Ref, k) ^ Unsafe.Add(ref lsh2Ref, k)) & validMask;
                    diffCount += BitOperations.PopCount(xor);
                    validCount += BitOperations.PopCount(validMask);
                }

                if (validCount > 64 && diffCount > validCount * 0.3)
                {
                    return true;
                }
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateOverlapStats(
        ICachedSoundData data1,
        ICachedSoundData data2,
        int channel,
        IReadOnlyList<ActiveRegion> regions1,
        IReadOnlyList<ActiveRegion> regions2,
        ref PearsonOverlapStats stats)
    {
        if (data1 is PreNormalizedSoundData && data2 is PreNormalizedSoundData)
        {
            AccumulateOverlapStatsPrePre(regions1, regions2, ref stats);
        }
        else if (data1 is PointerSoundData ptr1 && data2 is PointerSoundData ptr2)
        {
            AccumulateOverlapStatsPtrPtr(ptr1, ptr2, channel, regions1, regions2, ref stats);
        }
        else
        {
            AccumulateOverlapStatsGeneric(data1, data2, channel, regions1, regions2, ref stats);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateOverlapStatsPrePre(
        IReadOnlyList<ActiveRegion> regions1,
        IReadOnlyList<ActiveRegion> regions2,
        ref PearsonOverlapStats stats)
    {
        int i = 0, j = 0;
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

                stats.TotalN += len;

                ReadOnlySpan<float> span1 = new(r1.Data, offset1, len);
                ReadOnlySpan<float> span2 = new(r2.Data, offset2, len);

                stats.TotalDotProduct += CalculateDotProductSIMD(span1, span2);
            }

            if (r1.Offset + r1.Length < r2.Offset + r2.Length)
                i++;
            else
                j++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateOverlapStatsPtrPtr(
        PointerSoundData data1,
        PointerSoundData data2,
        int channel,
        IReadOnlyList<ActiveRegion> regions1,
        IReadOnlyList<ActiveRegion> regions2,
        ref PearsonOverlapStats stats)
    {
        int i = 0, j = 0;
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

                stats.TotalSumX += data1.GetRangeSum(channel, r1.Offset + offset1, len);
                stats.TotalSumX2 += data1.GetRangeSumSq(channel, r1.Offset + offset1, len);
                stats.TotalSumY += data2.GetRangeSum(channel, r2.Offset + offset2, len);
                stats.TotalSumY2 += data2.GetRangeSumSq(channel, r2.Offset + offset2, len);
                stats.TotalN += len;

                ReadOnlySpan<float> span1 = data1.GetRawSpan(channel, r1.Offset + offset1, len);
                ReadOnlySpan<float> span2 = data2.GetRawSpan(channel, r2.Offset + offset2, len);

                stats.TotalDotProduct += CalculateDotProductSIMD(span1, span2);
            }

            if (r1.Offset + r1.Length < r2.Offset + r2.Length)
                i++;
            else
                j++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateOverlapStatsGeneric(
        ICachedSoundData data1,
        ICachedSoundData data2,
        int channel,
        IReadOnlyList<ActiveRegion> regions1,
        IReadOnlyList<ActiveRegion> regions2,
        ref PearsonOverlapStats stats)
    {
        bool isPre1 = data1.IsPreNormalized;
        bool isPre2 = data2.IsPreNormalized;

        int i = 0, j = 0;
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

                if (!isPre1 && data1 is IAudioStatisticalData stat1)
                {
                    stats.TotalSumX += stat1.GetRangeSum(channel, r1.Offset + offset1, len);
                    stats.TotalSumX2 += stat1.GetRangeSumSq(channel, r1.Offset + offset1, len);
                }
                if (!isPre2 && data2 is IAudioStatisticalData stat2)
                {
                    stats.TotalSumY += stat2.GetRangeSum(channel, r2.Offset + offset2, len);
                    stats.TotalSumY2 += stat2.GetRangeSumSq(channel, r2.Offset + offset2, len);
                }
                stats.TotalN += len;

                ReadOnlySpan<float> span1 = isPre1
                    ? new ReadOnlySpan<float>(r1.Data, offset1, len)
                    : data1.GetRawSpan(channel, r1.Offset + offset1, len);

                ReadOnlySpan<float> span2 = isPre2
                    ? new ReadOnlySpan<float>(r2.Data, offset2, len)
                    : data2.GetRawSpan(channel, r2.Offset + offset2, len);

                stats.TotalDotProduct += CalculateDotProductSIMD(span1, span2);
            }

            if (r1.Offset + r1.Length < r2.Offset + r2.Length)
                i++;
            else
                j++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ComputeFinalCorrelation(
        ICachedSoundData data1,
        ICachedSoundData data2,
        int channel,
        ref PearsonOverlapStats stats)
    {
        if (data1.IsPreNormalized && data2.IsPreNormalized)
        {
            return (float)Math.Max(-1.0, Math.Min(1.0, stats.TotalDotProduct));
        }
        else if (data1.IsPreNormalized != data2.IsPreNormalized)
        {
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
            if (varSumY <= ZeroVarianceThreshold) return 0.0f;

            double correlation = stats.TotalDotProduct / Math.Sqrt(varSumY);
            return (float)Math.Max(-1.0, Math.Min(1.0, correlation));
        }
        else
        {
            int fileN = data1.TotalSamples / data1.Channels;
            if (fileN == 0) return 0.0f;

            double fullSumX = data1 is IAudioStatisticalData fs1 ? fs1.GetChannelSum(channel) : 0;
            double fullSumY = data2 is IAudioStatisticalData fs2 ? fs2.GetChannelSum(channel) : 0;
            double fullSumX2 = data1 is IAudioStatisticalData fs3 ? fs3.GetChannelSumSq(channel) : 0;
            double fullSumY2 = data2 is IAudioStatisticalData fs4 ? fs4.GetChannelSumSq(channel) : 0;

            double meanX = fullSumX / fileN;
            double meanY = fullSumY / fileN;

            double covXY = (stats.TotalDotProduct / fileN) - (meanX * meanY);
            double varX = (fullSumX2 / fileN) - (meanX * meanX);
            double varY = (fullSumY2 / fileN) - (meanY * meanY);

            if (varX < ZeroVarianceThreshold || varY < ZeroVarianceThreshold) return 0.0f;

            double correlation = covXY / Math.Sqrt(varX * varY);
            return (float)Math.Max(-1.0, Math.Min(1.0, correlation));
        }
    }

    #endregion
}
