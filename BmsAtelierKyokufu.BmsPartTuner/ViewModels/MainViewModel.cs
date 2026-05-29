using BmsAtelierKyokufu.BmsPartTuner.Services.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Services.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Services.Common;
using BmsAtelierKyokufu.BmsPartTuner.Services.UI;
using BmsAtelierKyokufu.BmsPartTuner.Core.Messages;
using CommunityToolkit.Mvvm.Messaging;

namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// 各種ViewModelを統括し、アプリケーション全体のUI状態とビジネスロジックを連携させるメインコーディネーター。
/// </summary>
public partial class MainViewModel : ObservableObject, IDataErrorInfo, IDisposable,
    IRecipient<InputPathChangedMessage>,
    IRecipient<AutoOutputPathRequestedMessage>,
    IRecipient<FileListLoadedMessage>,
    IRecipient<AudioPlaybackStateChangedMessage>,
    IRecipient<DefinitionReductionCompletedMessage>,
    IRecipient<OptimizationErrorMessage>,
    IRecipient<ValidationErrorMessage>,
    IRecipient<MediaPlaybackErrorMessage>
{
    private readonly AudioPreviewService _audioPreviewService;
    private readonly IBmsonConversionService _bmsonConversionService;
    private readonly IFileSystemService _fileSystemService;
    private bool _disposed;
    private string? _workingBmsPath;
    private string? _workingBmsContent;
    private string? _lastDownconvertedBmsonPath;

    /// <summary>ファイル操作ViewModel。</summary>
    public FileOperationsViewModel FileOperations { get; }

    /// <summary>ファイルリストViewModel。</summary>
    public FileListViewModel BmsDefinitionManager { get; }

    /// <summary>最適化ViewModel。</summary>
    public OptimizationViewModel Optimization { get; }

    /// <summary>通知ViewModel。</summary>
    public NotificationViewModel Notification { get; }

    /// <summary>設定ViewModel。</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>メディア再生ViewModel。</summary>
    public MediaPlaybackViewModel MediaPlayback { get; }

    /// <summary>入力検証ViewModel。</summary>
    public InputValidationViewModel InputValidation { get; }

    /// <summary>スライド確認要求イベント。</summary>
    public event EventHandler? SlideConfirmationRequested;

    /// <summary>
    /// 設定パネルが開いているかどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    /// <summary>
    /// アプリケーション全体のステータスメッセージ。
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "準備完了";

    /// <summary>
    /// 処理中かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    #region フォワードプロパティ

    // XAML側で直接子ViewModelのプロパティをバインドするように修正済み

    #endregion

    /// <summary>
    /// MainViewModelを初期化。
    /// </summary>
    public MainViewModel(
        IBmsOptimizationService optimizationService,
        BmsAtelierKyokufu.BmsPartTuner.Services.UseCases.IBmsOptimizationUseCase optimizationUseCase,
        IBmsonConversionService bmsonConversionService,
        IFileSystemService fileSystemService,
        FileListViewModel fileListViewModel,
        AudioPreviewService audioPreviewService,
        FileListFilterService filterService,
        SettingsService settingsService,
        ThemeService themeService,
        LicenseLoaderService licenseLoaderService)
    {
        _bmsonConversionService = bmsonConversionService ?? throw new ArgumentNullException(nameof(bmsonConversionService));
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _audioPreviewService = audioPreviewService ?? throw new ArgumentNullException(nameof(audioPreviewService));

        FileOperations = new FileOperationsViewModel();
        BmsDefinitionManager = fileListViewModel ?? throw new ArgumentNullException(nameof(fileListViewModel));
        BmsDefinitionManager.SetFilterService(filterService);
        Optimization = new OptimizationViewModel(optimizationService, optimizationUseCase);
        Notification = new NotificationViewModel();
        Settings = new SettingsViewModel(settingsService, themeService, licenseLoaderService);
        MediaPlayback = new MediaPlaybackViewModel();
        InputValidation = new InputValidationViewModel();
        // イベントハンドラーの代わりにMessengerを使用
        WeakReferenceMessenger.Default.RegisterAll(this);

        FileOperations.PropertyChanged += OnFileOperationsPropertyChanged;
        BmsDefinitionManager.PropertyChanged += OnBmsDefinitionManagerPropertyChanged;
        Optimization.PropertyChanged += OnOptimizationPropertyChanged;
        Notification.PropertyChanged += OnNotificationPropertyChanged;

        // 起動時にテーマを適用
        Settings.ApplyInitialTheme();
    }

    /// <summary>
    /// 設定画面を開くコマンド。
    /// </summary>
    [RelayCommand]
    private void OpenSettings()
    {
        IsSettingsOpen = true;
    }

    /// <summary>
    /// 設定画面を閉じるコマンド。
    /// </summary>
    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    /// <summary>
    /// 外部プレイヤーでテスト再生を実行するコマンド。
    /// 常に有効。条件に応じてトーストでエラーを表示。
    /// </summary>
    [RelayCommand]
    private void TestPlay()
    {
        var playerPath = Settings.MbmPlayPath;

        // プレイヤーパスが設定されていない、または存在しない場合はトースト
        if (string.IsNullOrWhiteSpace(playerPath) || !_fileSystemService.FileExists(playerPath))
        {
            ShowMessage("外部プレイヤーが設定されていないか、ファイルが見つかりません。設定画面でmBMplay.exeのパスを指定してください。", isError: true);
            return;
        }

        MediaPlayback.SetPlayerPath(playerPath);

        // 処理後の出力ファイルが存在する場合は優先して再生
        var outputFile = FileOperations.OutputPath?.Trim('"');
        if (!string.IsNullOrWhiteSpace(outputFile) && _fileSystemService.FileExists(outputFile))
        {
            MediaPlayback.LaunchPlayer(playerPath, outputFile, "処理後");
            return;
        }

        // 入力ファイルが存在する場合は再生
        var inputFile = FileOperations.InputPath?.Trim('"');
        if (!string.IsNullOrWhiteSpace(inputFile) && _fileSystemService.FileExists(inputFile))
        {
            MediaPlayback.LaunchPlayer(playerPath, inputFile, "処理前");
            return;
        }

        // どちらもない場合
        ShowMessage("再生対象のBMS/BMSONファイルがありません。まずBMS/BMSONファイルを読み込んでください。", isError: true);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteThresholdOptimization))]
    private async Task ExecuteThresholdOptimizationAsync()
    {
        var inputPath = (_workingBmsPath ?? FileOperations.InputPath)?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            ShowMessage("入力BMS/BMSONファイルを先に読み込んでください", isError: true);
            StatusMessage = "入力ファイルが指定されていません";
            return;
        }

        if (!_fileSystemService.FileExists(inputPath))
        {
            ShowMessage($"入力ファイルが見つかりません: {Path.GetFileName(inputPath)}", isError: true);
            StatusMessage = "入力ファイルが存在しません";
            BmsDefinitionManager.FileListItems.Clear();
            return;
        }

        if (BmsDefinitionManager.BmsFileList == null)
        {
            ShowMessage("BMS/BMSONファイルをまだ読み込んでいません。入力ファイルを選択してください", isError: true);
            StatusMessage = "ファイルリストが未読み込み";
            return;
        }

        var fileListItems = BmsDefinitionManager.BmsFileList.GetFileList();
        if (fileListItems == null || fileListItems.Count == 0)
        {
            ShowMessage("ファイルリストが空です。BMS/BMSONファイルに定義が含まれているか確認してください", isError: true);
            StatusMessage = "ファイルリストが空";
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
            ShowMessage("有効なファイルパスが見つかりません", isError: true);
            StatusMessage = "有効なファイルパスなし";
            return;
        }

        StatusMessage = "しきい値最適化シミュレーション開始...";

        var result = await Optimization.ExecuteThresholdOptimizationAsync(
            files,
            Core.Helpers.RadixConvert.ZZToInt(Optimization.DefinitionStart),
            Core.Helpers.RadixConvert.ZZToInt(Optimization.DefinitionEnd));

        if (result != null)
        {
            var execTime = result.ExecutionTime.TotalSeconds;
            var memoryMb = result.MemoryUsedBytes / 1024.0 / 1024.0;

            Notification.ShowResultCard(
                thresholdValues: $"36進数: {result.Base36Result.Threshold * 100:F0}%\n62進数: {result.Base62Result.Threshold * 100:F0}%",
                resultFileCounts: $"36進数: {result.Base36Result.Count}件\n62進数: {result.Base62Result.Count}件",
                additionalInfo: $"計測点: {result.SimulationData.Count}回",
                processingTime: $"{execTime:F1}秒",
                memoryInfo: $"{memoryMb:F1}MB",
                isOptimization: true);

            ShowMessage($"最適化完了: Base36={result.Base36Result.Threshold * 100:F0}%, Base62={result.Base62Result.Threshold * 100:F0}%");
        }
        else
        {
            ShowMessage("最適化に失敗しました", isError: true);
        }
    }

    private bool CanExecuteThresholdOptimization() => !Optimization.IsBusy;

    [RelayCommand(CanExecute = nameof(CanExecuteReduction))]
    private async Task ExecuteReductionAsync()
    {
        if (!ValidateInputs() || Optimization.HasFormLevelError)
        {
            return;
        }

        var inputToUse = _workingBmsPath ?? FileOperations.InputPath;

        // BMSONファイルがそのまま渡されており、かつダウンコンバート済みコンテンツがない場合はブロックする
        if (Path.GetExtension(inputToUse).Equals(".bmson", StringComparison.OrdinalIgnoreCase) && _workingBmsContent == null)
        {
            ShowMessage("BMSONファイルは直接削減できません。自動ダウンコンバートされたBMSファイルがセットされるまでお待ち下さい。", true);
            return;
        }

        if (FileOperations.CheckOverwriteRequired() || Optimization.IsPhysicalDeletionEnabled)
        {
            SlideConfirmationRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        var selectedKeywords = BmsDefinitionManager.GetSelectedKeywords();

        await Optimization.ExecuteDefinitionReductionAsync(
            BmsDefinitionManager.BmsFileList,
            inputToUse,
            FileOperations.OutputPath,
            _workingBmsContent,
            selectedKeywords);

        // 処理完了後、出力先のファイルでリストを再読み込み
        // InputPathとOutputPathが同じ場合（上書き保存）でも、
        // ファイルの内容が変更されているため再読み込みが必要
        if (string.Equals(inputToUse, FileOperations.OutputPath, StringComparison.OrdinalIgnoreCase))
        {
            if (_fileSystemService.FileExists(inputToUse))
            {
                BmsDefinitionManager.LoadBmsFile(inputToUse);
            }
        }
        else
        {
            // 別名保存の場合は入力パスを切り替えて読み込み
            FileOperations.InputPath = FileOperations.OutputPath;
        }
    }

    private bool _isDownconverting;

    private bool CanExecuteReduction() => !Optimization.IsBusy && !_isDownconverting;

    public void Receive(InputPathChangedMessage message)
    {
        var path = message.Path;
        if (_fileSystemService.FileExists(path))
        {
            var extension = Path.GetExtension(path);
            if (string.Equals(extension, ".bmson", StringComparison.OrdinalIgnoreCase))
            {
                // すでにダウンコンバート済みの同じファイルなら再変換をスキップ
                if (string.Equals(path, _lastDownconvertedBmsonPath, StringComparison.OrdinalIgnoreCase) && _workingBmsContent != null)
                {
                    BmsDefinitionManager.LoadBmsFile(path, _workingBmsContent);
                    return;
                }

                if (_isDownconverting) return;

                _ = DownconvertBmsonAsync(path);
            }
            else
            {
                Core.Audio.VirtualAudioRegistry.Clear();
                Core.Audio.PointerAudioRegistry.Clear();

                _workingBmsPath = path;
                _workingBmsContent = null;
                _lastDownconvertedBmsonPath = null; // 別のファイルが来たらクリア
                BmsDefinitionManager.LoadBmsFile(path);
            }
        }
        else
        {
            Core.Audio.VirtualAudioRegistry.Clear();
            Core.Audio.PointerAudioRegistry.Clear();

            _workingBmsPath = null;
            _workingBmsContent = null;
            _lastDownconvertedBmsonPath = null; // ファイルが存在しない場合もクリア
            if (!string.IsNullOrWhiteSpace(path))
            {
                StatusMessage = $"対応形式: {GetSupportedExtensionsPattern()}";
                BmsDefinitionManager.FileListItems.Clear();
            }
        }
    }

    private async Task DownconvertBmsonAsync(string path)
    {
        _isDownconverting = true;
        IsBusy = true;
        StatusMessage = "bmsonをダウンコンバート中...";
        try
        {
            using (PerformanceDebugLogger.MeasureTime("MainViewModel", "Total Flow (Downconvert + LoadBmsFile)"))
            {
                Core.Audio.VirtualAudioRegistry.Clear();
                Core.Audio.PointerAudioRegistry.Clear();
                string bmsText = await _bmsonConversionService.GenerateBmsTextAsync(path, keyNotesOnly: false);

                _workingBmsPath = path;
                _workingBmsContent = bmsText;
                _lastDownconvertedBmsonPath = path; // 成功時にパスを記憶

                BmsDefinitionManager.LoadBmsFile(path, bmsText);
            }
            ShowToast($"bmsonをダウンコンバートしました: {Path.GetFileName(path)}", "📁", false);
        }
        catch (Exception ex)
        {
            string errorMessage = ex is AggregateException aggEx && aggEx.InnerExceptions.Count > 0
                ? aggEx.InnerExceptions[0].Message
                : ex.Message;

            ShowToast($"bmson変換失敗: {errorMessage}", "⚠", true);
            BmsDefinitionManager.FileListItems.Clear();
            _lastDownconvertedBmsonPath = null; // 失敗時はクリア
            _workingBmsContent = null;
        }
        finally
        {
            _isDownconverting = false;
            IsBusy = false;
            StatusMessage = "準備完了";
            OnPropertyChanged(nameof(CanExecuteReduction));
        }
    }

    public void Receive(AutoOutputPathRequestedMessage message)
    {
        FileOperations.OutputPath = message.OutputPath;
    }

    public void Receive(FileListLoadedMessage message)
    {
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (message.IsSuccess)
            {
                var fileTypeName = FileOperationsViewModel.GetFileTypeName(message.FilePath);
                StatusMessage = $"読み込み完了: {Path.GetFileName(message.FilePath)} ({fileTypeName})";
                ShowMessage($"読み込み完了: {Path.GetFileName(message.FilePath)}");
            }
            else
            {
                ShowMessage($"読み込みエラー: {message.ErrorMessage}", isError: true);
                StatusMessage = "読み込みエラー";
            }
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    public void Receive(AudioPlaybackStateChangedMessage message)
    {
        if (message.IsLoading)
        {
            StatusMessage = "音声読み込み中...";
        }
        else if (message.IsPlaying && message.FileName != null)
        {
            StatusMessage = $"再生: {message.FileName}";
        }
    }

    public void Receive(DefinitionReductionCompletedMessage message)
    {
        if (message.Result != null)
        {
            dynamic result = message.Result;

            int displayThreshold = (int)Math.Round(message.Threshold * 100);

            Notification.ShowResultCard(
                thresholdValues: $"{displayThreshold}%",
                resultFileCounts: $"{result.OriginalCount}件 → {result.OptimizedCount}件",
                additionalInfo: $"削減率: {result.ReductionRate * 100:F1}%",
                processingTime: $"{result.ProcessingTime.TotalSeconds:F1}秒",
                memoryInfo: "-",
                isOptimization: false);

            ShowMessage($"処理完了: {Path.GetFileName(message.OutputPath)}");
        }
    }

    public void Receive(OptimizationErrorMessage message)
    {
        ShowMessage(message.ErrorMessage, isError: true);
    }

    public void Receive(ValidationErrorMessage message)
    {
        ShowMessage($"{message.PropertyName}: {message.ErrorMessage}", isError: true);
    }

    public void Receive(MediaPlaybackErrorMessage message)
    {
        ShowMessage(message.Message, isError: true);
    }

    private void OnFileOperationsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ForwardPropertyChanged(e.PropertyName);

        if (e.PropertyName == nameof(FileOperations.InputPath))
        {
            Notification.HideResultCard();
            Optimization.ProgressValue = 0;
            StatusMessage = "準備完了";
        }

        if (e.PropertyName == nameof(FileOperations.InputPath) ||
            e.PropertyName == nameof(FileOperations.OutputPath))
        {
            if (Notification.IsSlideConfirmationVisible)
            {
                HideSlideConfirmation();
            }
        }
    }

    private void OnBmsDefinitionManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ForwardPropertyChanged(e.PropertyName);
    }

    private void OnOptimizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ForwardPropertyChanged(e.PropertyName);
        if (e.PropertyName == nameof(Optimization.IsPhysicalDeletionEnabled))
        {
            // プロパティ変更通知は不要になったため削除
        }
    }

    private void OnNotificationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ForwardPropertyChanged(e.PropertyName);
    }

    private bool ValidateInputs()
    {
        var inputToUse = _workingBmsPath ?? FileOperations.InputPath;
        return InputValidation.ValidateAll(inputToUse, FileOperations.OutputPath);
    }

    private static string GetSupportedExtensionsPattern()
    {
        return string.Join(", ", Core.AppConstants.Files.SupportedBmsExtensions);
    }

    private void ShowMessage(string message, bool isError = false)
    {
        StatusMessage = message;

        Application.Current?.Dispatcher.BeginInvoke(new Action(() => Notification.ShowToast(message, isError ? "⚠" : "✓", isError)), System.Windows.Threading.DispatcherPriority.Normal);
    }

    private void ForwardPropertyChanged(string? propertyName)
    {
        if (!string.IsNullOrEmpty(propertyName))
        {
            OnPropertyChanged(propertyName);
        }
    }

    public async Task ExecuteDefinitionReductionAfterConfirmationAsync()
    {
        var selectedKeywords = BmsDefinitionManager.GetSelectedKeywords();
        var inputToUse = _workingBmsPath ?? FileOperations.InputPath;

        await Optimization.ExecuteDefinitionReductionAsync(
            BmsDefinitionManager.BmsFileList,
            inputToUse,
            FileOperations.OutputPath,
            _workingBmsContent,
            selectedKeywords);

        // 処理完了後、出力先のファイルでリストを再読み込み
        if (string.Equals(inputToUse, FileOperations.OutputPath, StringComparison.OrdinalIgnoreCase))
        {
            if (_fileSystemService.FileExists(inputToUse))
            {
                BmsDefinitionManager.LoadBmsFile(inputToUse);
            }
        }
        else
        {
            // 別名保存の場合は入力パスを切り替えて読み込み
            FileOperations.InputPath = FileOperations.OutputPath;
        }
    }

    public void ShowToast(string message, string icon = "✓", bool isError = false)
    {
        Notification.ShowToast(message, icon, isError);
    }

    public void HideToast()
    {
        Notification.HideToast();
    }

    public void ShowResultCard(
        string threshold, string summary, string reduction, string time,
        string margin, bool isOptimization)
    {
        Notification.ShowResultCard(threshold, summary, reduction, time, margin, isOptimization);
    }

    public void HideResultCard()
    {
        Notification.HideResultCard();
    }

    public void ShowSlideConfirmation()
    {
        Notification.ShowSlideConfirmation();
    }

    public void HideSlideConfirmation()
    {
        Notification.HideSlideConfirmation();
    }

    public string Error => Optimization.Error;

    public string this[string columnName]
    {
        get
        {
            return columnName switch
            {
                "R2Threshold" or "DefinitionStart" or "DefinitionEnd"
                    => Optimization[columnName],
                "InputPath" => ValidateInputPathError(),
                "OutputPath" => ValidateOutputPathError(),
                _ => string.Empty
            };
        }
    }

    private string ValidateInputPathError()
    {
        // ダウンコンバート済みのパスが存在する場合はそちらを検証ベースにする
        var inputPath = (_workingBmsPath ?? FileOperations.InputPath)?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return string.Empty;
        }

        if (!_fileSystemService.FileExists(inputPath))
        {
            return "ファイルが見つかりません";
        }

        var extension = Path.GetExtension(inputPath).ToLower();
        if (!Array.Exists(Core.AppConstants.Files.SupportedBmsExtensions, ext => ext == extension))
        {
            return $"サポートされていない形式です ({GetSupportedExtensionsPattern()})";
        }
        return string.Empty;
    }

    private string ValidateOutputPathError()
    {
        var outputPath = FileOperations.OutputPath?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return string.Empty;
        }

        try
        {
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                return $"フォルダが見つかりません: {outputDir}";
            }
        }
        catch (Exception)
        {
            return "パスが無効です";
        }

        var extension = Path.GetExtension(outputPath).ToLower();
        if (!Array.Exists(Core.AppConstants.Files.SupportedOutputBmsExtensions, ext => ext == extension))
        {
            return $"出力ファイルはBMS形式である必要があります ({GetSupportedOutputExtensionsPattern()})";
        }
        return string.Empty;
    }

    private static string GetSupportedOutputExtensionsPattern()
    {
        return string.Join(", ", Core.AppConstants.Files.SupportedOutputBmsExtensions);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // イベントの購読解除
            WeakReferenceMessenger.Default.UnregisterAll(this);

            FileOperations.PropertyChanged -= OnFileOperationsPropertyChanged;
            BmsDefinitionManager.PropertyChanged -= OnBmsDefinitionManagerPropertyChanged;
            Optimization.PropertyChanged -= OnOptimizationPropertyChanged;
            Notification.PropertyChanged -= OnNotificationPropertyChanged;

            (BmsDefinitionManager as IDisposable)?.Dispose();
            (Notification as IDisposable)?.Dispose();
            _audioPreviewService?.Dispose();

            // Clear static registries on disposal to release static caches
            Core.Audio.PointerAudioRegistry.Clear();
            Core.Audio.VirtualAudioRegistry.Clear();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region XAMLバインディング用プロパティ（子ViewModel委譲）

    // 他ViewModelからの単純なプロパティ委譲は排除され、
    // XAML側で直接 Notification.ToastMessage などをバインドする形に変更しました。

    #endregion
}
