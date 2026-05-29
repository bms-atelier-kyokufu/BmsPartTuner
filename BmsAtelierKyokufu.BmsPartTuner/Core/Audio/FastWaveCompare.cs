using BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;
using System.Numerics;
using static System.Numerics.BitOperations;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// オンメモリキャッシュされた音声データの高速比較クラス。
/// ピアソン相関係数による波形の形状比較を採用し、音量差やDCオフセットに影響されず波形の相似性のみを評価します。
/// ロード時に正規化された波形（平均0、ノルム1）を事前計算することで、比較時はドット積のみで相関係数を算出し高速に処理します。
/// </summary>
[ADRAnchor("OPT-11", nameof(FastWaveCompare))]
[ADRAnchor("M-01", nameof(FastWaveCompare))]
internal static class FastWaveCompare
{
    /// <summary>
    /// キャッシュされた音声データ2個の高速比較を行います。
    /// 事前処理で波形を正規化し、SIMD最適化されたドット積演算によりピアソン相関係数を計算します。
    /// 音量差やDCオフセットに影響されず、類似性を判定します。
    /// </summary>
    /// <param name="data1">比較元の音声データ。</param>
    /// <param name="data2">比較先の音声データ。</param>
    /// <param name="threshold">ピアソン相関係数のしきい値（0.0-1.0）。</param>
    /// <returns>類似している場合true。</returns>
    public static bool IsMatch(ICachedSoundData data1, ICachedSoundData data2, float threshold)
    {
        if (data1.SampleRate != data2.SampleRate ||
            data1.Channels != data2.Channels ||
            data1.BitsPerSample != data2.BitsPerSample)
        {
            return false;
        }
        var activeRegions1 = data1.GetActiveRegions();
        var activeRegions2 = data2.GetActiveRegions();

        if (activeRegions1 == null || activeRegions2 == null || activeRegions1.Length == 0 || activeRegions2.Length == 0)
        {

            return false;
        }


        // Check both channels for total silence
        bool isData1Silent = true;
        bool isData2Silent = true;
        for (int ch = 0; ch < activeRegions1.Length && ch < data1.Channels; ch++)
        {
            if (activeRegions1[ch]?.Count > 0) isData1Silent = false;
        }
        for (int ch = 0; ch < activeRegions2.Length && ch < data2.Channels; ch++)
        {
            if (activeRegions2[ch]?.Count > 0) isData2Silent = false;
        }

        // If both are entirely silent
        if (isData1Silent && isData2Silent)
        {
            return true;
        }
        // If only one is entirely silent
        if (isData1Silent || isData2Silent)
        {
            return false;
        }

        // Length Heuristic: 既に末尾が無音トリム済みであるという前提に立ち、
        // 長さ（フレーム数）が 0.1秒 以上異なる場合は、仮に部分一致しても
        // 最終的に「はみ出た部分に音が鳴っている」として弾かれるため、重い計算をスキップする。
        int frames1 = data1.TotalSamples / data1.Channels;
        int frames2 = data2.TotalSamples / data2.Channels;
        int lengthDiffThreshold = (int)(data1.SampleRate * 0.1); // 0.1秒の許容誤差

        if (Math.Abs(frames1 - frames2) > lengthDiffThreshold)
        {
            return false;
        }

        // SimHash256 Cascade Classifier (Heuristic Hamming Distance Threshold: 64)
        if (data1.SimHash256 != null && data2.SimHash256 != null)
        {
            var s1 = data1.SimHash256;
            var s2 = data2.SimHash256;

            // ループアンローリング（展開）による分岐命令の排除とパイプラインの最適化
            int hammingDistance =
                BitOperations.PopCount(s1[0] ^ s2[0]) +
                BitOperations.PopCount(s1[1] ^ s2[1]) +
                BitOperations.PopCount(s1[2] ^ s2[2]) +
                BitOperations.PopCount(s1[3] ^ s2[3]);

            if (hammingDistance > 64)
            {
                return false;
            }
        }

        // Spectral Features Cascade Classifier (Heuristic Threshold: 0.88f)
        if (data1.SpectralFeatures != null && data2.SpectralFeatures != null)
        {
            float distSq = 0;
            var v1 = data1.SpectralFeatures;
            var v2 = data2.SpectralFeatures;
            for (int i = 0; i < 16; i++)
            {
                float diff = v1[i] - v2[i];
                distSq += diff * diff;
            }
            // 0.88f ^ 2 = 0.7744f
            if (distSq > 0.7744f)
            {
                return false;
            }
        }

        // Find first active channel to compute Pearson on (usually 0, but could be 1 if left is silent)
        int targetChannel = 0;
        if (activeRegions1[0] == null || activeRegions1[0].Count == 0)
        {
            targetChannel = 1;
        }

        var shorter = data1.TotalSamples < data2.TotalSamples ? data1 : data2;
        var longer = data1.TotalSamples < data2.TotalSamples ? data2 : data1;

        var shorterFrames = shorter.TotalSamples / shorter.Channels;
        var longerFrames = longer.TotalSamples / longer.Channels;

        var shorterSpan = shorter.GetRawSpan(targetChannel, 0, shorterFrames);
        var longerFullSpan = longer.GetRawSpan(targetChannel, 0, longerFrames);

        var correlation = CalculateMaxCorrelation(shorter, longer, targetChannel, shorterFrames, longerFrames, shorterSpan, longerFullSpan, out int offset);

        if (correlation >= threshold && shorterFrames < longerFrames)
        {
            int overlapStart = offset >= 0 ? offset : 0;
            int overlapEnd = offset >= 0 ? (offset + shorterFrames) : (shorterFrames + offset);
            if (overlapEnd > longerFrames) overlapEnd = longerFrames;

            double nonOverlapSumSq = 0;
            int nonOverlapCount = 0;
            for (int i = 0; i < overlapStart; i++)
            {
                float val = longerFullSpan[i];
                nonOverlapSumSq += val * val;
                nonOverlapCount++;
            }
            for (int i = overlapEnd; i < longerFrames; i++)
            {
                float val = longerFullSpan[i];
                nonOverlapSumSq += val * val;
                nonOverlapCount++;
            }

            if (nonOverlapCount > 0)
            {
                double nonOverlapRms = Math.Sqrt(nonOverlapSumSq / nonOverlapCount);
                if (nonOverlapRms > AppConstants.AudioComparison.SilenceRmsThreshold)
                {
                    return false;
                }
            }
        }

        return correlation >= threshold;
    }

