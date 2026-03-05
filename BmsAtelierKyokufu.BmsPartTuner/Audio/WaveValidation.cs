using System.Numerics;

namespace BmsAtelierKyokufu.BmsPartTuner.Audio;

/// <summary>
/// 波形検証クラス（SIMD最適化版）。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>決定係数（R²）の計算</item>
/// <item>ピアソンの相関係数の計算</item>
/// <item>SIMD（Vector&lt;T&gt;）による並列演算最適化</item>
/// </list>
/// 
/// <para>【数学的定義】</para>
/// 
/// 決定係数（R²）:
/// R² = 1 - RSS/DSS
/// RSS (Residual Sum of Squares) = Σ(x - y)²
/// DSS (Deviation Sum of Squares) = Σ(x - x̄)²
/// 
/// ピアソンの相関係数（ρ）:
/// ρ = Cov(X,Y) / (σX × σY)
/// Cov(X, Y) = (1/n) × Σ(x - x̄)(y - ȳ)
/// σX = √((1/n) × Σ(x - x̄)²), σY = √((1/n) × Σ(y - ȳ)²)
/// 
/// <para>【比較】</para>
/// <list type="bullet">
/// <item>R²: 説明力（回帰の当てはまりの良さ）、y = x の形の関係を想定</item>
/// <item>ρ: 相関の強さ（スケール不変）、音量差に強い</item>
/// </list>
/// 
/// <para>【解釈】</para>
/// 
/// 決定係数（R²）:
/// R² = 1.0: 完全一致
/// R² ≥ 0.98: ほぼ同一（厳密モード）
/// R² ≥ 0.95: 非常に似ている（標準）
/// R² ≥ 0.90: 似ている（緩いモード）
/// R² &lt; 0.90: 異なる
/// 
/// ピアソン相関係数（ρ）:
/// ρ = 1.0: 完全相関
/// ρ ≥ 0.98: 非常に強い相関
/// ρ ≥ 0.95: 強い相関（推奨）
/// ρ ≥ 0.90: 中程度の相関
/// ρ &lt; 0.90: 弱い相関
/// 
/// <para>【SIMD最適化】</para>
/// <list type="bullet">
/// <item>Vector&lt;T&gt;による並列演算</item>
/// <item>AVX2: 8個のfloatを同時処理</item>
/// <item>AVX-512: 16個のfloatを同時処理</item>
/// <item>従来比: 3〜8倍高速化</item>
/// </list>
/// 
/// <para>【Phase 2最適化】</para>
/// <list type="bullet">
/// <item>事前正規化波形によりドット積でピアソン相関を計算</item>
/// <item>5変数の累積計算を1変数に削減</item>
/// <item>演算密度80%削減</item>
/// </list>
/// </remarks>
static public class WaveValidation
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
    /// 決定係数（R²）を計算（Span版 - ゼロコピー対応）。
    /// </summary>
    /// <param name="wav1">音声データSpan1。</param>
    /// <param name="wav2">音声データSpan2。</param>
    /// <returns>決定係数（0.0〜1.0）。</returns>
    /// <remarks>
    /// <para>【1パスアルゴリズム】</para>
    /// 従来は2パス必要だった計算を1パスで実行:
    /// <list type="bullet">
    /// <item>パス1: 平均値計算（Σx / n）</item>
    /// <item>パス2: 分散と誤差計算</item>
    /// </list>
    /// 
    /// <para>【最適化後】</para>
    /// 単一ループで Σx, Σx², Σ(x-y)² を同時計算。
    /// メモリアクセス回数: 半減
    /// 
    /// <para>【SIMD並列化】</para>
    /// <list type="bullet">
    /// <item>Vector&lt;T&gt;で複数要素を一度に処理</item>
    /// <item>ベクトル化可能な範囲: SIMD処理</item>
    /// <item>端数: スカラー処理</item>
    /// </list>
    /// 
    /// <para>【計算式】</para>
    /// DSS = Σ(x²) - (Σx)²/n （1パス公式）
    /// R² = 1 - RSS/DSS
    /// 
    /// <para>【Why double精度】</para>
    /// 次の2つの問題に対処するためdoubleで計算してからfloatにキャスト:
    /// <list type="bullet">
    /// <item>floatの精度限界により、大きな値の計算で桁落ちが発生する可能性</item>
    /// <item>sumX * sumX がオーバーフローする可能性（長い音声の場合）</item>
    /// </list>
    /// </remarks>
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
    /// ピアソンの相関係数を計算（配列版）。
    /// </summary>
    /// <param name="wav1">音声データ配列1。</param>
    /// <param name="wav2">音声データ配列2。</param>
    /// <returns>相関係数（-1.0〜1.0、通常は0.0〜1.0）。</returns>
    /// <remarks>
    /// <para>【特徴】</para>
    /// <list type="bullet">
    /// <item>音量（スケール）の違いに強い</item>
    /// <item>波形の「形状」の相似性を評価</item>
    /// <item>圧縮音声での位相ズレに対してロバスト</item>
    /// </list>
    /// 
    /// <para>【計算式】</para>
    /// ρ = Cov(X,Y) / (σX × σY)
    /// ρ = Σ((x - x̄) × (y - ȳ)) / √(Σ(x - x̄)² × Σ(y - ȳ)²)
    /// 
    /// <para>【1パス最適化】</para>
    /// Σx, Σy, Σx², Σy², Σ(x×y) を同時計算。
    /// </remarks>
    static public float CalculatePearsonCorrelation(float[] wav1, float[] wav2)
    {
        if (wav1.Length != wav2.Length || wav1.Length == 0)
            return 0.0F;

        return CalculatePearsonCorrelationSIMD(wav1.AsSpan(), wav2.AsSpan());
    }

    /// <summary>
    /// ピアソンの相関係数を計算（Span版 - ゼロコピー対応、SIMD最適化、1パス）。
    /// </summary>
    /// <param name="wav1">音声データSpan1。</param>
    /// <param name="wav2">音声データSpan2。</param>
    /// <returns>相関係数（-1.0〜1.0、通常は0.0〜1.0）。</returns>
    /// <remarks>
    /// <para>【計算フロー】</para>
    /// <list type="number">
    /// <item>SIMD処理: ベクトル化可能な範囲</item>
    /// <item>端数処理: ベクトル化できなかった残り</item>
    /// <item>平均値計算</item>
    /// <item>分散・共分散計算（1パス公式）</item>
    /// <item>相関係数計算: ρ = Cov(X,Y) / (σX × σY)</item>
    /// </list>
    /// 
    /// <para>【1パス公式】</para>
    /// Cov(X,Y) = E[XY] - E[X]E[Y]
    /// Var(X) = E[X²] - E[X]²
    /// </remarks>
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

        (float sumX, float sumY, float sumX2, float sumY2, float sumXY) = ProcessVectorizedPearson(
            wav1, wav2, vectorizedLength, vectorSize);

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
    /// Phase 2: 正規化済み波形のドット積でピアソン相関係数を計算（配列版）。
    /// </summary>
    /// <param name="normalizedWav1">正規化済み波形1。</param>
    /// <param name="normalizedWav2">正規化済み波形2。</param>
    /// <returns>相関係数（-1.0〜1.0）。</returns>
    /// <remarks>
    /// <para>【数学的背景】</para>
    /// 正規化波形（平均0、ノルム1）の場合、ピアソン相関係数は単純なドット積に帰着:
    /// r = Σ(x̂ × ŷ)
    /// 
    /// <para>【効果】</para>
    /// <list type="bullet">
    /// <item>5変数の累積計算を1変数に削減</item>
    /// <item>演算密度80%削減</item>
    /// <item>CPUパイプライン効率向上</item>
    /// </list>
    /// </remarks>
    static public float CalculatePearsonFromNormalized(float[] normalizedWav1, float[] normalizedWav2)
    {
        if (normalizedWav1.Length != normalizedWav2.Length || normalizedWav1.Length == 0)
            return 0.0F;

        return CalculatePearsonFromNormalizedSIMD(normalizedWav1.AsSpan(), normalizedWav2.AsSpan());
    }

    /// <summary>
    /// Phase 2: 正規化済み波形のドット積でピアソン相関係数を計算（Span版・SIMD最適化）。
    /// </summary>
    /// <param name="normalizedWav1">正規化済み波形Span1。</param>
    /// <param name="normalizedWav2">正規化済み波形Span2。</param>
    /// <returns>相関係数（-1.0〜1.0）。</returns>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// 正規化済み波形（平均0、ノルム1）の場合、ドット積がピアソン相関係数に等しい:
    /// r = Σ(x̂ × ŷ)
    /// 
    /// <para>【SIMD最適化】</para>
    /// <list type="bullet">
    /// <item>単純な乗算・加算のみでベクトル化効率最大</item>
    /// <item>FMA（Fused Multiply-Add）命令との相性良好</item>
    /// <item>AVX2で8要素同時処理</item>
    /// </list>
    /// 
    /// <para>【標準版との比較】</para>
    /// <list type="bullet">
    /// <item>標準版: 5変数の累積（Σx, Σy, Σx², Σy², Σxy）+ 複雑な除算</item>

    /// </list>
    /// </remarks>
    static public float CalculatePearsonFromNormalizedSIMD(ReadOnlySpan<float> normalizedWav1, ReadOnlySpan<float> normalizedWav2)
    {
        if (normalizedWav1.Length != normalizedWav2.Length || normalizedWav1.Length == 0)
            return 0.0F;

        int length = normalizedWav1.Length;
        int vectorSize = Vector<float>.Count;
        int vectorizedLength = length - (length % vectorSize);

        Vector<float> dotProduct_vec = Vector<float>.Zero;

        for (int i = 0; i < vectorizedLength; i += vectorSize)
        {
            Vector<float> x = new Vector<float>(normalizedWav1.Slice(i, vectorSize));
            Vector<float> y = new Vector<float>(normalizedWav2.Slice(i, vectorSize));
            dotProduct_vec += x * y;
        }

        Vector<float> ones = new Vector<float>(1.0f);
        double dotProduct = Vector.Dot(dotProduct_vec, ones);

        for (int i = vectorizedLength; i < length; i++)
        {
            dotProduct += normalizedWav1[i] * normalizedWav2[i];
        }

        return (float)Math.Max(-1.0, Math.Min(1.0, dotProduct));
    }

    #endregion

    #region プライベートメソッド

    /// <summary>
    /// ベクトル化された範囲の処理（R²計算用）。
    /// </summary>
    /// <returns>(Σx, Σx², RSS)</returns>
    /// <remarks>
    /// Vector&lt;T&gt;を使用してSIMD並列演算を実行します。
    /// </remarks>
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
            Vector<float> x = new Vector<float>(wav1.Slice(i, vectorSize));
            Vector<float> y = new Vector<float>(wav2.Slice(i, vectorSize));

            Vector<float> diff = x - y;

            sumX_vec += x;
            sumX2_vec += x * x;
            sumDiff2_vec += diff * diff;
        }

        Vector<float> ones = new Vector<float>(1.0f);
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
    /// </summary>
    /// <returns>(ΣX, ΣY, ΣX², ΣY², ΣXY)</returns>
    /// <remarks>
    /// Vector&lt;T&gt;を使用してSIMD並列演算を実行します。
    /// </remarks>
    private static (float sumX, float sumY, float sumX2, float sumY2, float sumXY) ProcessVectorizedPearson(
        ReadOnlySpan<float> wav1,
        ReadOnlySpan<float> wav2,
        int vectorizedLength,
        int vectorSize)
    {
        Vector<float> sumX_vec = Vector<float>.Zero;
        Vector<float> sumY_vec = Vector<float>.Zero;
        Vector<float> sumX2_vec = Vector<float>.Zero;
        Vector<float> sumY2_vec = Vector<float>.Zero;
        Vector<float> sumXY_vec = Vector<float>.Zero;

        for (int i = 0; i < vectorizedLength; i += vectorSize)
        {
            Vector<float> x = new Vector<float>(wav1.Slice(i, vectorSize));
            Vector<float> y = new Vector<float>(wav2.Slice(i, vectorSize));

            sumX_vec += x;
            sumY_vec += y;
            sumX2_vec += x * x;
            sumY2_vec += y * y;
            sumXY_vec += x * y;
        }

        Vector<float> ones = new Vector<float>(1.0f);
        float sumX = Vector.Dot(sumX_vec, ones);
        float sumY = Vector.Dot(sumY_vec, ones);
        float sumX2 = Vector.Dot(sumX2_vec, ones);
        float sumY2 = Vector.Dot(sumY2_vec, ones);
        float sumXY = Vector.Dot(sumXY_vec, ones);

        return (sumX, sumY, sumX2, sumY2, sumXY);
    }

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
