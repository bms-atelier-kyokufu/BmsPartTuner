using System.Diagnostics;
using System.Threading;
using System.Windows;
using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

public partial class OptimizationViewModel : ObservableObject, IDataErrorInfo
{
    private readonly IBmsOptimizationService _optimizationService;
    private readonly Progress<int> _progress;

    #region プロパティ

    private string _r2Threshold = AppConstants.Threshold.DefaultDisplay.ToString();
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputValid))]
    private string _definitionStart = AppConstants.Definition.Start;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputValid))]
    private string _definitionEnd = AppConstants.Definition.End;

    [ObservableProperty]
    private string _statusMessage = "準備完了";

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// ローダー表示フラグ。
    /// <see cref="LoaderDelayMs"/>後に表示されるため、高速処理時のチラつきを防止。
    /// </summary>
    [ObservableProperty]
    private bool _showLoader;

    /// <summary>
    /// 音源ファイルの物理削除を有効にするかどうか。
    /// </summary>
    private bool _isPhysicalDeletionEnabled;
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
    private string _slideInstructionText = "スライドして上書き保存";

    /// <summary>
    /// スライド方向。
    /// </summary>
    [ObservableProperty]
    private Controls.SlideDirection _swipeDirection = Controls.SlideDirection.LeftToRight;

    /// <summary>
    /// 現在の処理状況を示すローディングメッセージ。
    /// </summary>
    [ObservableProperty]
    private string _loadingMessage = string.Empty;

    /// <summary>
    /// 最後の最適化結果（パフォーマンス指標表示用）。
    /// </summary>
    [ObservableProperty]
    private Models.OptimizationResult? _lastOptimizationResult;

    public Controls.SlideDirection SlideDirection =>
        IsPhysicalDeletionEnabled ? Controls.SlideDirection.RightToLeft : Controls.SlideDirection.LeftToRight;

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

    // フォーム全体のエラーフラグ
    public bool HasFormLevelError { get; private set; }

    private void SetFormError(string message)
    {
        HasFormLevelError = true;
        ErrorOccurred?.Invoke(this, message);
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
            SwipeDirection = Controls.SlideDirection.RightToLeft;
        }
        else
        {
            SlideInstructionText = "スライドして上書き保存";
            SwipeDirection = Controls.SlideDirection.LeftToRight;
        }
    }
    #endregion

    public event EventHandler<ReductionResultEventArgs>? DefinitionReductionCompleted;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? WarningOccurred;

    public OptimizationViewModel(IBmsOptimizationService optimizationService)
    {
        _optimizationService = optimizationService ?? throw new ArgumentNullException(nameof(optimizationService));
        UpdateSlideConfirmationState();

        _progress = new Progress<int>(percent =>
        {
            ProgressValue = percent;
            IsProgressIndeterminate = false;

            // 進捗に応じてメッセージを更新
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

    /// <summary>
    /// 遅延ローダー制御を開始します。
    /// 高速処理時のローダーチラつきを防止するため、一定時間後にローダーを表示します。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>ローダー表示タスク。</returns>
    private async Task StartDelayedLoaderAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Core.AppConstants.UI.LoaderDelayMs, cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ShowLoader = true;
                });
            }
        }
        catch (TaskCanceledException)
        {
            // 処理が高速に完了した場合は無視
        }
    }

    /// <summary>
    /// ビジー状態を開始します（遅延ローダー制御付き）。
    /// </summary>
    /// <param name="initialMessage">初期ローディングメッセージ。</param>
    /// <returns>キャンセルトークンソース（ローダー制御用）。</returns>
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

    /// <summary>
    /// ビジー状態を終了します。
    /// </summary>
    /// <param name="cts">ローダー制御用キャンセルトークンソース。</param>
    private void EndBusyState(CancellationTokenSource cts)
    {
        cts.Cancel();
        cts.Dispose();

        IsBusy = false;
        ShowLoader = false;
        LoadingMessage = string.Empty;
    }

    /// <summary>
    /// 100回シミュレーションによる最適しきい値探索を実行します。
    /// </summary>
    /// <param name="files">ファイルパスリスト。</param>
    /// <param name="startDefinition">開始定義。</param>
    /// <param name="endDefinition">終了定義。</param>
    /// <returns>最適化結果。</returns>
    public async Task<Models.OptimizationResult?> ExecuteThresholdOptimizationAsync(
        List<string> files,
        int startDefinition,
        int endDefinition)
    {
        if (files == null || files.Count == 0)
        {
            ErrorOccurred?.Invoke(this, "ファイルリストが空です");
            return null;
        }

        var loaderCts = BeginBusyState("🎵 波形データを解析中...");
        StatusMessage = "🔬 しきい値最適化シミュレーション実行中...";

        try
        {
            var result = await _optimizationService.FindOptimalThresholdsAsync(
                files,
                startDefinition,
                endDefinition,
                _progress);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsProgressIndeterminate = false;
                ProgressValue = 100;

                if (result != null)
                {
                    LastOptimizationResult = result;

                    // パフォーマンス情報をステータスに表示
                    var execTime = result.ExecutionTime.TotalSeconds;
                    var memoryMb = result.MemoryUsedBytes / 1024.0 / 1024.0;

                    StatusMessage = $"✨ 最適化完了 | Base36: {result.Base36Result.Threshold:P0}, " +
                                   $"Base62: {result.Base62Result.Threshold:P0} " +
                                   $"({execTime:F1}s, {memoryMb:F1}MB)";

                    // 警告がある場合は表示
                    if (result.HasWarnings)
                    {
                        var warningMessage = string.Join("\n", result.Warnings);
                        WarningOccurred?.Invoke(this, warningMessage);
                    }
                }
                else
                {
                    StatusMessage = "最適化に失敗しました";
                }
            });

            return result;
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsProgressIndeterminate = false;
                ErrorOccurred?.Invoke(this, $"最適化エラー: {ex.Message}");
                StatusMessage = "最適化エラー";
            });

            Debug.WriteLine($"=== ExecuteThresholdOptimizationAsync Exception ===");
            Debug.WriteLine($"Exception Type: {ex.GetType().FullName}");
            Debug.WriteLine($"Message: {ex.Message}");
            Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            return null;
        }
        finally
        {
            EndBusyState(loaderCts);

            // メモリリーク対策: 処理完了後にGCを実行してメモリを解放
            // 注: キャッシュのクリアはBmsOptimizationServiceで実施
            await Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });
        }
    }

    public async Task ExecuteDefinitionReductionAsync(
        Models.FileList? bmsFileList,
        string? inputPath,
        string? outputPath,
        IEnumerable<string>? selectedKeywords = null)
    {
        if (bmsFileList == null)
        {
            ErrorOccurred?.Invoke(this, "BMSファイルが読み込まれていません");
            return;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            ErrorOccurred?.Invoke(this, "入力BMSファイルを指定してください");
            return;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            ErrorOccurred?.Invoke(this, "出力先を指定してください");
            return;
        }

        var r2Result = _optimizationService.ValidateR2Threshold(R2Threshold);
        if (!r2Result.IsValid)
        {
            ErrorOccurred?.Invoke(this, r2Result.GetFirstError());
            return;
        }

        await ExecuteDefinitionReductionInternalAsync(bmsFileList, inputPath, outputPath, r2Result.Value, selectedKeywords);
    }

    private async Task ExecuteDefinitionReductionInternalAsync(
        Models.FileList bmsFileList,
        string inputPath,
        string outputPath,
        float r2Val,
        IEnumerable<string>? selectedKeywords = null)
    {
        var loaderCts = BeginBusyState("📁 ファイルを処理中...");

        try
        {
            var result = await _optimizationService.ExecuteDefinitionReductionAsync(
                bmsFileList.GetFileList(),
                inputPath.Trim('"'),
                outputPath.Trim('"'),
                r2Val,
                RadixConvert.ZZToInt(DefinitionStart),
                RadixConvert.ZZToInt(DefinitionEnd),
                IsPhysicalDeletionEnabled,
                _progress,
                selectedKeywords);

            if (result.IsSuccess)
            {
                var deletedMsg = result.DeletedFilesCount > 0 ? $" (削除: {result.DeletedFilesCount}ファイル)" : "";
                StatusMessage = $"完了: {Path.GetFileName(outputPath)} ({result.OriginalCount}→{result.OptimizedCount}ファイル){deletedMsg}";

                DefinitionReductionCompleted?.Invoke(this, new ReductionResultEventArgs
                {
                    Result = result,
                    OutputPath = outputPath,
                    Threshold = r2Val
                });
            }
            else
            {
                ErrorOccurred?.Invoke(this, $"処理エラー: {result.ErrorMessage}");
                StatusMessage = "処理エラー";
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"処理エラー: {ex.Message}");
            StatusMessage = "処理エラー";
            Debug.WriteLine($"ExecuteDefinitionReductionInternalAsync Exception: {ex}");
        }
        finally
        {
            EndBusyState(loaderCts);
            IsBusy = false;

            // メモリリーク対策: 処理完了後にキャッシュをクリアしてメモリを解放
            await Task.Run(() =>
            {
                Debug.WriteLine("=== OptimizationViewModel: Clearing caches ===");
                bmsFileList.ClearAllCaches();
            });
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
                    if (string.IsNullOrWhiteSpace(R2Threshold))
                    {
                        error = "マッチ許容度を入力してください";
                    }
                    else
                    {
                        // %記号を削除して数値をパース、非ASCIIはブロック
                        var raw = R2Threshold;
                        if (raw.Any(c => c > 0x7F))
                        {
                            error = "半角数字のみを入力してください";
                            SetFormError(error);
                            break;
                        }

                        var valueText = raw.TrimEnd('%').Trim();
                        if (!int.TryParse(valueText, out var displayValue))
                        {
                            error = "有効な数値を入力してください";
                        }
                        else if (displayValue < AppConstants.Threshold.MinDisplay || displayValue > AppConstants.Threshold.MaxDisplay)
                        {
                            error = $"マッチ許容度は{AppConstants.Threshold.MinDisplay}～{AppConstants.Threshold.MaxDisplay}の範囲で入力してください";
                        }
                    }
                    break;

                case nameof(DefinitionStart):
                    if (string.IsNullOrWhiteSpace(DefinitionStart) || DefinitionStart.Length != 2)
                    {
                        error = "2桁で入力してください";
                    }
                    else
                    {
                        // 非ASCIIを排除
                        if (DefinitionStart.Any(c => c > 0x7F))
                        {
                            error = "英数字のみを入力してください";
                            SetFormError(error);
                            break;
                        }

                        try
                        {
                            var startVal = RadixConvert.ZZToInt(DefinitionStart);
                            if (startVal < 1)
                            {
                                error = "01以上を入力してください";
                            }
                            else if (!string.IsNullOrWhiteSpace(DefinitionEnd))
                            {
                                var endVal = RadixConvert.ZZToInt(DefinitionEnd);
                                if (startVal >= endVal)
                                {
                                    error = "終了より小さい値にしてください";
                                }
                            }
                        }
                        catch
                        {
                            error = "有効な値を入力してください";
                        }
                    }
                    break;

                case nameof(DefinitionEnd):
                    if (string.IsNullOrWhiteSpace(DefinitionEnd) || DefinitionEnd.Length != 2)
                    {
                        error = "2桁で入力してください";
                    }
                    else
                    {
                        if (DefinitionEnd.Any(c => c > 0x7F))
                        {
                            error = "英数字のみを入力してください";
                            SetFormError(error);
                            break;
                        }

                        if (DefinitionEnd.Equals("00", StringComparison.OrdinalIgnoreCase))
                        {
                            // 00は許可（ファイルから推定）
                        }
                        else if (!string.IsNullOrWhiteSpace(DefinitionStart))
                        {
                            try
                            {
                                var startVal = RadixConvert.ZZToInt(DefinitionStart);
                                var endVal = RadixConvert.ZZToInt(DefinitionEnd);
                                if (endVal != 0 && endVal <= startVal)
                                {
                                    error = "開始より大きい値または00を入力してください";
                                }
                            }
                            catch
                            {
                                error = "有効な値を入力してください";
                            }
                        }
                    }
                    break;
            }

            return error ?? string.Empty;
        }
    }

    public class OptimizationResultEventArgs : EventArgs
    {
        public object? Result { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ReductionResultEventArgs : EventArgs
    {
        public object? Result { get; set; }
        public string OutputPath { get; set; } = string.Empty;
        public float Threshold { get; set; }
    }
}
