using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio;
using BmsAtelierKyokufu.BmsPartTuner.UI.Controllers;
using BmsAtelierKyokufu.BmsPartTuner.UI.Services;
namespace BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;

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
    private readonly AppController _appController;
    private bool _disposed;
    private System.Threading.CancellationTokenSource? _activeCts;

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

    /// <summary>チュートリアルViewModel。</summary>
    public TutorialViewModel Tutorial { get; }

    /// <summary>スライド確認要求イベント。</summary>
    public event EventHandler? SlideConfirmationRequested;

    /// <summary>
    /// 設定パネルが開いているかどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    /// <summary>
    /// チュートリアル画面が表示されているかどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsTutorialVisible { get; set; }

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

    partial void OnIsBusyChanged(bool value)
    {
        UpdateGlobalProgressVisibility();
        NotifyCanExecuteReductionChanged();
        NotifyCanExecuteThresholdOptimizationChanged();
        CancelActiveTaskCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// グローバルなプログレスの進捗率（0〜100）。
    /// </summary>
    [ObservableProperty]
    public partial int GlobalProgressValue { get; set; }

    /// <summary>
    /// グローバルなプログレスが不定状態（インジケーターぐるぐる状態）かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsGlobalProgressIndeterminate { get; set; }

    /// <summary>
    /// グローバルなプログレスバーを表示するかどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsGlobalProgressVisible { get; set; }

    #region フォワードプロパティ

    // XAML側で直接子ViewModelのプロパティをバインドするように修正済み

    #endregion

    /// <summary>
    /// MainViewModelを初期化。
    /// </summary>
    public MainViewModel(
        IBmsOptimizationService optimizationService,
        UseCases.IBmsOptimizationUseCase optimizationUseCase,
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
        var _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _appController = new AppController(this, _bmsonConversionService, _fileSystemService);

        FileOperations = new FileOperationsViewModel();
        BmsDefinitionManager = fileListViewModel ?? throw new ArgumentNullException(nameof(fileListViewModel));
        BmsDefinitionManager.SetFilterService(filterService);
        Optimization = new OptimizationViewModel(optimizationService, optimizationUseCase);
        Notification = new NotificationViewModel();
        Settings = new SettingsViewModel(settingsService, themeService, licenseLoaderService);
        MediaPlayback = new MediaPlaybackViewModel();
        InputValidation = new InputValidationViewModel();
        Tutorial = new TutorialViewModel();

        // チュートリアルの完了イベントを購読
        Tutorial.TutorialCompleted += (s, e) =>
        {
            IsTutorialVisible = false;
            var settings = _settingsService.Load();
            if (!settings.HasSeenTutorial)
            {
                _settingsService.Save(settings with { HasSeenTutorial = true });
            }
        };

        // イベントハンドラーの代わりにMessengerを使用
        WeakReferenceMessenger.Default.RegisterAll(this);

        FileOperations.PropertyChanged += OnFileOperationsPropertyChanged;
        BmsDefinitionManager.PropertyChanged += OnBmsDefinitionManagerPropertyChanged;
        Optimization.PropertyChanged += OnOptimizationPropertyChanged;
        Notification.PropertyChanged += OnNotificationPropertyChanged;

        // 起動時にテーマを適用
        Settings.ApplyInitialTheme();

        // チュートリアル表示判定
        var currentSettings = _settingsService.Load();
        if (!currentSettings.HasSeenTutorial)
        {
            IsTutorialVisible = true;
        }
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
        SetActiveCts(new System.Threading.CancellationTokenSource());
        try
        {
            await _appController.ExecuteThresholdOptimizationAsync(_activeCts!.Token);
        }
        catch (System.OperationCanceledException)
        {
            ShowMessage("最適化処理がキャンセルされました", isError: false);
        }
        catch (System.Exception ex)
        {
            ShowMessage($"エラー: {ex.Message}", isError: true);
        }
        finally
        {
            var cts = _activeCts;
            SetActiveCts(null);
            cts?.Dispose();
        }
    }

    private bool CanExecuteThresholdOptimization() => !Optimization.IsBusy && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanExecuteReduction))]
    private async Task ExecuteReductionAsync()
    {
        if (!ValidateInputs() || Optimization.HasFormLevelError)
        {
            return;
        }

        SetActiveCts(new System.Threading.CancellationTokenSource());
        try
        {
            await _appController.ExecuteReductionAsync(_activeCts!.Token);
        }
        catch (System.OperationCanceledException)
        {
            ShowMessage("削減処理がキャンセルされました", isError: false);
        }
        catch (System.Exception ex)
        {
            ShowMessage($"エラー: {ex.Message}", isError: true);
        }
        finally
        {
            var cts = _activeCts;
            SetActiveCts(null);
            cts?.Dispose();
        }
    }

    private bool CanExecuteReduction() => !Optimization.IsBusy && !_appController.IsDownconverting && !IsBusy;

    public void SetActiveCts(System.Threading.CancellationTokenSource? cts)
    {
        _activeCts = cts;
        CancelActiveTaskCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCancelActiveTask))]
    private void CancelActiveTask()
    {
        if (_activeCts != null)
        {
            try
            {
                // 即時UIフィードバック：キャンセル中であることを表示
                StatusMessage = "キャンセル中...";
                IsGlobalProgressIndeterminate = false;
                _activeCts.Cancel();
            }
            catch (System.ObjectDisposedException)
            {
            }
        }
    }

    private bool CanCancelActiveTask() => _activeCts != null;

    public void Receive(InputPathChangedMessage message)
    {
        _appController.HandleInputPathChanged(message.Path);
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

            long memoryBytes = 0;
            try { memoryBytes = result.MemoryUsedBytes; } catch { }
            var memoryMb = memoryBytes / 1024.0 / 1024.0;

            int displayThreshold = (int)Math.Round(message.Threshold * 100);

            Notification.ShowResultCard(
                thresholdValues: $"{displayThreshold}%",
                resultFileCounts: $"{result.OriginalCount}件 → {result.OptimizedCount}件",
                additionalInfo: $"削減率: {result.ReductionRate * 100:F1}%",
                processingTime: $"{result.ProcessingTime.TotalSeconds:F1}秒",
                memoryInfo: $"{memoryMb:F1}MB",
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

    private void OnFileOperationsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ForwardPropertyChanged(e.PropertyName);

        if (e.PropertyName == nameof(FileOperations.InputPath))
        {
            Optimization.ProgressValue = 0;
            if (!IsBusy)
            {
                StatusMessage = "準備完了";
            }
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

    private void OnBmsDefinitionManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ForwardPropertyChanged(e.PropertyName);
    }

    private void OnOptimizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ForwardPropertyChanged(e.PropertyName);

        if (e.PropertyName == nameof(Optimization.ProgressValue))
        {
            GlobalProgressValue = Optimization.ProgressValue;
        }
        else if (e.PropertyName == nameof(Optimization.IsProgressIndeterminate))
        {
            IsGlobalProgressIndeterminate = Optimization.IsProgressIndeterminate;
        }
        else if (e.PropertyName == nameof(Optimization.IsBusy))
        {
            UpdateGlobalProgressVisibility();
        }
    }

    private void UpdateGlobalProgressVisibility()
    {
        IsGlobalProgressVisible = IsBusy || Optimization.IsBusy;
    }

    private void OnNotificationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ForwardPropertyChanged(e.PropertyName);
    }

    private bool ValidateInputs()
    {
        var inputToUse = _appController.WorkingBmsPath ?? FileOperations.InputPath;
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

    public void InvokeSlideConfirmationRequested()
    {
        SlideConfirmationRequested?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyCanExecuteReductionChanged()
    {
        OnPropertyChanged(nameof(CanExecuteReduction));
        ExecuteReductionCommand.NotifyCanExecuteChanged();
    }

    public void NotifyCanExecuteThresholdOptimizationChanged()
    {
        OnPropertyChanged(nameof(CanExecuteThresholdOptimization));
        ExecuteThresholdOptimizationCommand.NotifyCanExecuteChanged();
    }

    public async Task ExecuteDefinitionReductionAfterConfirmationAsync()
    {
        SetActiveCts(new System.Threading.CancellationTokenSource());
        try
        {
            await _appController.ExecuteDefinitionReductionAfterConfirmationAsync(_activeCts!.Token);
        }
        catch (System.OperationCanceledException)
        {
            ShowMessage("削減処理がキャンセルされました", isError: false);
        }
        catch (System.Exception ex)
        {
            ShowMessage($"エラー: {ex.Message}", isError: true);
        }
        finally
        {
            var cts = _activeCts;
            SetActiveCts(null);
            cts?.Dispose();
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

    [RelayCommand]
    private void HideResultCardInternal()
    {
        Notification.HideResultCard();
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

    [RelayCommand]
    private void CancelSlideConfirmationInternal()
    {
        HideSlideConfirmation();
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
                "InputPath" => Validators.MainInputValidator.ValidateInputPath(_appController.WorkingBmsPath ?? FileOperations.InputPath, _fileSystemService),
                "OutputPath" => Validators.MainInputValidator.ValidateOutputPath(FileOperations.OutputPath),
                _ => string.Empty
            };
        }
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
            AudioRegistry.Instance.Dispose();
            VirtualAudioRegistry.Clear();
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
