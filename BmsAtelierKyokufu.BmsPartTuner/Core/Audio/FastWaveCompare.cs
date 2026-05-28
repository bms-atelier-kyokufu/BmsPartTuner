namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// オンメモリキャッシュされた音声データの高速比較クラス。
/// ピアソン相関係数による波形の形状比較を採用し、音量差やDCオフセットに影響されず波形の相似性のみを評価します。
/// ロード時に正規化された波形（平均0、ノルム1）を事前計算することで、比較時はドット積のみで相関係数を算出し高速に処理します。
/// </summary>
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
            // PerformanceDebugLogger.WriteDebug(nameof(FastWaveCompare), $"[DEBUG-FormatMismatch] {System.IO.Path.GetFileName(data1.FilePath)} vs {System.IO.Path.GetFileName(data2.FilePath)}");
            return false;
        }

        // Length difference check has been removed to allow merging of files with different tail lengths.
        // The Pearson correlation over the zero-padded shorter file is robust enough to reject false positives.
        _ = Math.Abs(data1.TotalSamples - data2.TotalSamples);

        var activeRegions1 = data1.GetActiveRegions();
        var activeRegions2 = data2.GetActiveRegions();

        if (activeRegions1 == null || activeRegions2 == null || activeRegions1.Length == 0 || activeRegions2.Length == 0)
        {
            // PerformanceDebugLogger.WriteDebug(nameof(FastWaveCompare), $"[DEBUG-NoActiveRegions] {System.IO.Path.GetFileName(data1.FilePath)} vs {System.IO.Path.GetFileName(data2.FilePath)}");
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
            // PerformanceDebugLogger.WriteDebug(nameof(FastWaveCompare), $"[DEBUG-SilenceReject] {System.IO.Path.GetFileName(data1.FilePath)}({isData1Silent}) vs {System.IO.Path.GetFileName(data2.FilePath)}({isData2Silent})");
            return false;
        }

        // Find first active channel to compute Pearson on (usually 0, but could be 1 if left is silent)
        int targetChannel = 0;
        if (activeRegions1[0] == null || activeRegions1[0].Count == 0)
        {
            targetChannel = 1;
        }

        var shorter = data1.TotalSamples < data2.TotalSamples ? data1 : data2;
        var longer = data1.TotalSamples < data2.TotalSamples ? data2 : data1;

        int shorterFrames = shorter.TotalSamples / shorter.Channels;
        int longerFrames = longer.TotalSamples / longer.Channels;

        var shorterSpan = shorter.GetRawSpan(targetChannel, 0, shorterFrames);
        var longerFullSpan = longer.GetRawSpan(targetChannel, 0, longerFrames);

        int offset = 0;
        float correlation = CalculateMaxCorrelation(shorter, longer, targetChannel, shorterFrames, longerFrames, shorterSpan, longerFullSpan, out offset);

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
                    // PerformanceDebugLogger.WriteDebug(nameof(FastWaveCompare), $"[DEBUG-IsMatch] Rejected due to non-silent tail (RMS={nonOverlapRms:F6})");
                    return false;
                }
            }
        }

        if (correlation < threshold)
        {
            string n1 = System.IO.Path.GetFileName(data1.FilePath);
            string n2 = System.IO.Path.GetFileName(data2.FilePath);
            // PerformanceDebugLogger.WriteDebug(nameof(FastWaveCompare), $"[DEBUG-IsMatch] {n1} vs {n2} corr={correlation:F4}, offset={offset}, thr={threshold}");
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
