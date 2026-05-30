using BmsAtelierKyokufu.BmsPartTuner.UI.Views.Controls;
using BmsAtelierKyokufu.BmsPartTuner.UseCases;
using BmsAtelierKyokufu.BmsPartTuner.UseCases.Dto;
namespace BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;

/// <summary>
/// 音声ファイルの最適化と定義ファイル（BMS）の書き換え処理を制御するViewModel。
/// </summary>
[ADRAnchor("OPT-07", nameof(OptimizationViewModel))]
public partial class OptimizationViewModel : ObservableObject, IDataErrorInfo
{
    private static readonly Logger<OptimizationViewModel> s_logger = new();
    private readonly IBmsOptimizationUseCase _optimizationUseCase;
    private readonly IBmsOptimizationService _optimizationService;
    private readonly Progress<int> _progress;

    #region プロパティ

    private string _r2Threshold = AppConstants.Threshold.DefaultDisplay.ToString();

    /// <summary>
    /// 音声比較におけるマッチ許容度（しきい値）。
    /// </summary>
    public string R2Threshold
    {
        get => _r2Threshold;
        set
        {
            if (SetProperty(ref _r2Threshold, value))
            {
                OnPropertyChanged(nameof(IsInputValid));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// 最適化対象とするBMS定義の開始インデックス。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputValid))]
    public partial string DefinitionStart { get; set; } = AppConstants.Definition.Start;

    /// <summary>
    /// 最適化対象とするBMS定義の終了インデックス。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputValid))]
    public partial string DefinitionEnd { get; set; } = AppConstants.Definition.End;

    /// <summary>
    /// 現在の処理状況を示すステータスメッセージ。
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "準備完了";

    /// <summary>
    /// 現在の処理の進捗率（0〜100）。
    /// </summary>
    [ObservableProperty]
    public partial int ProgressValue { get; set; }

    /// <summary>
    /// 進捗が不定状態（インジケーターぐるぐる状態）かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; }

    /// <summary>
    /// 現在最適化処理が実行中かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// ローダーUIの表示フラグ。
    /// </summary>
    [ObservableProperty]
    public partial bool ShowLoader { get; set; }

    private bool _isPhysicalDeletionEnabled;

    /// <summary>
    /// 未使用となった音源ファイルを物理的に削除するかどうか。
    /// </summary>
    public bool IsPhysicalDeletionEnabled
    {
        get => _isPhysicalDeletionEnabled;
        set
        {
            if (SetProperty(ref _isPhysicalDeletionEnabled, value))
            {
                UpdateSlideConfirmationState();
            }
        }
    }

    /// <summary>
    /// スライド確認UIの指示テキスト。
    /// </summary>
    [ObservableProperty]
    public partial string SlideInstructionText { get; set; } = "スライドして上書き保存";

    /// <summary>
    /// スライド操作の方向。
    /// </summary>
    [ObservableProperty]
    public partial SlideDirection SwipeDirection { get; set; } = SlideDirection.LeftToRight;

    /// <summary>
    /// 処理中のローディングメッセージ。
    /// </summary>
    [ObservableProperty]
    public partial string LoadingMessage { get; set; } = string.Empty;

    /// <summary>
    /// 最後に実行された最適化処理の結果データ。
    /// </summary>
    [ObservableProperty]
    public partial OptimizationResult? LastOptimizationResult { get; set; }

    public SlideDirection SlideDirection =>
        IsPhysicalDeletionEnabled ? SlideDirection.RightToLeft : SlideDirection.LeftToRight;

    public string SlideInstruction =>
        IsPhysicalDeletionEnabled ? "スライドして音源ファイルを物理削除" : "スライドして上書きを確定";

    public bool IsInputValid
    {
        get
        {
            return string.IsNullOrEmpty(this[nameof(R2Threshold)]) &&
                   string.IsNullOrEmpty(this[nameof(DefinitionStart)]) &&
                   string.IsNullOrEmpty(this[nameof(DefinitionEnd)]) &&
                   !HasFormLevelError;
        }
    }

    public bool HasFormLevelError { get; private set; }

    private void SetFormError(string message)
    {
        HasFormLevelError = true;
        ErrorOccurred?.Invoke(this, message);
        WeakReferenceMessenger.Default.Send(new OptimizationErrorMessage(message));
    }

    private void ClearFormError()
    {
        HasFormLevelError = false;
    }

    private void UpdateSlideConfirmationState()
    {
        if (IsPhysicalDeletionEnabled)
        {
            SlideInstructionText = "上書きして不要な音源も削除する";
            SwipeDirection = SlideDirection.RightToLeft;
        }
        else
        {
            SlideInstructionText = "スライドして上書き保存";
            SwipeDirection = SlideDirection.LeftToRight;
        }
    }
    #endregion

    /// <summary>
    /// 定義削減処理が完了した際に発生するイベント。
    /// </summary>
    public event EventHandler<ReductionResultEventArgs>? DefinitionReductionCompleted;

    /// <summary>
    /// 処理中にエラーが発生した際に発生するイベント。
    /// </summary>
    public event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// 処理中に警告が発生した際に発生するイベント。
    /// </summary>
    public event EventHandler<string>? WarningOccurred;

    public OptimizationViewModel(IBmsOptimizationService optimizationService, IBmsOptimizationUseCase optimizationUseCase)
    {
        _optimizationService = optimizationService ?? throw new ArgumentNullException(nameof(optimizationService));
        _optimizationUseCase = optimizationUseCase ?? throw new ArgumentNullException(nameof(optimizationUseCase));
        UpdateSlideConfirmationState();

        _progress = new Progress<int>(percent =>
        {
            if (!IsBusy) return;

            ProgressValue = percent;
            IsProgressIndeterminate = false;

            var message = percent switch
            {
                < 10 => "波形データを解析中...",
                < 50 => "シナリオをシミュレーション中...",
                < 80 => "最適値を探索中...",
                100 => "完了しました。",
                _ => "実行中..."
            };

            LoadingMessage = message;
            StatusMessage = message;
        });
    }

    private async Task StartDelayedLoaderAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Core.AppConstants.UI.LoaderDelayMs, cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
            {
                await Application.Current.Dispatcher.InvokeAsync(() => ShowLoader = true);
            }
        }
        catch (TaskCanceledException)
        {
            // 処理が高速に完了した場合は無視
        }
    }

    private CancellationTokenSource BeginBusyState(string initialMessage)
    {
        IsBusy = true;
        ShowLoader = false;
        LoadingMessage = initialMessage;
        ProgressValue = 0;
        IsProgressIndeterminate = true;

        var cts = new CancellationTokenSource();
        _ = StartDelayedLoaderAsync(cts.Token);

        return cts;
    }

    private void EndBusyState(CancellationTokenSource cts)
    {
        cts.Cancel();
        cts.Dispose();

        IsBusy = false;
        ShowLoader = false;
        LoadingMessage = string.Empty;
    }

    /// <summary>
    /// しきい値の最適化シミュレーションを実行します。
    /// </summary>
    public async Task<OptimizationResult?> ExecuteThresholdOptimizationAsync(
        List<string> files,
        int startDefinition,
        int endDefinition)
    {
        var loaderCts = BeginBusyState("🎵 波形データを解析中...");
        StatusMessage = "🔬 しきい値最適化シミュレーション実行中...";

        try
        {
            var request = new ThresholdOptimizationRequest
            {
                BmsFileList = files,
                StartDefinition = startDefinition,
                EndDefinition = endDefinition,
                Progress = _progress
            };

            var result = await _optimizationUseCase.ExecuteThresholdOptimizationAsync(request);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsProgressIndeterminate = false;
                ProgressValue = 100;

                if (result.IsSuccess && result.Data != null)
                {
                    LastOptimizationResult = result.Data;

                    var execTime = result.Data.ExecutionTime.TotalSeconds;
                    var memoryMb = result.Data.MemoryUsedBytes / 1024.0 / 1024.0;

                    StatusMessage = $"✨ 最適化完了 | Base36: {result.Data.Base36Result.Threshold:P0}, " +
                                   $"Base62: {result.Data.Base62Result.Threshold:P0} " +
                                   $"({execTime:F1}s, {memoryMb:F1}MB)";

                    if (result.Data.HasWarnings)
                    {
                        var warningMessage = string.Join("\n", result.Data.Warnings);
                        WarningOccurred?.Invoke(this, warningMessage);
                    }
                }
                else
                {
                    StatusMessage = "最適化に失敗しました";
                    ErrorOccurred?.Invoke(this, result.ErrorMessage ?? "最適化エラー");
                    WeakReferenceMessenger.Default.Send(new OptimizationErrorMessage(result.ErrorMessage ?? "最適化エラー"));
                }
            });

            return result.Data;
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsProgressIndeterminate = false;
                ErrorOccurred?.Invoke(this, $"最適化エラー: {ex.Message}");
                WeakReferenceMessenger.Default.Send(new OptimizationErrorMessage($"最適化エラー: {ex.Message}"));
                StatusMessage = "最適化エラー";
            });

            s_logger.WriteDebug("=== ExecuteThresholdOptimizationAsync Exception ===");
            s_logger.WriteDebug($"Exception Type: {ex.GetType().FullName}");
            s_logger.WriteDebug($"Message: {ex.Message}");
            s_logger.WriteDebug($"StackTrace: {ex.StackTrace}");

            return null;
        }
        finally
        {
            EndBusyState(loaderCts);

            await Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });
        }
    }

    /// <summary>
    /// BMSの定義削減処理を実行します。
    /// </summary>
    public async Task ExecuteDefinitionReductionAsync(
        BmsDefinitionManager? bmsFileList,
        string? inputPath,
        string? outputPath,
        string? inputBmsContent = null,
        IEnumerable<string>? selectedKeywords = null)
    {
        if (bmsFileList == null)
        {
            ErrorOccurred?.Invoke(this, "BMS/BMSONファイルが読み込まれていません");
            WeakReferenceMessenger.Default.Send(new OptimizationErrorMessage("BMS/BMSONファイルが読み込まれていません"));
            return;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            ErrorOccurred?.Invoke(this, "入力BMS/BMSONファイルを指定してください");
            WeakReferenceMessenger.Default.Send(new OptimizationErrorMessage("入力BMS/BMSONファイルを指定してください"));
            return;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            ErrorOccurred?.Invoke(this, "出力先を指定してください");
            WeakReferenceMessenger.Default.Send(new OptimizationErrorMessage("出力先を指定してください"));
            return;
        }

        var r2Result = _optimizationService.ValidateR2Threshold(R2Threshold);
        if (!r2Result.IsValid)
        {
            ErrorOccurred?.Invoke(this, r2Result.GetFirstError());
            WeakReferenceMessenger.Default.Send(new OptimizationErrorMessage(r2Result.GetFirstError()));
            return;
        }

        await ExecuteDefinitionReductionInternalAsync(bmsFileList, inputPath, outputPath, r2Result.Value, inputBmsContent, selectedKeywords);
    }

    private async Task ExecuteDefinitionReductionInternalAsync(
        BmsDefinitionManager bmsFileList,
        string inputPath,
        string outputPath,
        float r2Val,
        string? inputBmsContent = null,
        IEnumerable<string>? selectedKeywords = null)
    {
        var loaderCts = BeginBusyState("📁 ファイルを処理中...");

        try
        {
            var request = new DefinitionReductionRequest
            {
                BmsFileList = bmsFileList,
                InputPath = inputPath,
                OutputPath = outputPath,
                R2Threshold = r2Val,
                StartDefinition = RadixConvert.ZZToInt(DefinitionStart),
                EndDefinition = RadixConvert.ZZToInt(DefinitionEnd),
                IsPhysicalDeletionEnabled = IsPhysicalDeletionEnabled,
                InputBmsContent = inputBmsContent,
                Progress = _progress,
                SelectedKeywords = selectedKeywords
            };

            var result = await _optimizationUseCase.ExecuteDefinitionReductionAsync(request);

            if (result.IsSuccess && result.Data != null)
            {
                var data = result.Data;
                var deletedMsg = data.DeletedFilesCount > 0 ? $" (削除: {data.DeletedFilesCount}ファイル)" : "";
                StatusMessage = $"完了: {Path.GetFileName(outputPath)} ({data.OriginalCount}→{data.OptimizedCount}ファイル){deletedMsg}";

                var args = new ReductionResultEventArgs
                {
                    Result = data,
                    OutputPath = outputPath,
                    Threshold = r2Val
                };
                DefinitionReductionCompleted?.Invoke(this, args);
                WeakReferenceMessenger.Default.Send(new DefinitionReductionCompletedMessage(args.Result, args.OutputPath, args.Threshold));
            }
            else
            {
                ErrorOccurred?.Invoke(this, result.ErrorMessage ?? "処理エラー");
                WeakReferenceMessenger.Default.Send(new OptimizationErrorMessage(result.ErrorMessage ?? "処理エラー"));
                StatusMessage = "処理エラー";
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"処理エラー: {ex.Message}");
            WeakReferenceMessenger.Default.Send(new OptimizationErrorMessage($"処理エラー: {ex.Message}"));
            StatusMessage = "処理エラー";
            s_logger.WriteDebug($"ExecuteDefinitionReductionInternalAsync Exception: {ex}");
        }
        finally
        {
            EndBusyState(loaderCts);
            IsBusy = false;

            await Task.Run(() => s_logger.WriteDebug("=== OptimizationViewModel: Clearing caches ==="));
        }
    }

    public string Error => string.Empty;

    public string this[string columnName]
    {
        get
        {
            string? error = null;

            switch (columnName)
            {
                case nameof(R2Threshold):
                    error = Validators.OptimizationInputValidator.ValidateR2Threshold(R2Threshold);
                    if (!string.IsNullOrEmpty(error) && error.Contains("のみを入力"))
                    {
                        SetFormError(error);
                    }
                    break;

                case nameof(DefinitionStart):
                    error = Validators.OptimizationInputValidator.ValidateDefinitionStart(DefinitionStart, DefinitionEnd);
                    if (!string.IsNullOrEmpty(error) && error.Contains("のみを入力"))
                    {
                        SetFormError(error);
                    }
                    break;

                case nameof(DefinitionEnd):
                    error = Validators.OptimizationInputValidator.ValidateDefinitionEnd(DefinitionEnd, DefinitionStart);
                    if (!string.IsNullOrEmpty(error) && error.Contains("のみを入力"))
                    {
                        SetFormError(error);
                    }
                    break;
            }

            return error ?? string.Empty;
        }
    }

    /// <summary>
    /// 最適化結果イベントの引数を提供します。
    /// </summary>
    public class OptimizationResultEventArgs : EventArgs
    {
        public object? Result { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 削減結果イベントの引数を提供します。
    /// </summary>
    public class ReductionResultEventArgs : EventArgs
    {
        public object? Result { get; set; }
        public string OutputPath { get; set; } = string.Empty;
        public float Threshold { get; set; }
    }
}

