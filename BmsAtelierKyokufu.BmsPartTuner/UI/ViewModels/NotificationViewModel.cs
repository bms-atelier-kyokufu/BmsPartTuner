namespace BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;

/// <summary>
/// アプリケーション内の各種通知（トースト、結果カード、スライド確認）の表示状態を管理するViewModel。
/// </summary>
public partial class NotificationViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _toastHideTimer;
    private bool _disposed;

    public NotificationViewModel()
    {
        _toastHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Core.AppConstants.UI.ToastDisplayDurationMs)
        };
        _toastHideTimer.Tick += OnToastHideTimerTick;
    }

    /// <summary>
    /// 指定されたメッセージとアイコンでトースト通知を表示します。
    /// 一定時間後に自動的に非表示になります。
    /// </summary>
    public void ShowToast(string message, string icon = "✓", bool isError = false)
    {
        _toastHideTimer.Stop();

        ToastMessage = message;
        ToastIcon = icon;
        IsToastError = isError;
        IsToastVisible = true;

        _toastHideTimer.Start();
    }

    /// <summary>
    /// トースト通知を即座に非表示にします。
    /// </summary>
    public void HideToast()
    {
        IsToastVisible = false;
    }

    /// <summary>
    /// 最適化や削減処理の詳細な結果を示すカードを表示します。
    /// </summary>
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
    /// 結果カードを非表示にします。
    /// </summary>
    public void HideResultCard()
    {
        IsResultCardVisible = false;
    }

    /// <summary>
    /// ファイル上書き等の重要な操作前に、スライド確認ダイアログを表示します。
    /// </summary>
    public void ShowSlideConfirmation()
    {
        IsSlideConfirmationVisible = true;
    }

    /// <summary>
    /// スライド確認ダイアログを非表示にします。
    /// </summary>
    public void HideSlideConfirmation()
    {
        IsSlideConfirmationVisible = false;
    }

    private void OnToastHideTimerTick(object? sender, EventArgs e)
    {
        _toastHideTimer.Stop();
        IsToastVisible = false;
    }

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
            _toastHideTimer.Stop();
            _toastHideTimer.Tick -= OnToastHideTimerTick;
        }

        _disposed = true;
    }

    #endregion
}
