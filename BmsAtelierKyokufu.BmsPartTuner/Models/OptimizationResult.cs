namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// しきい値最適化のシミュレーション結果を表します。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>計算結果（Base36/Base62のしきい値とカウント）を保持</item>
/// <item>シミュレーションデータ（グラフ描画用）を保持</item>
/// <item>パフォーマンス指標（実行時間、メモリ使用量）を記録</item>
/// </list>
/// 
/// <para>【用途】</para>
/// <see cref="Services.BmsOptimizationService.FindOptimalThresholdsAsync"/>の戻り値として使用します。
/// UIレイヤーで最適化結果を表示し、ユーザーに最適値を提案するために利用されます。
/// </remarks>
public class OptimizationResult
{
    /// <summary>
    /// Base36（1295ファイル制限）の最適しきい値とそれに対応するファイル数。
    /// </summary>
    public (float Threshold, int Count) Base36Result { get; set; }

    /// <summary>
    /// Base62（3843ファイル制限）の最適しきい値とそれに対応するファイル数。
    /// </summary>
    public (float Threshold, int Count) Base62Result { get; set; }

    /// <summary>
    /// シミュレーションで取得した全ての測定点（グラフ描画用）。
    /// </summary>
    /// <remarks>
    /// 各タプルは (Threshold, FileCount) を表します。
    /// グラフのX軸をしきい値、Y軸をファイル数とすることで、
    /// ユーザーが最適値の変化を視覚的に理解できます。
    /// </remarks>
    public List<(double Threshold, int Count)> SimulationData { get; set; } = new();

    /// <summary>
    /// シミュレーション実行時間。
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// シミュレーション実行中の推定メモリ使用量（バイト）。
    /// </summary>
    /// <remarks>
    /// <para>【計測方法】</para>
    /// 実行前後の <c>GC.GetTotalMemory(false)</c> の差分、
    /// または <c>Process.GetCurrentProcess().PeakWorkingSet64</c> から推定します。
    /// </remarks>
    public long MemoryUsedBytes { get; set; }

    /// <summary>
    /// 警告メッセージのリスト。
    /// </summary>
    /// <remarks>
    /// 処理中に発生した警告（破損ファイル、読み込み失敗など）を格納します。
    /// ユーザーに通知すべき情報ですが、処理の成功を妨げるものではありません。
    /// </remarks>
    public List<string> Warnings { get; set; } = new List<string>();

    /// <summary>
    /// 警告が存在するかどうかを示します。
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;
}

