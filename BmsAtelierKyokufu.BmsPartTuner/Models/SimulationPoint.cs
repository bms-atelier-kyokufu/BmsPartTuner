namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// 相関しきい値のシミュレーションにおける1データ点を提供します。
/// イミュータブルなデータ構造として設計されており、生成後の変更が不可能なためスレッドセーフです。
/// </summary>
public class SimulationPoint(float threshold, int fileCount)
{
    /// <summary>
    /// 相関係数しきい値。
    /// この値以上の類似度を持つファイルを統合します。
    /// </summary>
    public float Threshold { get; } = threshold;

    /// <summary>
    /// 当該しきい値で最適化した場合の予測ファイル数。
    /// </summary>
    public int FileCount { get; } = fileCount;
}
