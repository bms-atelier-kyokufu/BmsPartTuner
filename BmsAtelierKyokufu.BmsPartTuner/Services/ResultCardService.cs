using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.ViewModels;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// 結果カード表示サービス。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>最適化結果を視覚的に表示</item>
/// <item>プレースホルダーと結果カードの切り替え</item>
/// <item>フェードインアニメーション</item>
/// <item>パフォーマンス統計（Tech Stats）の表示</item>
/// </list>
/// 
/// <para>【表示項目】</para>
/// <list type="bullet">
/// <item>閾値（Threshold）</item>
/// <item>サマリー（Summary）</item>
/// <item>削減率（Reduction）</item>
/// <item>処理時間（Time）</item>
/// <item>エルボーポイント（Elbow）</item>
/// <item>安全マージン（Margin）</item>
/// <item>Tech Stats（処理時間・メモリ使用量）</item>
/// </list>
/// 
/// <para>【アニメーション】</para>
/// 結果表示時にフェードイン（0.3秒）を適用し、
/// 視覚的なフィードバックを提供します。
/// </remarks>
public class ResultCardService : IUiElementService<ResultCardData>
{
    private FrameworkElement? _card;
    private FrameworkElement? _placeholder;
    private TextBlock? _icon;
    private TextBlock? _threshold;
    private TextBlock? _summary;
    private TextBlock? _reduction;
    private TextBlock? _time;
    private TextBlock? _elbow;
    private TextBlock? _margin;
    private TextBlock? _techStats;

    /// <summary>結果カードが表示されているかどうか。</summary>
    public bool IsVisible => _card != null && _card.Visibility == Visibility.Visible;

    /// <summary>
    /// 結果データ。
    /// </summary>
    public class ResultData
    {
        /// <summary>閾値。</summary>
        public string Threshold { get; set; } = string.Empty;

        /// <summary>サマリー。</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>削減率。</summary>
        public string Reduction { get; set; } = string.Empty;

        /// <summary>処理時間。</summary>
        public string Time { get; set; } = string.Empty;

        /// <summary>エルボーポイント。</summary>
        public string Elbow { get; set; } = string.Empty;

        /// <summary>安全マージン。</summary>
        public string Margin { get; set; } = string.Empty;

        /// <summary>アイコン。</summary>
        public string Icon { get; set; } = "?";

        /// <summary>最適化かどうか。</summary>
        public bool IsOptimization { get; set; }
    }

    /// <summary>
    /// デフォルトコンストラクタ（DIコンテナ用）。
    /// </summary>
    public ResultCardService()
    {
    }

