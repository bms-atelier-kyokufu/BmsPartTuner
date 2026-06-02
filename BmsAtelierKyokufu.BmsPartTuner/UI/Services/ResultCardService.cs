using System.Windows.Media.Animation;
using BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.Services;

/// <summary>
/// 最適化結果やパフォーマンス統計（Tech Stats）を視覚的に表示するカードUIを制御するサービス。
/// プレースホルダーと結果カードの切り替えや、フェードインアニメーションを管理します。
/// </summary>
[ExcludeFromCodeCoverage]
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
    /// 結果カードを非表示にし、プレースホルダーを表示状態に戻します。
    /// </summary>
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
    /// 指定された結果データを用いて各項目を設定し、プレースホルダーを非表示にして結果カードを表示します。
    /// 表示時には0.3秒のフェードインアニメーションが適用されます。
    /// </summary>
    /// <param name="data">表示するデータ。</param>
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
    /// 最適化シミュレーションの結果からResultCardDataを生成し、推奨しきい値やパフォーマンス統計を視覚的に表示します。
    /// 最も重要な情報である「推奨しきい値」が強調して表示されます。
    /// </summary>
    /// <param name="result">最適化結果。</param>
    public void ShowOptimizationResult(OptimizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var execTime = result.ExecutionTime.TotalSeconds;
        var memoryMb = result.MemoryUsedBytes / 1024.0 / 1024.0;

        var data = new ResultCardData
        {
            Icon = "✨",
            // 大見出し: 推奨しきい値（36進数と62進数を改行で分けて表示）
            Threshold = $"36進数: {result.Base36Result.Threshold * 100:F0}%\n62進数: {result.Base62Result.Threshold * 100:F0}%",

            // サマリー: 削減後ファイル数（改行で分けて表示）
            Summary = $"36進数: {result.Base36Result.Count}/{Core.AppConstants.Definition.MaxNumberBase36}件\n62進数: {result.Base62Result.Count}/{Core.AppConstants.Definition.MaxNumberBase62}件",
            // シミュレーション情報
            Reduction = string.Empty,
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
        _techStats?.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// パフォーマンス統計（Tech Stats）と結果カードを非表示にし、プレースホルダー状態に戻します。
    /// </summary>
    public void Hide()
    {
        HideTechStats();
        Clear();
    }
}
