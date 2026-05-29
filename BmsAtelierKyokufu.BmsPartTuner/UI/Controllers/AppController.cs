using BmsAtelierKyokufu.BmsPartTuner.UseCases;
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

    public string? WorkingBmsPath { get; private set; }
    public string? WorkingBmsContent { get; private set; }
    public string? LastDownconvertedBmsonPath { get; private set; }
    public bool IsDownconverting { get; private set; }

    public async Task ExecuteThresholdOptimizationAsync()
    {
        var inputPath = (WorkingBmsPath ?? _mainViewModel.FileOperations.InputPath)?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            _mainViewModel.ShowToast("入力BMS/BMSONファイルを先に読み込んでください", "⚠", isError: true);
            _mainViewModel.StatusMessage = "入力ファイルが指定されていません";
            return;
        }

        if (!_fileSystemService.FileExists(inputPath))
        {
            _mainViewModel.ShowToast($"入力ファイルが見つかりません: {Path.GetFileName(inputPath)}", "⚠", isError: true);
            _mainViewModel.StatusMessage = "入力ファイルが存在しません";
            _mainViewModel.BmsDefinitionManager.FileListItems.Clear();
            return;
        }

        if (_mainViewModel.BmsDefinitionManager.BmsFileList == null)
        {
            _mainViewModel.ShowToast("BMS/BMSONファイルをまだ読み込んでいません。入力ファイルを選択してください", "⚠", isError: true);
            _mainViewModel.StatusMessage = "ファイルリストが未読み込み";
            return;
        }

        var fileListItems = _mainViewModel.BmsDefinitionManager.BmsFileList.GetFileList();
        if (fileListItems == null || fileListItems.Count == 0)
        {
            _mainViewModel.ShowToast("ファイルリストが空です。BMS/BMSONファイルに定義が含まれているか確認してください", "⚠", isError: true);
            _mainViewModel.StatusMessage = "ファイルリストが空";
            return;
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
            return;
        }

        _mainViewModel.StatusMessage = "しきい値最適化シミュレーション開始...";

        var result = await _mainViewModel.Optimization.ExecuteThresholdOptimizationAsync(
            files,
            RadixConvert.ZZToInt(_mainViewModel.Optimization.DefinitionStart),
            RadixConvert.ZZToInt(_mainViewModel.Optimization.DefinitionEnd));

        if (result != null)
        {
            var execTime = result.ExecutionTime.TotalSeconds;
            var memoryMb = result.MemoryUsedBytes / 1024.0 / 1024.0;

            _mainViewModel.ShowResultCard(
                threshold: $"36進数: {result.Base36Result.Threshold * 100:F0}%\n62進数: {result.Base62Result.Threshold * 100:F0}%",
                summary: $"36進数: {result.Base36Result.Count}件\n62進数: {result.Base62Result.Count}件",
                reduction: $"計測点: {result.SimulationData.Count}回",
                time: $"{execTime:F1}秒",
                margin: $"{memoryMb:F1}MB",
                isOptimization: true);

            _mainViewModel.ShowToast($"最適化完了: Base36={result.Base36Result.Threshold * 100:F0}%, Base62={result.Base62Result.Threshold * 100:F0}%");
            _mainViewModel.StatusMessage = $"最適化完了: Base36={result.Base36Result.Threshold * 100:F0}%, Base62={result.Base62Result.Threshold * 100:F0}%";
        }
        else
        {
            _mainViewModel.ShowToast("最適化に失敗しました", "⚠", isError: true);
            _mainViewModel.StatusMessage = "最適化に失敗しました";
        }
    }

    public async Task ExecuteReductionAsync()
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

        await ExecuteDefinitionReductionInternalAsync(inputToUse);
    }

    public async Task ExecuteDefinitionReductionAfterConfirmationAsync()
    {
        var inputToUse = WorkingBmsPath ?? _mainViewModel.FileOperations.InputPath;
        await ExecuteDefinitionReductionInternalAsync(inputToUse);
    }

    private async Task ExecuteDefinitionReductionInternalAsync(string? inputToUse)
    {
        var selectedKeywords = _mainViewModel.BmsDefinitionManager.GetSelectedKeywords();

        await _mainViewModel.Optimization.ExecuteDefinitionReductionAsync(
            _mainViewModel.BmsDefinitionManager.BmsFileList,
            inputToUse,
            _mainViewModel.FileOperations.OutputPath,
            WorkingBmsContent,
            selectedKeywords);

        // 処理完了後、出力先のファイルでリストを再読み込み
        if (string.Equals(inputToUse, _mainViewModel.FileOperations.OutputPath, StringComparison.OrdinalIgnoreCase))
        {
            if (inputToUse != null && _fileSystemService.FileExists(inputToUse))
            {
                _mainViewModel.BmsDefinitionManager.LoadBmsFile(inputToUse);
            }
        }
        else
        {
            // 別名保存の場合は入力パスを切り替えて読み込み
            _mainViewModel.FileOperations.InputPath = _mainViewModel.FileOperations.OutputPath;
        }
    }

    public void HandleInputPathChanged(string? path)
    {
        if (path != null && _fileSystemService.FileExists(path))
        {
            var extension = Path.GetExtension(path);
            if (string.Equals(extension, ".bmson", StringComparison.OrdinalIgnoreCase))
            {
                // すでにダウンコンバート済みの同じファイルなら再変換をスキップ
                if (string.Equals(path, LastDownconvertedBmsonPath, StringComparison.OrdinalIgnoreCase) && WorkingBmsContent != null)
                {
                    _mainViewModel.BmsDefinitionManager.LoadBmsFile(path, WorkingBmsContent);
                    return;
                }

                if (IsDownconverting) return;

                _ = DownconvertBmsonAsync(path);
            }
            else
            {
                Core.Audio.VirtualAudioRegistry.Clear();
                Core.Audio.PointerAudioRegistry.Clear();

                WorkingBmsPath = path;
                WorkingBmsContent = null;
                LastDownconvertedBmsonPath = null; // 別のファイルが来たらクリア
                _mainViewModel.BmsDefinitionManager.LoadBmsFile(path);
            }
        }
        else
        {
            Core.Audio.VirtualAudioRegistry.Clear();
            Core.Audio.PointerAudioRegistry.Clear();

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

    private async Task DownconvertBmsonAsync(string path)
    {
        IsDownconverting = true;
        _mainViewModel.IsBusy = true;
        _mainViewModel.StatusMessage = "bmsonをダウンコンバート中...";
        try
        {
            using (PerformanceDebugLogger.MeasureTime("MainViewModel", "Total Flow (Downconvert + LoadBmsFile)"))
            {
                Core.Audio.VirtualAudioRegistry.Clear();
                Core.Audio.PointerAudioRegistry.Clear();
                string bmsText = await _bmsonConversionService.GenerateBmsTextAsync(path, keyNotesOnly: false);

                WorkingBmsPath = path;
                WorkingBmsContent = bmsText;
                LastDownconvertedBmsonPath = path; // 成功時にパスを記憶

                _mainViewModel.BmsDefinitionManager.LoadBmsFile(path, bmsText);
            }
            _mainViewModel.ShowToast($"bmsonをダウンコンバートしました: {Path.GetFileName(path)}", "📁", false);
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
}
