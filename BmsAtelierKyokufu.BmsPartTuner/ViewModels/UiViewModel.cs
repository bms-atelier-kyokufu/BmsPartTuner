namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// トースト通知用のデータモデルです。
/// </summary>
/// <remarks>
/// <para>【用途】</para>
/// 処理完了や警告を一時的に表示する通知UIのデータを保持します。
/// Material Design風のトースト通知を実現します。
/// 
/// <para>【表示例】</para>
/// <list type="bullet">
/// <item>成功: "✓ 最適化が完了しました"（Icon="✓", IsError=false）</item>
/// <item>エラー: "✗ ファイルが見つかりません"（Icon="✗", IsError=true）</item>
/// </list>
/// </remarks>
public class ToastViewModel
{
    /// <summary>通知メッセージ本文。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 表示アイコン（絵文字または記号、デフォルト: "✓"）。
    /// </summary>
    public string Icon { get; set; } = "✓";

    /// <summary>
    /// エラー表示かどうか（true: 赤背景、false: 緑背景）。
    /// </summary>
    public bool IsError { get; set; }
}

/// <summary>
/// 結果カード用のデータモデルです。
/// </summary>
/// <remarks>
/// <para>【用途】</para>
/// 最適化完了後に表示される詳細結果カードのデータを保持します。
/// 推奨しきい値、削減率、処理時間などの統計情報を提示します。
/// 
/// <para>【表示優先度】</para>
/// <list type="number">
/// <item>Threshold（大見出し）: 推奨しきい値 - ユーザーが最も知りたい情報</item>
/// <item>Summary（サマリー）: 削減後ファイル数 - Base36/Base62の具体的な結果</item>
/// <item>Reduction（追加情報）: シミュレーション情報や削減率</item>
/// <item>Time（処理時間）: パフォーマンス指標</item>
/// <item>Margin（メモリ情報）: メモリ使用量</item>
/// </list>
/// 
/// <para>【表示例（最適化結果）】</para>
/// <code>
/// ┌─────────────────────────────────────┐
/// │ ✨ 最適化サマリー                   │
/// │                                     │
/// │ 36進数: 11%                         │
/// │ 62進数: 100%                        │  ← Threshold（大見出し・推奨しきい値）
/// │                                     │
/// │ 推奨しきい値                        │  ← ラベル（XAMLで固定表示）
/// │ ───────────────────────────────     │
/// │ 36進数: 1250件                      │
/// │ 62進数: 2196件                      │  ← Summary（削減後ファイル数）
/// │                                     │
/// │ 削減後ファイル数                    │  ← ラベル（XAMLで固定表示）
/// │ ───────────────────────────────     │
/// │ 計測点: 90回                        │  ← Reduction
/// │                                     │
/// │ 使用メモリ                          │
/// │ 571.7MB                             │  ← Margin
/// │ 11.5秒                              │  ← Time
/// └─────────────────────────────────────┘
/// </code>
/// </remarks>
public class ResultCardData
{
    /// <summary>
    /// 推奨しきい値（大見出し）。ユーザーが最も知りたい情報。
    /// ラベル（「推奨しきい値」など）はXAML側で固定表示されるため、ここには数値のみを格納します。
    /// 改行(\n)で36進数と62進数を分けて表示します。
    /// 例: "36進数: 11%\n62進数: 100%"
    /// </summary>
    public string Threshold { get; set; } = string.Empty;

    /// <summary>
    /// 削減後ファイル数（サマリー）。Base36/Base62の具体的な結果。
    /// 改行(\n)で36進数と62進数を分けて表示します。
    /// 例: "36進数: 1250件\n62進数: 2196件"
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 追加情報（削減率やシミュレーション情報）。
    /// 最適化時: "計測点: 90回"
    /// 削減時: "削減率: 46.7%"
    /// </summary>
    public string Reduction { get; set; } = string.Empty;

    /// <summary>処理時間。例: "12.3秒"</summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>
    /// メモリ情報。例: "543.5MB"
    /// </summary>
    public string Margin { get; set; } = string.Empty;

    /// <summary>
    /// 表示アイコン（絵文字、デフォルト: "✨"）。
    /// </summary>
    public string Icon { get; set; } = "✨";

    /// <summary>
    /// 最適化結果かどうか（true: AutoOptimize実行結果、false: 通常のReduction実行結果）。
    /// </summary>
    public bool IsOptimization { get; set; }
}
