using System.Numerics;
using System.Runtime.CompilerServices;

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
    private const float MismatchScore = -2.0f;
    private const float SilenceMatchScore = 2.0f;

    /// <summary>
    /// 音響SimHash(256bit)のハミング距離の許容しきい値。
    /// 実測値(DiscoverHeuristicThreshold_SimHash256_R2_Relationshipテスト)に基づき、
    /// 相関係数R >= 0.40 となる類似波形ペアがこの値(64)を超えない統計的境界から決定。
    /// </summary>
    private const int SimHashHammingThreshold = 64;

    /// <summary>
    /// L2正規化された16次元パワースペクトル距離の二乗しきい値。
    /// 実測された最大距離(約0.58)に50%の安全マージンを加えた値(0.88)の二乗値(0.7744f)。
    /// 平方根計算(Math.Sqrt)を回避してO(1)で高速判定するための二乗値での定義。
    /// </summary>
    private const float SpectralDistanceSquaredThreshold = 0.7744f; // 0.88f ^ 2
    private const float PerfectCorrelation = 1.0f;
    private const float ZeroCorrelation = 0.0f;
    private const int LChannel = 0;
    private const int RChannel = 1;

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
        // 1. キャッシュキーの構築（順序に依存しないペアキー）とキャッシュ探索
        string name1 = data1.FilePath;
        string name2 = data2.FilePath;
        bool canCache = !string.IsNullOrEmpty(name1) && !string.IsNullOrEmpty(name2);
        var key = canCache ? (string.CompareOrdinal(name1, name2) < 0 ? (name1, name2) : (name2, name1)) : default;

        if (canCache && AudioRegistry.Instance.CorrelationCache.TryGetValue(key, out float cachedCorr))
        {
            return cachedCorr >= threshold;
        }

        // キャッシュ書き込みを伴う判定失敗時のユーティリティローカル関数
        bool ReturnMismatch()
        {
            if (canCache) AudioRegistry.Instance.CorrelationCache[key] = MismatchScore;
            return false;
        }

        // 2. 音声フォーマット（サンプリングレート、チャンネル数、ビット深度）の同一性検証
        if (!HasCompatibleFormat(data1, data2))
        {
            return ReturnMismatch();
        }

        // 3. 有効波形領域（ActiveRegions）の検証
        var activeRegions1 = data1.GetActiveRegions();
        var activeRegions2 = data2.GetActiveRegions();

        if (activeRegions1 == null || activeRegions2 == null || activeRegions1.Length == 0 || activeRegions2.Length == 0)
        {
            return ReturnMismatch();
        }

        // 4. 無音判定（片方または両方が完全に無音である場合の一括判定）
        if (TryCheckSilenceMatch(data1, data2, activeRegions1, activeRegions2, out bool isSilenceMatch))
        {
            if (canCache) AudioRegistry.Instance.CorrelationCache[key] = isSilenceMatch ? SilenceMatchScore : MismatchScore;
            return isSilenceMatch;
        }

        // 5. 高速絞り込み（カスケード分類器群による先行枝刈り）
        // 許容を超える長さの差、SimHash距離、またはスペクトル特徴の乖離がある場合は、重い相関演算を行わずに弾く
        if (ExceedsLengthDifference(data1, data2) ||
            IsMismatchedBySimHash(data1, data2) ||
            IsMismatchedBySpectralFeatures(data1, data2))
        {
            return ReturnMismatch();
        }

        // 6. ピアソン相関係数を算出するための準備（短い方を基準に長い方の部分波形と比較）
        // Lチャンネル(0)が有効ならLチャンネル、なければRチャンネル(1)を選択する
        // 片方のチャンネルのみ評価する。通常BMSでは両方同じような音がなるので処理を省く
        int targetChannel = (activeRegions1[LChannel] == null || activeRegions1[LChannel].Count == 0) ? RChannel : LChannel;

        var shorter = data1.TotalSamples < data2.TotalSamples ? data1 : data2;
        var longer = data1.TotalSamples < data2.TotalSamples ? data2 : data1;

        var shorterFrames = shorter.TotalSamples / shorter.Channels;
        var longerFrames = longer.TotalSamples / longer.Channels;

        var shorterSpan = shorter.GetRawSpan(targetChannel, 0, shorterFrames);
        var longerFullSpan = longer.GetRawSpan(targetChannel, 0, longerFrames);

        // 7. アライメント（位相ズレ補正）を加味した最大ピアソン相関係数の計算
        var parameters = new WaveComparisonParameters(shorter, longer, targetChannel, shorterFrames, longerFrames, shorterSpan, longerFullSpan);
        var (correlation, offset) = CalculateMaxCorrelation(parameters);

        // 8. 非重複領域のエネルギー検証
        // 短いクリップが長いクリップの一部にのみ一致し、長いクリップの他の部分に無視できない音量が存在する場合は不一致とする
        if (shorterFrames < longerFrames)
        {
            if (HasSignificantNonOverlapEnergy(longerFullSpan, shorterFrames, longerFrames, offset))
            {
                correlation = MismatchScore;
            }
        }

        // 結果のキャッシュと最終判定
        if (canCache) AudioRegistry.Instance.CorrelationCache[key] = correlation;
        return correlation >= threshold;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasCompatibleFormat(ICachedSoundData data1, ICachedSoundData data2)
    {
        return data1.SampleRate == data2.SampleRate &&
               data1.Channels == data2.Channels &&
               data1.BitsPerSample == data2.BitsPerSample;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryCheckSilenceMatch(
        ICachedSoundData data1,
        ICachedSoundData data2,
        IReadOnlyList<ActiveRegion>[] activeRegions1,
        IReadOnlyList<ActiveRegion>[] activeRegions2,
        out bool isMatch)
    {
        bool isData1Silent = true;
        bool isData2Silent = true;

        for (int ch = 0; ch < activeRegions1.Length && ch < data1.Channels; ch++)
        {
            if (activeRegions1[ch]?.Count > 0)
            {
                isData1Silent = false;
                break;
            }
        }

        for (int ch = 0; ch < activeRegions2.Length && ch < data2.Channels; ch++)
        {
            if (activeRegions2[ch]?.Count > 0)
            {
                isData2Silent = false;
                break;
            }
        }

        if (isData1Silent || isData2Silent)
        {
            isMatch = isData1Silent && isData2Silent;
            return true;
        }

        isMatch = false;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExceedsLengthDifference(ICachedSoundData data1, ICachedSoundData data2)
    {
        int frames1 = data1.TotalSamples / data1.Channels;
        int frames2 = data2.TotalSamples / data2.Channels;
        int lengthDiffThreshold = (int)(data1.SampleRate * AppConstants.AudioComparison.LengthSimilarityTolerance);

        return Math.Abs(frames1 - frames2) > lengthDiffThreshold;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMismatchedBySimHash(ICachedSoundData data1, ICachedSoundData data2)
    {
        if (data1 is IAudioStatisticalData stat1 && data2 is IAudioStatisticalData stat2 &&
            stat1.SimHash256 != null && stat2.SimHash256 != null)
        {
            var s1 = stat1.SimHash256;
            var s2 = stat2.SimHash256;

            // 手動ループ展開により分岐命令を排除
            int hammingDistance =
                BitOperations.PopCount(s1[0] ^ s2[0]) +
                BitOperations.PopCount(s1[1] ^ s2[1]) +
                BitOperations.PopCount(s1[2] ^ s2[2]) +
                BitOperations.PopCount(s1[3] ^ s2[3]);

            if (hammingDistance > SimHashHammingThreshold)
            {
                return true;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMismatchedBySpectralFeatures(ICachedSoundData data1, ICachedSoundData data2)
    {
        if (data1 is IAudioStatisticalData sStat1 && data2 is IAudioStatisticalData sStat2 &&
            sStat1.SpectralFeatures != null && sStat2.SpectralFeatures != null)
        {
            float distSq = 0;
            var v1 = sStat1.SpectralFeatures;
            var v2 = sStat2.SpectralFeatures;
            for (int i = 0; i < 16; i++)
            {
                float diff = v1[i] - v2[i];
                distSq += diff * diff;
            }

            if (distSq > SpectralDistanceSquaredThreshold)
            {
                return true;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasSignificantNonOverlapEnergy(
        ReadOnlySpan<float> longerFullSpan,
        int shorterFrames,
        int longerFrames,
        int offset)
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
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 最適なアライメント（位相ズレ補正）を加味した上での最大ピアソン相関係数を計算します。
    /// </summary>
    public static (float Correlation, int Offset) CalculateMaxCorrelation(in WaveComparisonParameters parameters)
    {
        int offset = GetAlignmentOffset(parameters.Shorter, parameters.Longer, parameters.TargetChannel, parameters.ShorterSpan, parameters.LongerFullSpan);

        if (parameters.ShorterFrames == parameters.LongerFrames && offset == 0)
        {
            float corr = WaveValidation.CalculatePearsonCorrelationSIMD(parameters.ShorterSpan, parameters.LongerFullSpan);
            return (corr, offset);
        }

        float[] paddedShorter = ArrayPool<float>.Shared.Rent(parameters.LongerFrames);
        try
        {
            Array.Clear(paddedShorter, 0, parameters.LongerFrames);
            PopulatePaddedShorter(parameters.ShorterSpan, paddedShorter, parameters.ShorterFrames, parameters.LongerFrames, ref offset);
            float corr = WaveValidation.CalculatePearsonCorrelationSIMD(paddedShorter.AsSpan(0, parameters.LongerFrames), parameters.LongerFullSpan);
            return (corr, offset);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(paddedShorter);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetAlignmentOffset(
        ICachedSoundData shorter, ICachedSoundData longer,
        int targetChannel, ReadOnlySpan<float> shorterSpan, ReadOnlySpan<float> longerFullSpan)
    {
        if (shorter is IAudioStatisticalData sFft && longer is IAudioStatisticalData lFft &&
            sFft.FftSpectrum?[targetChannel] != null && lFft.FftSpectrum?[targetChannel] != null)
        {
            return WaveValidation.CalculateAlignmentOffset(sFft.FftSpectrum[targetChannel], lFft.FftSpectrum[targetChannel]);
        }
        return FftAlignmentEngine.CalculateAlignmentOffset(shorterSpan, longerFullSpan);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PopulatePaddedShorter(
        ReadOnlySpan<float> shorterSpan,
        float[] paddedShorter,
        int shorterFrames,
        int longerFrames,
        ref int offset)
    {
        if (offset >= 0)
        {
            if (offset + shorterFrames > longerFrames)
            {
                offset = 0;
            }
            shorterSpan.CopyTo(paddedShorter.AsSpan(offset, shorterFrames));
        }
        else
        {
            int absOffset = -offset;
            if (absOffset >= shorterFrames)
            {
                absOffset = 0;
            }
            int compareLen = shorterFrames - absOffset;
            shorterSpan.Slice(absOffset, compareLen).CopyTo(paddedShorter.AsSpan(0, compareLen));
        }
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
            return ZeroCorrelation;
        }

        if (data1.TotalSamples != data2.TotalSamples) return ZeroCorrelation;

        var activeRegions1 = data1.GetActiveRegions();
        var activeRegions2 = data2.GetActiveRegions();

        if (activeRegions1 != null && activeRegions2 != null && activeRegions1.Length > 0 && activeRegions2.Length > 0)
        {
            var regions1 = activeRegions1[LChannel];
            var regions2 = activeRegions2[LChannel];

            // If both are entirely silent
            if ((regions1 == null || regions1.Count == 0) && (regions2 == null || regions2.Count == 0))
            {
                return PerfectCorrelation;
            }
            // If only one is entirely silent
            if (regions1 == null || regions1.Count == 0 || regions2 == null || regions2.Count == 0)
            {
                return ZeroCorrelation;
            }

            return WaveValidation.CalculatePearsonForCachedDataSIMD(data1, data2, LChannel);
        }

        return ZeroCorrelation;
    }
}
