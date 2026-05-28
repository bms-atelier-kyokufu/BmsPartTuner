namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// BMS定義削減処理のオプションパラメータを提供します。
/// </summary>
public class DefinitionReductionOptions
{
    /// <summary>
    /// 音声比較時の相関係数のしきい値。
    /// </summary>
    public float R2Threshold { get; set; } = 0.95f;

    /// <summary>
    /// 最適化対象の開始定義番号。
    /// </summary>
    public int StartDefinition { get; set; } = 1;

    /// <summary>
    /// 最適化対象の終了定義番号。
    /// </summary>
    public int EndDefinition { get; set; } = 1;

    /// <summary>
    /// 未参照ファイルの実体（物理ファイル）を削除するかどうか。
    /// </summary>
    public bool IsPhysicalDeletionEnabled { get; set; }

    /// <summary>
    /// 入力BMSファイルのテキストコンテンツ。
    /// </summary>
    public string? InputBmsContent { get; set; }

    /// <summary>
    /// 進捗通知オブジェクト。
    /// </summary>
    public IProgress<int>? Progress { get; set; }

    /// <summary>
    /// 最適化対象から除外するキーワードのリスト。
    /// </summary>
    public IEnumerable<string>? SelectedKeywords { get; set; }
}
