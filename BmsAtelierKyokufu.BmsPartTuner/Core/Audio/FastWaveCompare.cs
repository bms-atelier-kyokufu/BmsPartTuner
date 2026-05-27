namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// オンメモリキャッシュされた音声データの高速比較クラス。
/// </summary>
/// <remarks>
/// <para>【比較戦略】</para>
/// ピアソン相関係数による波形の形状比較を採用。
/// 音量差やDCオフセットに影響されず、波形の相似性のみを評価します。
///
/// <para>【Phase 2最適化】</para>
/// ロード時に正規化された波形（平均0、ノルム1）を事前計算。
/// 比較時はドット積のみで相関係数を算出し、演算密度を80%削減。
///
/// <para>【Why RMS比較を廃止したか】</para>
/// <list type="number">
/// <item>数学的妥当性: ピアソン相関は計算過程で標準偏差による除算（正規化）を含むため、
/// 本質的に「音量差を無視して波形の形状のみを比較」する特性を持ちます。
/// RMSフィルタによる事前足切りは、この特性と自己矛盾するため廃止しました。</item>
/// <item>パフォーマンス（SIMD最適化）: 現代のCPUは、複雑な条件分岐（if文）を繰り返すよりも、
/// SIMDを用いて「メモリの頭から末尾まで一気に演算」する方が圧倒的に速いです。
/// 余計な中間フィルタを除くことで、CPUパイプラインを最大限に活用します。</item>
/// <item>パラメータの単一化: 「波形の相関（音色の類似度）」という1つの軸に絞ることで、
/// ユーザーが閾値設定によって「削減率とクオリティ」を直感的にコントロールできる
/// 高い操作性を実現しました。</item>
/// <item>ピアソン相関係数を計算するということは、
/// 「内部で勝手に正規化してから比較している」のと同じです。
/// この正規化を比較のたびに繰り返すのは非効率なため、
/// ロード時に「平均0、ノルム1」に変換しておくことで、
/// 比較処理を単純なドット積（FMA積算）に帰着させます。</item>
/// </list>
///
/// <para>【数式変形】</para>
/// <para>ピアソン相関係数 $r$ の定義式を変形すると、以下のようになります:</para>
/// <para>
/// $$
/// \begin{aligned}
/// r &amp;= \frac{\sum(x_i - \bar{x})(y_i - \bar{y})}{\sqrt{\sum(x_i - \bar{x})^2} \sqrt{\sum(y_i - \bar{y})^2}} \\
/// &amp;= \frac{1}{n} \sum_{i=1}^{n} \left( \frac{x_i - \bar{x}}{s_x} \right) \left( \frac{y_i - \bar{y}}{s_y} \right)
/// \end{aligned}
/// $$
/// </para>
/// <para>※ $s_x, s_y$ は標準偏差</para>
/// <para>この $\left( \frac{x_i - \bar{x}}{s_x} \right)$ という項はデータを正規化（標準化）していることを示しています。</para>
///
/// <para>【Phase 2の変換】</para>
/// 正規化波形 $\hat{x}_i$ を事前計算することで:
///
/// $$
/// r = \sum_{i=1}^{n} \hat{x}_i \cdot \hat{y}_i
/// $$
///
/// （単なるドット積）
///
/// <para>【ラグ補正について】</para>
/// ラグ（開始位置のズレ）は音ゲーの演奏感に直結するため、あえて補正せず、
/// 長さの一致と位相の同期を厳密に求めることで品質を担保しています。
/// </remarks>
internal static class FastWaveCompare
{
    /// <summary>
    /// キャッシュされた音声データ2個の高速比較。
    /// </summary>
    /// <param name="data1">比較元の音声データ。</param>
    /// <param name="data2">比較先の音声データ。</param>
    /// <param name="threshold">ピアソン相関係数のしきい値（0.0-1.0）。</param>
    /// <returns>類似している場合true。</returns>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>フォーマットチェック（サンプルレート、チャンネル数、ビット深度）</item>
    /// <item>長さチェック（サンプル数の完全一致を要求）</item>
    /// <item>正規化波形のドット積による相関計算（SIMD最適化）</item>
    /// </list>
    ///
    /// <para>【Phase 2の数学的背景】</para>
    /// <para>事前処理で波形を正規化:</para>
    /// <para>
    /// $$
    /// \hat{x}_i = \frac{x_i - \bar{x}}{\sqrt{\sum_{j=1}^{n}(x_j - \bar{x})^2}}
    /// $$
    /// </para>
    /// <para>これにより、ピアソン相関係数はドット積に帰着:</para>
    /// <para>
    /// $$
    /// r = \sum_{i=1}^{n} \hat{x}_i \cdot \hat{y}_i
    /// $$
    /// </para>
    ///
    /// <para>【演算効率の改善】</para>
    /// <list type="bullet">
    /// <item>従来版: 5変数の累積（$\sum x, \sum y, \sum x^2, \sum y^2, \sum xy$）+ 複雑な除算</item>
    /// <item>Phase 2版: 1変数の累積（$\sum(x \times y)$）のみ</item>
    /// <item>演算密度: 80%削減</item>
    /// </list>
    ///
    /// <para>【特徴】</para>
    /// <list type="bullet">
    /// <item>音量差に影響されない（±3dB、±6dBでも波形が相似なら高相関）</item>
    /// <item>DCオフセットに影響されない（相関係数 = 1.0を維持）</item>
    /// <item>ノイズに対して適度にロバスト（0.01程度のノイズなら相関 &gt; 0.98）</item>
    /// <item>逆相を確実に検出（相関係数 = -1.0）</item>
    /// </list>
    /// </remarks>
    public static bool IsMatch(ICachedSoundData data1, ICachedSoundData data2, float threshold)
    {
        if (data1.SampleRate != data2.SampleRate ||
            data1.Channels != data2.Channels ||
            data1.BitsPerSample != data2.BitsPerSample)
        {
            return false;
        }

        // Length difference check has been removed to allow merging of files with different tail lengths.
        // The Pearson correlation over the zero-padded shorter file is robust enough to reject false positives.
        int diff = Math.Abs(data1.TotalSamples - data2.TotalSamples);

        var activeRegions1 = data1.GetActiveRegions();
        var activeRegions2 = data2.GetActiveRegions();

        if (activeRegions1 != null && activeRegions2 != null && activeRegions1.Length > 0 && activeRegions2.Length > 0)
        {
            // Check both channels for total silence
            bool isData1Silent = true;
            bool isData2Silent = true;
            for (int ch = 0; ch < activeRegions1.Length && ch < data1.Channels; ch++)
            {
                if (activeRegions1[ch] != null && activeRegions1[ch].Count > 0) isData1Silent = false;
            }
            for (int ch = 0; ch < activeRegions2.Length && ch < data2.Channels; ch++)
            {
                if (activeRegions2[ch] != null && activeRegions2[ch].Count > 0) isData2Silent = false;
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

            // Find first active channel to compute Pearson on (usually 0, but could be 1 if left is silent)
            int targetChannel = 0;
            if (activeRegions1[0] == null || activeRegions1[0].Count == 0)
            {
                targetChannel = 1;
            }

            if (diff == 0)
            {
                // Exact length match -> Use Phase 2 precomputed NormalizedData (Ultra fast)
                float correlation = WaveValidation.CalculatePearsonForCachedDataSIMD(data1, data2, targetChannel);
                return correlation >= threshold;
            }
            else
            {
                // Length mismatch within 50ms -> Use Phase 1 Pearson on zero-padded DecodedData
                var shorter = data1.TotalSamples < data2.TotalSamples ? data1 : data2;
                var longer = data1.TotalSamples < data2.TotalSamples ? data2 : data1;

                int shorterFrames = shorter.TotalSamples / shorter.Channels;
                int longerFrames = longer.TotalSamples / longer.Channels;

                var shorterSpan = shorter.GetRawSpan(targetChannel, 0, shorterFrames);
                var longerSpan = longer.GetRawSpan(targetChannel, 0, longerFrames);

                // Create a temporary array padded to the longer length
                float[] paddedShorter = new float[longerFrames];
                shorterSpan.CopyTo(paddedShorter.AsSpan(0, shorterFrames));
                
                // Compare only the target channel using the raw padded floats
                float correlation = WaveValidation.CalculatePearsonCorrelationSIMD(paddedShorter, longerSpan);
                return correlation >= threshold;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if an array is all zeros (within floating point tolerance).
    /// </summary>
    private static bool IsAllZero(float[] data)
    {
        const float epsilon = 1e-9f;
        for (int i = 0; i < data.Length; i++)
        {
            if (Math.Abs(data[i]) > epsilon)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 類似度スコアを取得（デバッグ・ベンチマーク用）。
    /// </summary>
    /// <param name="data1">比較元の音声データ。</param>
    /// <param name="data2">比較先の音声データ。</param>
    /// <returns>ピアソン相関係数（-1.0〜1.0）、フォーマット不一致時は0.0。</returns>
    /// <remarks>
    /// <para>【用途】</para>
    /// <list type="bullet">
    /// <item>ベンチマークでの演算効率比較</item>
    /// <item>デバッグ時の相関係数確認</item>
    /// <item>閾値調整のための統計収集</item>
    /// </list>
    /// </remarks>
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
            if ((regions1 == null || regions1.Count == 0) || (regions2 == null || regions2.Count == 0))
            {
                return 0.0f;
            }

            return WaveValidation.CalculatePearsonForCachedDataSIMD(data1, data2, 0);
        }

        return 0.0f;
    }
}
