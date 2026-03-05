namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// シミュレーション結果の1データ点を表します。
/// </summary>
/// <remarks>
/// <para>【用途】</para>
/// <see cref="Core.CorrelationThresholdOptimizer"/>が相関係数のしきい値を変化させながら
/// 最適化後のファイル数を予測する際の、1つの測定点を表します。
/// 
/// <para>【例】</para>
/// <code>
/// Threshold=0.90, FileCount=150 → "しきい値0.90で最適化すると150ファイルになる"
/// Threshold=0.95, FileCount=180 → "しきい値0.95で最適化すると180ファイルになる"
/// </code>
/// これらの点をプロットすることで、エルボーポイント（最適値）を見つけます。
/// 
/// <para>【Why Primary Constructor】</para>
/// イミュータブルなデータ点なので、コンストラクタで初期化し、
/// 後から変更不可能にすることでスレッドセーフを保証します。
/// </remarks>
/// <param name="threshold">相関係数しきい値（0.0～1.0）。</param>
/// <param name="fileCount">予測される最適化後のファイル数。</param>
public class SimulationPoint(float threshold, int fileCount)
{
    /// <summary>相関係数しきい値（この値以上の類似度を持つファイルを統合）。</summary>
    public float Threshold { get; } = threshold;

    /// <summary>このしきい値で最適化した場合の予測ファイル数。</summary>
    public int FileCount { get; } = fileCount;
}
