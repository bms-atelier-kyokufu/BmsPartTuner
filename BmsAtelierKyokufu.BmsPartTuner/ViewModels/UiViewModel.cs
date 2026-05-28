namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// トースト通知の表示データを保持するモデル。
/// </summary>
public class ToastViewModel
{
    /// <summary>
    /// 通知の本文。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 通知アイコン（デフォルト: "✓"）。
    /// </summary>
    public string Icon { get; set; } = "✓";

    /// <summary>
    /// エラー通知であるかどうか。trueの場合は警告・エラー用のスタイルで表示されます。
    /// </summary>
    public bool IsError { get; set; }
}

/// <summary>
/// 最適化処理などの詳細な結果を表示するカードUIのデータモデル。
/// </summary>
public class ResultCardData
{
    /// <summary>
    /// 推奨しきい値や使用しきい値などの大見出し情報。
    /// 改行(\n)でBase36とBase62の情報を分けて格納します。
    /// </summary>
    public string Threshold { get; set; } = string.Empty;

    /// <summary>
    /// 削減後のファイル数などのサマリー情報。
    /// 改行(\n)でBase36とBase62の情報を分けて格納します。
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 削減率やシミュレーション回数などの追加情報。
    /// </summary>
    public string Reduction { get; set; } = string.Empty;

    /// <summary>
    /// 処理にかかった時間。
    /// </summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>
    /// 処理中に使用されたメモリ使用量。
    /// </summary>
    public string Margin { get; set; } = string.Empty;

    /// <summary>
    /// カードに表示するアイコン。
    /// </summary>
    public string Icon { get; set; } = "✨";

    /// <summary>
    /// 自動最適化処理（AutoOptimize）の実行結果であるかどうか。
    /// </summary>
    public bool IsOptimization { get; set; }
}
