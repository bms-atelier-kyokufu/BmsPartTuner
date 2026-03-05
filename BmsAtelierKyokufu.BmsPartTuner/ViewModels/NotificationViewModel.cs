using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// 通知表示ViewModel。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>トースト通知の表示・非表示</item>
/// <item>結果カードの表示・非表示</item>
/// <item>スライド確認ダイアログの表示・非表示</item>
/// </list>
/// 
/// <para>【UI要素】</para>
/// <list type="number">
/// <item>トースト通知: 短時間のフィードバック（成功/エラー）</item>
/// <item>結果カード: 最適化・削減の詳細結果表示</item>
/// <item>スライド確認: ファイル上書き確認</item>
/// </list>
/// 
/// <para>【設計思想】</para>
/// UI表示ロジックをViewModelに集約し、MainViewModelから分離することで、
/// 責任の明確化と再利用性を向上させています。
/// 
/// <para>【Why 分離】</para>
/// 通知機能は横断的関心事であり、複数の操作から呼ばれるため、
/// 独立したViewModelとして管理することで保守性が向上します。
/// </remarks>
public partial class NotificationViewModel : ObservableObject
{
    public NotificationViewModel()
    {
        // トースト自動非表示タイマーの初期化
        _toastHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Core.AppConstants.UI.ToastDisplayDurationMs)
        };
        _toastHideTimer.Tick += OnToastHideTimerTick;
    }

    /// <summary>
    /// トースト通知を表示。
    /// </summary>
    /// <param name="message">メッセージ。</param>
    /// <param name="icon">アイコン（デフォルト: "✓"）。</param>
    /// <param name="isError">エラー通知かどうか（デフォルト: false）。</param>
    /// <remarks>
    /// <para>【用途】</para>
    /// 処理の成功/失敗を短時間（数秒）ユーザーに通知します。
    /// 
    /// <para>【アイコン例】</para>
    /// <list type="bullet">
    /// <item>"✓": 成功</item>
    /// <item>"⚠": エラー</item>
    /// <item>"ℹ": 情報</item>
    /// </list>
    /// </remarks>
    public void ShowToast(string message, string icon = "✓", bool isError = false)
    {
        // 既存のタイマーを停止して再開始
        _toastHideTimer.Stop();

        ToastMessage = message;
        ToastIcon = icon;
        IsToastError = isError;
        IsToastVisible = true;

        // 自動非表示タイマーを開始
        _toastHideTimer.Start();
    }

    /// <summary>
    /// トースト通知を非表示。
    /// </summary>
    public void HideToast()
    {
        IsToastVisible = false;
    }

    /// <summary>
    /// 結果カードを表示。
    /// </summary>
    /// <param name="thresholdValues">しきい値（大見出し）- 36進数と62進数のしきい値。</param>
    /// <param name="resultFileCounts">削減後ファイル数（サマリー）- 36進数と62進数のファイル数。</param>
    /// <param name="additionalInfo">追加情報（削減率やシミュレーション情報）。</param>
    /// <param name="processingTime">処理時間。</param>
    /// <param name="memoryInfo">メモリ情報。</param>
    /// <param name="isOptimization">最適化結果かどうか（true: 推奨しきい値、false: 使用しきい値）。</param>
    /// <remarks>
    /// <para>【用途】</para>
    /// 自動最適化または定義削減の詳細結果を表示します。
    /// 
    /// <para>【表示優先度】</para>
    /// <list type="number">
    /// <item>推奨しきい値（大見出し）: ユーザーが最も知りたい情報</item>
    /// <item>削減後ファイル数（サマリー）: Base36/Base62の具体的な結果</item>
    /// <item>追加情報: シミュレーション情報や削減率</item>
    /// </list>
    /// </remarks>
    public void ShowResultCard(
        string thresholdValues,
        string resultFileCounts,
        string additionalInfo,
        string processingTime,
        string memoryInfo,
        bool isOptimization)
    {
        ResultThreshold = thresholdValues;
        ResultThresholdLabel = isOptimization ? "推奨しきい値" : "使用しきい値";
        ResultSummary = resultFileCounts;
        ResultReduction = additionalInfo;
        ResultTime = processingTime;
        ResultMargin = memoryInfo;
        ResultIcon = isOptimization ? "🔬" : "✓";
        IsResultOptimization = isOptimization;
        IsResultCardVisible = true;
    }

    /// <summary>
    /// 結果カードを非表示。
    /// </summary>
    public void HideResultCard()
    {
        IsResultCardVisible = false;
    }

    /// <summary>
    /// スライド確認ダイアログを表示。
    /// </summary>
    /// <remarks>
    /// ファイル上書き確認時に使用されます。
    /// ユーザーがスライド操作で確認することで、
    /// 誤操作を防ぎます。
    /// </remarks>
    public void ShowSlideConfirmation()
    {
        IsSlideConfirmationVisible = true;
    }
    private readonly DispatcherTimer _toastHideTimer;
    private bool _disposed;

    #region トースト通知プロパティ

    private string _toastMessage = string.Empty;
    public string ToastMessage
    {
        get => _toastMessage;
        set => SetProperty(ref _toastMessage, value);
    }

    private string _toastIcon = "✓";
    public string ToastIcon
    {
        get => _toastIcon;
        set => SetProperty(ref _toastIcon, value);
    }

    private bool _isToastVisible;
    public bool IsToastVisible
    {
        get => _isToastVisible;
        set
        {
            // 非表示にする場合のみタイマーを停止
            // （表示する場合はShowToast()経由でタイマーが起動されるため、ここでは処理不要）
            if (!value && _isToastVisible)
            {
                _toastHideTimer.Stop();
            }
            SetProperty(ref _isToastVisible, value);
        }
    }

    private bool _isToastError;
    public bool IsToastError
    {
        get => _isToastError;
        set => SetProperty(ref _isToastError, value);
    }

    #endregion

    #region 結果カードプロパティ

    private bool _isResultCardVisible;
    public bool IsResultCardVisible
    {
        get => _isResultCardVisible;
        set => SetProperty(ref _isResultCardVisible, value);
    }

    private string _resultThreshold = string.Empty;
    public string ResultThreshold
    {
        get => _resultThreshold;
        set => SetProperty(ref _resultThreshold, value);
    }

    private string _resultThresholdLabel = "推奨しきい値";
    /// <summary>
    /// しきい値ラベル（最適化時: 推奨しきい値、削減実行時: 使用しきい値）
    /// </summary>
    public string ResultThresholdLabel
    {
        get => _resultThresholdLabel;
        set => SetProperty(ref _resultThresholdLabel, value);
    }

    private string _resultSummary = string.Empty;
    public string ResultSummary
    {
        get => _resultSummary;
        set => SetProperty(ref _resultSummary, value);
    }

    private string _resultReduction = string.Empty;
    public string ResultReduction
    {
        get => _resultReduction;
        set => SetProperty(ref _resultReduction, value);
    }

    private string _resultTime = string.Empty;
    public string ResultTime
    {
        get => _resultTime;
        set => SetProperty(ref _resultTime, value);
    }

    private string _resultMargin = string.Empty;
    public string ResultMargin
    {
        get => _resultMargin;
        set => SetProperty(ref _resultMargin, value);
    }

    private string _resultIcon = "✨";
    public string ResultIcon
    {
        get => _resultIcon;
        set => SetProperty(ref _resultIcon, value);
    }

    private bool _isResultOptimization;
    public bool IsResultOptimization
    {
        get => _isResultOptimization;
        set => SetProperty(ref _isResultOptimization, value);
    }

    #endregion

    #region スライド確認プロパティ

    private bool _isSlideConfirmationVisible;
    public bool IsSlideConfirmationVisible
    {
        get => _isSlideConfirmationVisible;
        set => SetProperty(ref _isSlideConfirmationVisible, value);
    }

    #endregion

    #region パブリックメソッド

    /// <summary>
    /// タイマーによる自動非表示処理
    /// </summary>
    private void OnToastHideTimerTick(object? sender, EventArgs e)
    {
        _toastHideTimer.Stop();
        IsToastVisible = false;
    }

    public void HideSlideConfirmation()
    {
        IsSlideConfirmationVisible = false;
    }

    #endregion

    #region IDisposable実装

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // マネージドリソースの解放
            _toastHideTimer.Stop();
            _toastHideTimer.Tick -= OnToastHideTimerTick;
        }

        _disposed = true;
    }

    #endregion
}
