namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// しきい値最適化のシミュレーション結果を提供します。
/// </summary>
public class OptimizationResult
{
    /// <summary>
    /// Base36 (1295ファイル制限) の最適しきい値とそれに対応するファイル数。
    /// </summary>
    public (float Threshold, int Count) Base36Result { get; set; }

    /// <summary>
    /// Base62 (3843ファイル制限) の最適しきい値とそれに対応するファイル数。
    /// </summary>
    public (float Threshold, int Count) Base62Result { get; set; }

    /// <summary>
    /// シミュレーションで取得した全ての測定点 (グラフ描画用)。
    /// 各タプルは (Threshold, FileCount) を表します。
    /// </summary>
    public List<(double Threshold, int Count)> SimulationData { get; set; } = [];

    /// <summary>
    /// シミュレーション実行時間。
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// シミュレーション実行中の推定メモリ使用量 (バイト)。
    /// </summary>
    public long MemoryUsedBytes { get; set; }

    /// <summary>
    /// 処理中に発生した警告メッセージのリスト。
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// 警告が存在するかどうかを示します。
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;
}
