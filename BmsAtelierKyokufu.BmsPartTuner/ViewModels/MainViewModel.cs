using System.Collections.ObjectModel;
using System.Windows;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// メインViewModelの統合コーディネーター。
/// </summary>
public partial class MainViewModel : ObservableObject, IDataErrorInfo, IDisposable
{
    private readonly AudioPreviewService _audioPreviewService;
    private bool _disposed;

    /// <summary>ファイル操作ViewModel。</summary>
    public FileOperationsViewModel FileOperations { get; }

    /// <summary>ファイルリストViewModel。</summary>
    public FileListViewModel FileList { get; }

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
    private bool _isSettingsOpen;

    #region フォワードプロパティ

    public string InputPath
    {
        get => FileOperations.InputPath;
        set => FileOperations.InputPath = value;
    }

    public string OutputPath
    {
        get => FileOperations.OutputPath;
        set => FileOperations.OutputPath = value;
    }

    public string R2Threshold
    {
        get => Optimization.R2Threshold;
        set => Optimization.R2Threshold = value;
    }

    public string DefinitionStart
    {
        get => Optimization.DefinitionStart;
        set => Optimization.DefinitionStart = value;
    }

    public string DefinitionEnd
    {
        get => Optimization.DefinitionEnd;
        set => Optimization.DefinitionEnd = value;
    }

    public string StatusMessage
    {
        get => Optimization.StatusMessage;
        set => Optimization.StatusMessage = value;
    }

    public int ProgressValue
    {
        get => Optimization.ProgressValue;
        set => Optimization.ProgressValue = value;
    }

    public bool IsProgressIndeterminate
    {
        get => Optimization.IsProgressIndeterminate;
        set => Optimization.IsProgressIndeterminate = value;
    }

    public bool IsBusy
    {
        get => Optimization.IsBusy;
        set => Optimization.IsBusy = value;
    }

    public Controls.SlideDirection SlideDirection
    {
        get => Optimization.SlideDirection;
    }

    public string SlideInstruction
    {
        get => Optimization.SlideInstruction;
    }

    public bool IsPhysicalDeletionEnabled
    {
        get => Optimization.IsPhysicalDeletionEnabled;
        set => Optimization.IsPhysicalDeletionEnabled = value;
    }

    public ICommand BrowseInputFileCommand => FileOperations.BrowseInputFileCommand;
    public ICommand BrowseOutputFileCommand => FileOperations.BrowseOutputFileCommand;
    public ICommand ClearFilterCommand => FileList.ClearFilterCommand;

    #endregion

