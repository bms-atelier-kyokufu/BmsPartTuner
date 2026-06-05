using BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.Controllers;

/// <summary>
/// アプリケーション全体のユースケース実行フローを制御するコントローラークラス。
/// 肥大化した MainViewModel からフロー制御ロジックを分離するために導入されました。
/// </summary>
public class AppController(
    MainViewModel mainViewModel,
    IBmsonConversionService bmsonConversionService,
    IFileSystemService fileSystemService)
{
    private readonly IBmsonConversionService _bmsonConversionService = bmsonConversionService ?? throw new ArgumentNullException(nameof(bmsonConversionService));
    private readonly IFileSystemService _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    private readonly MainViewModel _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
    private static readonly Logger<AppController> s_logger = new();
    private bool _suppressHideResultCard;

    public string? WorkingBmsPath { get; private set; }
    public string? WorkingBmsContent { get; private set; }
    public string? LastDownconvertedBmsonPath { get; private set; }
    public bool IsDownconverting { get; private set; }

    public async Task ExecuteThresholdOptimizationAsync(CancellationToken cancellationToken = default)
    {
        var inputPath = GetAndValidateInputPath();
        if (string.IsNullOrEmpty(inputPath)) return;

        var files = GetFileListToOptimize();
        if (files == null) return;

        _mainViewModel.HideResultCard();
        SetBusyState(true, "しきい値最適化シミュレーション開始...");

        int startDef = RadixConvert.ZZToInt(_mainViewModel.Optimization.DefinitionStart);
        int endDef = RadixConvert.ZZToInt(_mainViewModel.Optimization.DefinitionEnd);

        var inputToUse = WorkingBmsPath ?? _mainViewModel.FileOperations.InputPath;

        // OptimizationViewModelの_progressと上部グローバルプログレスバーを同期させる
        _mainViewModel.Optimization.ProgressChanged += OnSimulationProgressChanged;

        try
        {
            var result = await _mainViewModel.Optimization.ExecuteThresholdOptimizationAsync(
                inputToUse,
                files,
                startDef,
                endDef,
                cancellationToken);

            HandleOptimizationResult(result);
        }
        finally
        {
            _mainViewModel.Optimization.ProgressChanged -= OnSimulationProgressChanged;
            SetBusyState(false, string.Empty);
        }
    }

    private void OnSimulationProgressChanged(object? sender, int percent)
    {
        _mainViewModel.GlobalProgressValue = percent;
    }

    public async Task ExecuteReductionAsync(CancellationToken cancellationToken = default)
    {
        var inputToUse = WorkingBmsPath ?? _mainViewModel.FileOperations.InputPath;

        // BMSONファイルがそのまま渡されており、かつダウンコンバート済みコンテンツがない場合はブロックする
        if (Path.GetExtension(inputToUse).Equals(".bmson", StringComparison.OrdinalIgnoreCase) && WorkingBmsContent == null)
        {
            _mainViewModel.ShowToast("BMSONファイルは直接削減できません。自動ダウンコンバートされたBMSファイルがセットされるまでお待ち下さい。", "⚠", true);
            _mainViewModel.StatusMessage = "BMSONファイルは直接削減できません。自動ダウンコンバートされたBMSファイルがセットされるまでお待ち下さい。";
            return;
        }

        if (_mainViewModel.FileOperations.CheckOverwriteRequired() || _mainViewModel.Optimization.IsPhysicalDeletionEnabled)
        {
            _mainViewModel.InvokeSlideConfirmationRequested();
            return;
        }

        await ExecuteDefinitionReductionInternalAsync(inputToUse, cancellationToken);
    }

    public async Task ExecuteDefinitionReductionAfterConfirmationAsync(CancellationToken cancellationToken = default)
    {
        var inputToUse = WorkingBmsPath ?? _mainViewModel.FileOperations.InputPath;
        await ExecuteDefinitionReductionInternalAsync(inputToUse, cancellationToken);
    }

    private async Task ExecuteDefinitionReductionInternalAsync(string? inputToUse, CancellationToken cancellationToken = default)
    {
        var selectedKeywords = _mainViewModel.BmsDefinitionManager.GetSelectedKeywords();

        await _mainViewModel.Optimization.ExecuteDefinitionReductionAsync(
            _mainViewModel.BmsDefinitionManager.BmsFileList,
            inputToUse,
            _mainViewModel.FileOperations.OutputPath,
            WorkingBmsContent,
            selectedKeywords,
            cancellationToken);

        // If cancellation was requested, skip further processing and clear busy flag.
        if (cancellationToken.IsCancellationRequested)
        {
            // Ensure UI reflects cancellation; status will be set by OptimizationViewModel.
            _mainViewModel.IsBusy = false;
            return;
        }

        if (string.Equals(inputToUse, _mainViewModel.FileOperations.OutputPath, StringComparison.OrdinalIgnoreCase))
        {
            if (inputToUse != null && _fileSystemService.FileExists(inputToUse))
            {
                _mainViewModel.IsBusy = true;
                _mainViewModel.IsGlobalProgressIndeterminate = true;
                _mainViewModel.StatusMessage = "リストを再読み込み中...";
                await _mainViewModel.BmsDefinitionManager.LoadBmsFileAsync(inputToUse, cancellationToken: cancellationToken);
                _mainViewModel.IsGlobalProgressIndeterminate = false;
                _mainViewModel.IsBusy = false;
                _mainViewModel.StatusMessage = "準備完了";
            }
        }
        else
        {
            // 別名保存の場合は入力パスを切り替えて読み込み
            _suppressHideResultCard = true;
            _mainViewModel.FileOperations.InputPath = _mainViewModel.FileOperations.OutputPath;
            _suppressHideResultCard = false;
        }
    }

    public void HandleInputPathChanged(string? path)
    {
        // 入力パスが変更（または新規ファイル読み込み）されたタイミングでリザルトカードを隠す
        // プログラムからの変更（削減後など）ではリザルトカードを消さない
        if (!_suppressHideResultCard)
        {
            _mainViewModel.HideResultCard();
        }

        _ = ProcessInputPathAsync(path);
    }

    private async Task ProcessInputPathAsync(string? path)
    {
        if (path != null && _fileSystemService.FileExists(path))
        {
            var cts = new System.Threading.CancellationTokenSource();
            _mainViewModel.SetActiveCts(cts);

            var extension = Path.GetExtension(path);
            try
            {
                if (string.Equals(extension, ".bmson", StringComparison.OrdinalIgnoreCase))
                {
                    // すでにダウンコンバート済みの同じファイルなら再変換をスキップ
                    if (string.Equals(path, LastDownconvertedBmsonPath, StringComparison.OrdinalIgnoreCase) && WorkingBmsContent != null)
                    {
                        _mainViewModel.IsBusy = true;
                        _mainViewModel.IsGlobalProgressIndeterminate = true;
                        _mainViewModel.StatusMessage = "リストを読み込み中...";
                        await _mainViewModel.BmsDefinitionManager.LoadBmsFileAsync(path, WorkingBmsContent, cts.Token);
                        _mainViewModel.IsGlobalProgressIndeterminate = false;
                        _mainViewModel.IsBusy = false;
                        _mainViewModel.StatusMessage = "準備完了";
                        return;
                    }

                    if (IsDownconverting) return;

                    await DownconvertBmsonAsync(path, cts.Token);
                }
                else
                {
                    VirtualAudioRegistry.Clear();
                    ClearProcessedAudioRegistryIfDirectoryChanged(path);

                    WorkingBmsPath = path;
                    WorkingBmsContent = null;
                    LastDownconvertedBmsonPath = null; // 別のファイルが来たらクリア

                    _mainViewModel.IsBusy = true;
                    _mainViewModel.IsGlobalProgressIndeterminate = true;
                    _mainViewModel.StatusMessage = "リストを読み込み中...";
                    await _mainViewModel.BmsDefinitionManager.LoadBmsFileAsync(path, cancellationToken: cts.Token);
                    _mainViewModel.IsGlobalProgressIndeterminate = false;
                    _mainViewModel.IsBusy = false;
                    _mainViewModel.StatusMessage = "準備完了";
                }
            }
            catch (System.OperationCanceledException)
            {
                // キャンセル＝そのファイルはいらない → キャッシュ・状態・UIパスを全て破棄する
                AudioRegistry.Instance.Clear();
                VirtualAudioRegistry.Clear();
                WorkingBmsPath = null;
                WorkingBmsContent = null;
                LastDownconvertedBmsonPath = null;
                _mainViewModel.BmsDefinitionManager.FileListItems.Clear();
                _mainViewModel.FileOperations.InputPath = string.Empty;
                _mainViewModel.FileOperations.OutputPath = string.Empty;
                _mainViewModel.IsGlobalProgressIndeterminate = false;
                _mainViewModel.IsBusy = false;
                _mainViewModel.StatusMessage = "読み込みがキャンセルされました";
                _mainViewModel.ShowToast("ファイルの読み込みがキャンセルされました", "✕", false);
            }
            catch (System.Exception ex)
            {
                _mainViewModel.IsGlobalProgressIndeterminate = false;
                _mainViewModel.IsBusy = false;
                _mainViewModel.StatusMessage = "読み込みエラー";
                _mainViewModel.ShowToast($"読み込みエラー: {ex.Message}", "⚠", true);
            }
            finally
            {
                _mainViewModel.SetActiveCts(null);
                cts.Dispose();
            }
        }
        else
        {
            VirtualAudioRegistry.Clear();
            AudioRegistry.Instance.Clear();

            WorkingBmsPath = null;
            WorkingBmsContent = null;
            LastDownconvertedBmsonPath = null; // ファイルが存在しない場合もクリア
            if (!string.IsNullOrWhiteSpace(path))
            {
                var pattern = string.Join(", ", AppConstants.Files.SupportedBmsExtensions);
                _mainViewModel.StatusMessage = $"対応形式: {pattern}";
                _mainViewModel.BmsDefinitionManager.FileListItems.Clear();
            }
        }
    }

    private async Task DownconvertBmsonAsync(string path, System.Threading.CancellationToken cancellationToken)
    {
        IsDownconverting = true;
        _mainViewModel.IsBusy = true;
        _mainViewModel.StatusMessage = "bmsonをダウンコンバート中...";
        try
        {
            using (s_logger.MeasureTime("Total Flow (Downconvert + LoadBmsFile)"))
            {
                Core.Audio.Virtual.VirtualAudioRegistry.Clear();
                ClearProcessedAudioRegistryIfDirectoryChanged(path);


                var progress = new Progress<int>(percent =>
                {
                    if (_mainViewModel.IsGlobalProgressIndeterminate)
                    {
                        _mainViewModel.IsGlobalProgressIndeterminate = false;
                    }
                    _mainViewModel.GlobalProgressValue = percent;
                });

                _mainViewModel.IsGlobalProgressIndeterminate = true;
                cancellationToken.ThrowIfCancellationRequested();
                string bmsText = await _bmsonConversionService.GenerateBmsTextAsync(path, keyNotesOnly: false, progress, cancellationToken);

                WorkingBmsPath = path;
                WorkingBmsContent = bmsText;
                LastDownconvertedBmsonPath = path; // 成功時にパスを記憶

                _mainViewModel.StatusMessage = "リストを構築中...";
                cancellationToken.ThrowIfCancellationRequested();
                await _mainViewModel.BmsDefinitionManager.LoadBmsFileAsync(path, bmsText, cancellationToken);
                _mainViewModel.IsGlobalProgressIndeterminate = false;
            }
            _mainViewModel.ShowToast($"bmsonをダウンコンバートしました: {Path.GetFileName(path)}", "📁", false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            string errorMessage = ex is AggregateException aggEx && aggEx.InnerExceptions.Count > 0
                ? aggEx.InnerExceptions[0].Message
                : ex.Message;

            _mainViewModel.ShowToast($"bmson変換失敗: {errorMessage}", "⚠", true);
            _mainViewModel.BmsDefinitionManager.FileListItems.Clear();
            LastDownconvertedBmsonPath = null; // 失敗時はクリア
            WorkingBmsContent = null;
        }
        finally
        {
            IsDownconverting = false;
            _mainViewModel.IsBusy = false;
            _mainViewModel.StatusMessage = "準備完了";
            _mainViewModel.NotifyCanExecuteReductionChanged();
        }
    }

    private void ClearProcessedAudioRegistryIfDirectoryChanged(string? newPath)
    {
        try
        {
            var oldDir = !string.IsNullOrEmpty(WorkingBmsPath) ? Path.GetDirectoryName(Path.GetFullPath(WorkingBmsPath)) : null;
            var newDir = !string.IsNullOrEmpty(newPath) ? Path.GetDirectoryName(Path.GetFullPath(newPath)) : null;
            if (oldDir == null || newDir == null || !string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase))
            {
                Core.Audio.AudioRegistry.Instance.Clear();
            }
        }
        catch
        {
            Core.Audio.AudioRegistry.Instance.Clear();
        }
    }

    #region Refactored Helpers for ExecuteThresholdOptimizationAsync

    private string GetAndValidateInputPath()
    {
        var inputPath = (WorkingBmsPath ?? _mainViewModel.FileOperations.InputPath)?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            _mainViewModel.ShowToast("入力BMS/BMSONファイルを先に読み込んでください", "⚠", isError: true);
            _mainViewModel.StatusMessage = "入力ファイルが指定されていません";
            return string.Empty;
        }

        if (!_fileSystemService.FileExists(inputPath))
        {
            _mainViewModel.ShowToast($"入力ファイルが見つかりません: {Path.GetFileName(inputPath)}", "⚠", isError: true);
            _mainViewModel.StatusMessage = "入力ファイルが存在しません";
            _mainViewModel.BmsDefinitionManager.FileListItems.Clear();
            return string.Empty;
        }

        return inputPath;
    }

    private List<string>? GetFileListToOptimize()
    {
        if (_mainViewModel.BmsDefinitionManager.BmsFileList == null)
        {
            _mainViewModel.ShowToast("BMS/BMSONファイルをまだ読み込んでいません。入力ファイルを選択してください", "⚠", isError: true);
            _mainViewModel.StatusMessage = "ファイルリストが未読み込み";
            return null;
        }

        var fileListItems = _mainViewModel.BmsDefinitionManager.BmsFileList.GetFileList();
        if (fileListItems == null || fileListItems.Count == 0)
        {
            _mainViewModel.ShowToast("ファイルリストが空です。BMS/BMSONファイルに定義が含まれているか確認してください", "⚠", isError: true);
            _mainViewModel.StatusMessage = "ファイルリストが空";
            return null;
        }

        var files = new List<string>();
        foreach (var wavFile in fileListItems)
        {
            if (!string.IsNullOrEmpty(wavFile.Name))
            {
                files.Add(wavFile.Name);
            }
        }

        if (files.Count == 0)
        {
            _mainViewModel.ShowToast("有効なファイルパスが見つかりません", "⚠", isError: true);
            _mainViewModel.StatusMessage = "有効なファイルパスなし";
            return null;
        }

        return files;
    }

    private void SetBusyState(bool isBusy, string statusMessage)
    {
        _mainViewModel.StatusMessage = statusMessage;
        _mainViewModel.IsBusy = isBusy;
        _mainViewModel.IsGlobalProgressIndeterminate = false;
        _mainViewModel.GlobalProgressValue = 0;
    }

    private void HandleOptimizationResult(OptimizationResult? result)
    {
        if (result != null)
        {
            var execTime = result.ExecutionTime.TotalSeconds;
            var memoryMb = result.MemoryUsedBytes / 1024.0 / 1024.0;

            _mainViewModel.ShowResultCard(
                threshold: $"36進数: {result.Base36Result.Threshold * 100:F0}%\n62進数: {result.Base62Result.Threshold * 100:F0}%",
                summary: $"36進数: {result.Base36Result.Count}/{Core.AppConstants.Definition.MaxNumberBase36}件\n62進数: {result.Base62Result.Count}/{Core.AppConstants.Definition.MaxNumberBase62}件",
                reduction: string.Empty,
                time: $"{execTime:F1}秒",
                margin: $"{memoryMb:F1}MB",
                isOptimization: true);

            var toastMsg = $"最適化完了: Base36={result.Base36Result.Threshold * 100:F0}%, Base62={result.Base62Result.Threshold * 100:F0}%";
            _mainViewModel.ShowToast(toastMsg);
            _mainViewModel.StatusMessage = toastMsg;
        }
        else
        {
            _mainViewModel.ShowToast("最適化に失敗しました", "⚠", isError: true);
            _mainViewModel.StatusMessage = "最適化に失敗しました";
        }
    }

    #endregion
}