    /// <summary>
    /// 最適なアライメント（位相ズレ補正）を加味した上での最大ピアソン相関係数を計算します。
    /// </summary>
    public static float CalculateMaxCorrelation(
        ICachedSoundData shorter, ICachedSoundData longer,
        int targetChannel,
        int shorterFrames, int longerFrames,
        ReadOnlySpan<float> shorterSpan, ReadOnlySpan<float> longerFullSpan,
        out int offset)
    {
        offset = 0;
        if (shorter.FftSpectrum != null && longer.FftSpectrum != null &&
            shorter.FftSpectrum[targetChannel] != null && longer.FftSpectrum[targetChannel] != null)
        {
            offset = WaveValidation.CalculateAlignmentOffset(shorter.FftSpectrum[targetChannel], longer.FftSpectrum[targetChannel]);
        }
        else
        {
            offset = FftAlignmentEngine.CalculateAlignmentOffset(shorterSpan, longerFullSpan);
        }

        float correlation;
        if (shorterFrames == longerFrames && offset == 0)
        {
            correlation = WaveValidation.CalculatePearsonCorrelationSIMD(shorterSpan, longerFullSpan);
        }
        else
        {
            float[] paddedShorter = System.Buffers.ArrayPool<float>.Shared.Rent(longerFrames);
            try
            {
                Array.Clear(paddedShorter, 0, longerFrames);
                if (offset >= 0)
                {
                    if (offset + shorterFrames > longerFrames) offset = 0;
                    shorterSpan.CopyTo(paddedShorter.AsSpan(offset, shorterFrames));
                }
                else
                {
                    int absOffset = -offset;
                    if (absOffset >= shorterFrames) absOffset = 0;
                    int compareLen = shorterFrames - absOffset;
                    shorterSpan.Slice(absOffset, compareLen).CopyTo(paddedShorter.AsSpan(0, compareLen));
                }
                correlation = WaveValidation.CalculatePearsonCorrelationSIMD(paddedShorter.AsSpan(0, longerFrames), longerFullSpan);
            }
            finally
            {
                System.Buffers.ArrayPool<float>.Shared.Return(paddedShorter);
            }
        }
        return correlation;
    }

    /// <summary>
    /// デバッグやベンチマーク用に類似度スコア（ピアソン相関係数）を取得します。
    /// 演算効率の比較、デバッグ時の相関係数確認、閾値調整のための統計収集に使用されます。
    /// </summary>
    /// <param name="data1">比較元の音声データ。</param>
    /// <param name="data2">比較先の音声データ。</param>
    /// <returns>ピアソン相関係数（-1.0〜1.0）、フォーマット不一致時は0.0。</returns>
    public static float GetCorrelation(ICachedSoundData data1, ICachedSoundData data2)
    {
        if (data1.SampleRate != data2.SampleRate ||
            data1.Channels != data2.Channels ||
            data1.BitsPerSample != data2.BitsPerSample)
        {
            return 0.0f;
        }

        if (data1.TotalSamples != data2.TotalSamples) return 0.0f;

        var activeRegions1 = data1.GetActiveRegions();
        var activeRegions2 = data2.GetActiveRegions();

        if (activeRegions1 != null && activeRegions2 != null && activeRegions1.Length > 0 && activeRegions2.Length > 0)
        {
            var regions1 = activeRegions1[0];
            var regions2 = activeRegions2[0];

            // If both are entirely silent
            if ((regions1 == null || regions1.Count == 0) && (regions2 == null || regions2.Count == 0))
            {
                return 1.0f;
            }
            // If only one is entirely silent
            if (regions1 == null || regions1.Count == 0 || regions2 == null || regions2.Count == 0)
            {
                return 0.0f;
            }

            return WaveValidation.CalculatePearsonForCachedDataSIMD(data1, data2, 0);
        }

        return 0.0f;
    }
}