    /// <summary>
    /// MainViewModelを初期化。
    /// </summary>
    public MainViewModel(
        IBmsOptimizationService optimizationService,
        AudioPreviewService audioPreviewService,
        InstrumentNameDetectionService instrumentDetectionService,
        FileListFilterService filterService,
        SettingsService settingsService,
        ThemeService themeService,
        LicenseLoaderService licenseLoaderService)
    {
        _audioPreviewService = audioPreviewService ?? throw new ArgumentNullException(nameof(audioPreviewService));

        FileOperations = new FileOperationsViewModel();
        FileList = new FileListViewModel(audioPreviewService, instrumentDetectionService);
        FileList.SetFilterService(filterService);
        Optimization = new OptimizationViewModel(optimizationService);
        Notification = new NotificationViewModel();
        Settings = new SettingsViewModel(settingsService, themeService, licenseLoaderService);
        MediaPlayback = new MediaPlaybackViewModel();
        InputValidation = new InputValidationViewModel();

        // イベントハンドラー登録
        FileOperations.InputPathChanged += OnInputPathChanged;
        FileOperations.AutoOutputPathRequested += OnAutoOutputPathRequested;
        FileList.FileListLoaded += OnFileListLoaded;
        FileList.AudioPlaybackStateChanged += OnAudioPlaybackStateChanged;
        Optimization.DefinitionReductionCompleted += OnDefinitionReductionCompleted;
        Optimization.ErrorOccurred += OnOptimizationError;

        // 検証エラーハンドラー
        InputValidation.ValidationErrorOccurred += (s, e) =>
        {
            ShowMessage($"{e.PropertyName}: {e.ErrorMessage}", isError: true);
        };

        // メディア再生エラーハンドラー
        MediaPlayback.PlaybackError += (s, message) =>
        {
            ShowMessage(message, isError: true);
        };

        FileOperations.PropertyChanged += (s, e) =>
        {
            ForwardPropertyChanged(e.PropertyName);

            if (e.PropertyName == nameof(FileOperations.InputPath))
            {
                Notification.HideResultCard();
                ProgressValue = 0;
                StatusMessage = "準備完了";
            }

            if (e.PropertyName == nameof(FileOperations.InputPath) ||
                e.PropertyName == nameof(FileOperations.OutputPath))
            {
                if (IsSlideConfirmationVisible)
                {
                    HideSlideConfirmation();
                }
            }
        };

        FileList.PropertyChanged += (s, e) => ForwardPropertyChanged(e.PropertyName);
        Optimization.PropertyChanged += (s, e) =>
        {
            ForwardPropertyChanged(e.PropertyName);
            if (e.PropertyName == nameof(Optimization.IsPhysicalDeletionEnabled))
            {
                OnPropertyChanged(nameof(IsPhysicalDeletionEnabled));
                OnPropertyChanged(nameof(SlideDirection));
                OnPropertyChanged(nameof(SlideInstruction));
            }
        };
        Notification.PropertyChanged += (s, e) => ForwardPropertyChanged(e.PropertyName);

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
        if (string.IsNullOrWhiteSpace(playerPath) || !File.Exists(playerPath))
        {
            ShowMessage("外部プレイヤーが設定されていないか、ファイルが見つかりません。設定画面でmBMplay.exeのパスを指定してください。", isError: true);
            return;
        }

        // 処理後の出力ファイルが存在する場合は優先して再生
        var outputFile = OutputPath?.Trim('"');
        if (!string.IsNullOrWhiteSpace(outputFile) && File.Exists(outputFile))
        {
            MediaPlayback.LaunchPlayer(playerPath, outputFile, "処理後");
            return;
        }

        // 入力ファイルが存在する場合は再生
        var inputFile = InputPath?.Trim('"');
        if (!string.IsNullOrWhiteSpace(inputFile) && File.Exists(inputFile))
        {
            MediaPlayback.LaunchPlayer(playerPath, inputFile, "処理前");
            return;
        }

        // どちらもない場合
        ShowMessage("再生対象のBMSファイルがありません。まずBMSファイルを読み込んでください。", isError: true);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteThresholdOptimization))]
    private async Task ExecuteThresholdOptimizationAsync()
    {
        var inputPath = InputPath?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            ShowMessage("入力BMSファイルを先に読み込んでください", isError: true);
            StatusMessage = "入力ファイルが指定されていません";
            return;
        }

        if (!File.Exists(inputPath))
        {
            ShowMessage($"入力ファイルが見つかりません: {Path.GetFileName(inputPath)}", isError: true);
            StatusMessage = "入力ファイルが存在しません";
            FileList.FileListItems.Clear();
            return;
        }

        if (FileList.BmsFileList == null)
        {
            ShowMessage("BMSファイルをまだ読み込んでいません。入力ファイルを選択してください", isError: true);
            StatusMessage = "ファイルリストが未読み込み";
            return;
        }

        var fileListItems = FileList.BmsFileList.GetFileList();
        if (fileListItems == null || fileListItems.Count == 0)
        {
            ShowMessage("ファイルリストが空です。BMSファイルに#WAV定義が含まれているか確認してください", isError: true);
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
            RadixConvert.ZZToInt(DefinitionStart),
            RadixConvert.ZZToInt(DefinitionEnd));

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

        if (FileOperations.CheckOverwriteRequired() || IsPhysicalDeletionEnabled)
        {
            SlideConfirmationRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        var selectedKeywords = FileList.GetSelectedKeywords();

        await Optimization.ExecuteDefinitionReductionAsync(
            FileList.BmsFileList,
            InputPath,
            OutputPath,
            selectedKeywords);

        // 処理完了後、出力先のファイルでリストを再読み込み
        // InputPathとOutputPathが同じ場合（上書き保存）でも、
        // ファイルの内容が変更されているため再読み込みが必要
        if (string.Equals(InputPath, OutputPath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(InputPath))
            {
                FileList.LoadBmsFile(InputPath);
            }
        }
        else
        {
            // 別名保存の場合は入力パスを切り替えて読み込み
            InputPath = OutputPath;
        }
    }

    private bool CanExecuteReduction() => !Optimization.IsBusy;

    private void OnInputPathChanged(object? sender, string path)
    {
        if (File.Exists(path))
        {
            FileList.LoadBmsFile(path);
        }
        else if (!string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = $"対応形式: {GetSupportedExtensionsPattern()}";
            FileList.FileListItems.Clear();
        }
    }

    private void OnAutoOutputPathRequested(object? sender, string autoPath)
    {
        OutputPath = autoPath;
    }

    private void OnFileListLoaded(object? sender, FileListViewModel.FileListLoadedEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (e.IsSuccess)
            {
                var fileTypeName = FileOperations.GetFileTypeName(e.FilePath);
                StatusMessage = $"読み込み完了: {Path.GetFileName(e.FilePath)} ({fileTypeName})";
                ShowMessage($"読み込み完了: {Path.GetFileName(e.FilePath)}");
            }
            else
            {
                ShowMessage($"読み込みエラー: {e.ErrorMessage}", isError: true);
                StatusMessage = "読み込みエラー";
            }
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void OnAudioPlaybackStateChanged(object? sender, AudioPreviewService.PlaybackStateChangedEventArgs e)
    {
        if (e.IsLoading)
        {
            StatusMessage = "音声読み込み中...";
        }
        else if (e.IsPlaying && e.FileName != null)
        {
            StatusMessage = $"再生: {e.FileName}";
        }
    }

    private void OnDefinitionReductionCompleted(object? sender, OptimizationViewModel.ReductionResultEventArgs e)
    {
        if (e.Result != null)
        {
            dynamic result = e.Result;

            int displayThreshold = (int)Math.Round(e.Threshold * 100);

            Notification.ShowResultCard(
                thresholdValues: $"{displayThreshold}%",
                resultFileCounts: $"{result.OriginalCount}件 → {result.OptimizedCount}件",
                additionalInfo: $"削減率: {result.ReductionRate * 100:F1}%",
                processingTime: $"{result.ProcessingTime.TotalSeconds:F1}秒",
                memoryInfo: "-",
                isOptimization: false);

            ShowMessage($"処理完了: {Path.GetFileName(e.OutputPath)}");
        }
    }

    private void OnOptimizationError(object? sender, string errorMessage)
    {
        ShowMessage(errorMessage, isError: true);
    }

    private bool ValidateInputs()
    {
        return InputValidation.ValidateAll(InputPath, OutputPath);
    }

    private string GetSupportedExtensionsPattern()
    {
        return string.Join(", ", Core.AppConstants.Files.SupportedBmsExtensions);
    }

    private void ShowMessage(string message, bool isError = false)
    {
        StatusMessage = message;

        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            Notification.ShowToast(message, isError ? "⚠" : "✓", isError);
        }), System.Windows.Threading.DispatcherPriority.Normal);
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
        var selectedKeywords = FileList.GetSelectedKeywords();

        await Optimization.ExecuteDefinitionReductionAsync(
            FileList.BmsFileList,
            InputPath,
            OutputPath,
            selectedKeywords);
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
                nameof(R2Threshold) or nameof(DefinitionStart) or nameof(DefinitionEnd)
                    => Optimization[columnName],
                nameof(InputPath) => ValidateInputPathError(),
                _ => string.Empty
            };
        }
    }

    private string ValidateInputPathError()
    {
        var inputPath = InputPath?.Trim('"') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return string.Empty;
        }

        if (!File.Exists(inputPath))
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

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            (FileList as IDisposable)?.Dispose();
            (Notification as IDisposable)?.Dispose();
            _audioPreviewService?.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region XAMLバインディング用プロパティ（子ViewModel委譲）

    public string ToastMessage
    {
        get => Notification.ToastMessage;
        set => Notification.ToastMessage = value;
    }

    public string ToastIcon
    {
        get => Notification.ToastIcon;
        set => Notification.ToastIcon = value;
    }

    public bool IsToastVisible
    {
        get => Notification.IsToastVisible;
        set => Notification.IsToastVisible = value;
    }

    public bool IsToastError
    {
        get => Notification.IsToastError;
        set => Notification.IsToastError = value;
    }

    public bool IsResultCardVisible
    {
        get => Notification.IsResultCardVisible;
        set => Notification.IsResultCardVisible = value;
    }

    public string ResultThreshold
    {
        get => Notification.ResultThreshold;
        set => Notification.ResultThreshold = value;
    }

    public string ResultSummary
    {
        get => Notification.ResultSummary;
        set => Notification.ResultSummary = value;
    }

    public string ResultReduction
    {
        get => Notification.ResultReduction;
        set => Notification.ResultReduction = value;
    }

    public string ResultTime
    {
        get => Notification.ResultTime;
        set => Notification.ResultTime = value;
    }

    public string ResultMargin
    {
        get => Notification.ResultMargin;
        set => Notification.ResultMargin = value;
    }

    public string ResultIcon
    {
        get => Notification.ResultIcon;
        set => Notification.ResultIcon = value;
    }

    public bool IsResultOptimization
    {
        get => Notification.IsResultOptimization;
        set => Notification.IsResultOptimization = value;
    }

    public bool IsSlideConfirmationVisible
    {
        get => Notification.IsSlideConfirmationVisible;
        set => Notification.IsSlideConfirmationVisible = value;
    }

    public ObservableCollection<Models.FileList.WavFiles> FileListItems
    {
        get => FileList.FileListItems;
        set => FileList.FileListItems = value;
    }

    public Models.FileList.WavFiles? SelectedFile
    {
        get => FileList.SelectedFile;
        set => FileList.SelectedFile = value;
    }

    public string FilterText
    {
        get => FileList.FilterText;
        set => FileList.FilterText = value;
    }

    public Visibility ClearFilterButtonVisibility
    {
        get => FileList.ClearFilterButtonVisibility;
        set => FileList.ClearFilterButtonVisibility = value;
    }

    public ObservableCollection<InstrumentNameDetectionService.InstrumentGroup> InstrumentGroups
    {
        get => FileList.InstrumentGroups;
        set => FileList.InstrumentGroups = value;
    }

    #endregion
}