    /// <summary>
    /// UIコントロールを初期化。
    /// </summary>
    /// <param name="card">結果カード。</param>
    /// <param name="placeholder">プレースホルダー。</param>
    /// <param name="icon">アイコンTextBlock。</param>
    /// <param name="threshold">閾値TextBlock。</param>
    /// <param name="summary">サマリーTextBlock。</param>
    /// <param name="reduction">削減率TextBlock。</param>
    /// <param name="time">処理時間TextBlock。</param>
    /// <param name="elbow">エルボーポイントTextBlock。</param>
    /// <param name="margin">安全マージンTextBlock。</param>
    /// <param name="techStats">Tech Stats TextBlock（オプション）。</param>
    public void Initialize(
        FrameworkElement card,
        FrameworkElement placeholder,
        TextBlock icon,
        TextBlock threshold,
        TextBlock summary,
        TextBlock reduction,
        TextBlock time,
        TextBlock elbow,
        TextBlock margin,
        TextBlock? techStats = null)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
        _placeholder = placeholder ?? throw new ArgumentNullException(nameof(placeholder));
        _icon = icon ?? throw new ArgumentNullException(nameof(icon));
        _threshold = threshold ?? throw new ArgumentNullException(nameof(threshold));
        _summary = summary ?? throw new ArgumentNullException(nameof(summary));
        _reduction = reduction ?? throw new ArgumentNullException(nameof(reduction));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _elbow = elbow ?? throw new ArgumentNullException(nameof(elbow));
        _margin = margin ?? throw new ArgumentNullException(nameof(margin));
        _techStats = techStats;
    }

    /// <summary>
    /// 結果をクリア。
    /// </summary>
    /// <remarks>
    /// 結果カードを非表示にし、プレースホルダーを表示します。
    /// </remarks>
    public void Clear()
    {
        if (_card == null)
            throw new InvalidOperationException("Initialize()を先に呼び出してください");

        if (_card != null && _placeholder != null)
        {
            _card.Visibility = Visibility.Collapsed;
            _placeholder.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// 結果カードを表示。
    /// </summary>
    /// <param name="data">表示するデータ。</param>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>各TextBlockにデータを設定</item>
    /// <item>プレースホルダーを非表示</item>
    /// <item>結果カードを表示</item>
    /// <item>フェードインアニメーション（0.3秒）</item>
    /// </list>
    /// </remarks>
    void IUiElementService<ResultCardData>.Show(ResultCardData data)
    {
        if (_card == null)
            throw new InvalidOperationException("Initialize()を先に呼び出してください");

        _icon!.Text = data.Icon;
        _threshold!.Text = data.Threshold;
        _summary!.Text = data.Summary;
        _reduction!.Text = data.Reduction;
        _time!.Text = data.Time;
        _margin!.Text = data.Margin;

        _placeholder!.Visibility = Visibility.Collapsed;
        _card.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(0.3)
        };
        _card.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    /// <summary>
    /// 最適化結果からResultCardDataを生成して表示します。
    /// </summary>
    /// <param name="result">最適化結果。</param>
    /// <remarks>
    /// <para>【用途】</para>
    /// FindOptimalThresholdsAsyncの結果を視覚的に表示する際に使用します。
    /// Base36/Base62の推奨値とパフォーマンス統計を表示します。
    /// 
    /// <para>【表示優先度】</para>
    /// ユーザーが最も知りたいのは「推奨しきい値」なので、これを最も大きく表示します。
    /// しきい値は36進数と62進数の両方を満たす最高品質（しきい値が最大）の値です。
    /// </remarks>
    public void ShowOptimizationResult(OptimizationResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        var execTime = result.ExecutionTime.TotalSeconds;
        var memoryMb = result.MemoryUsedBytes / 1024.0 / 1024.0;

        var data = new ResultCardData
        {
            Icon = "✨",
            // 大見出し: 推奨しきい値（36進数と62進数を改行で分けて表示）
            Threshold = $"36進数: {result.Base36Result.Threshold * 100:F0}%\n62進数: {result.Base62Result.Threshold * 100:F0}%",

            // サマリー: 削減後ファイル数（改行で分けて表示）
            Summary = $"36進数: {result.Base36Result.Count}件\n62進数: {result.Base62Result.Count}件",
            // シミュレーション情報
            Reduction = $"計測点: {result.SimulationData.Count}回",
            Time = $"{execTime:F1}秒",
            Margin = $"{memoryMb:F1}MB",
            IsOptimization = true
        };

        // Tech Stats表示（オプション）
        if (_techStats != null)
        {
            _techStats.Text = $"Processed in {execTime:F1}s, RAM: {memoryMb:F1}MB";
            _techStats.Visibility = Visibility.Visible;
        }

        ((IUiElementService<ResultCardData>)this).Show(data);
    }

    /// <summary>
    /// パフォーマンス統計（Tech Stats）を更新します。
    /// </summary>
    /// <param name="executionTime">実行時間。</param>
    /// <param name="memoryUsedBytes">メモリ使用量（バイト）。</param>
    public void UpdateTechStats(TimeSpan executionTime, long memoryUsedBytes)
    {
        if (_techStats != null)
        {
            var memoryMb = memoryUsedBytes / 1024.0 / 1024.0;
            _techStats.Text = $"Processed in {executionTime.TotalSeconds:F1}s, RAM: {memoryMb:F1}MB";
            _techStats.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Tech Statsを非表示にします。
    /// </summary>
    public void HideTechStats()
    {
        if (_techStats != null)
        {
            _techStats.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 結果カードを非表示。
    /// </summary>
    /// <remarks>
    /// <see cref="Clear"/>を呼び出してプレースホルダーへ切り替えます。
    /// </remarks>
    public void Hide()
    {
        HideTechStats();
        Clear();
    }
}
