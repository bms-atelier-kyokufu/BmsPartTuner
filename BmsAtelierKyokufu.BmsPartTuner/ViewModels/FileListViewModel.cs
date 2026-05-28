using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Services.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Services.Bms;

namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// 読み込まれたBMSファイルのリスト表示と、音声プレビュー、フィルタリングを管理するViewModel。
/// </summary>
public partial class FileListViewModel : ObservableObject, IDisposable
{
    private readonly AudioPreviewService _audioPreviewService;
    private readonly InstrumentNameDetectionService _instrumentDetectionService;
    private FileListFilterService? _filterService;
    private BmsDefinitionManager? _bmsFileList;
    private bool disposedValue;

    /// <summary>
    /// 表示用のBMS音声ファイルリスト。
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<BmsAudioFile> FileListItems { get; set; } = [];

    /// <summary>
    /// リスト上で選択されている音声ファイル。
    /// 変更時に自動的に音声プレビューが開始されます。
    /// </summary>
    [ObservableProperty]
    public partial BmsAudioFile? SelectedFile { get; set; }

    /// <summary>
    /// ファイル名検索用のフィルタテキスト。
    /// </summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>
    /// フィルタクリアボタンの表示状態。
    /// </summary>
    [ObservableProperty]
    public partial Visibility ClearFilterButtonVisibility { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// ファイル名から自動検出された楽器グループのリスト。
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<InstrumentNameDetectionService.InstrumentGroup> InstrumentGroups { get; set; } = [];

    /// <summary>
    /// UIに表示されるフィルタチップのリスト。
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<FileListFilterService.SelectableFilterChip> FilterChips { get; set; } = [];

    /// <summary>
    /// 読み込まれたBMSファイルの定義マネージャー。
    /// </summary>
    public BmsDefinitionManager? BmsFileList => _bmsFileList;

    /// <summary>
    /// ファイルリストの読み込みが完了した際に発生するイベント。
    /// </summary>
    public event EventHandler<FileListLoadedEventArgs>? FileListLoaded;

    /// <summary>
    /// 音声の再生状態が変化した際に発生するイベント。
    /// </summary>
    public event EventHandler<AudioPreviewService.PlaybackStateChangedEventArgs>? AudioPlaybackStateChanged;

    /// <summary>
    /// 選択中の楽器キーワードが変化した際に発生するイベント。
    /// </summary>
    public event EventHandler<SelectedKeywordsChangedEventArgs>? SelectedKeywordsChanged;

    public FileListViewModel(
        AudioPreviewService audioPreviewService,
        InstrumentNameDetectionService instrumentDetectionService)
    {
        _audioPreviewService = audioPreviewService ?? throw new ArgumentNullException(nameof(audioPreviewService));
        _instrumentDetectionService = instrumentDetectionService ?? throw new ArgumentNullException(nameof(instrumentDetectionService));

        _audioPreviewService.PlaybackStateChanged += OnAudioPlaybackStateChanged;
    }

    partial void OnFilterTextChanged(string value)
    {
        ClearFilterButtonVisibility = string.IsNullOrWhiteSpace(value) ?
            Visibility.Collapsed : Visibility.Visible;
    }

    partial void OnSelectedFileChanged(BmsAudioFile? value)
    {
        if (value != null)
        {
            _ = _audioPreviewService.PreviewAudioAsync(value.Name);
        }
    }

    /// <summary>
    /// ファイルリストに対するフィルタリングサービスを注入します。
    /// </summary>
    public void SetFilterService(FileListFilterService filterService)
    {
        _filterService = filterService;
    }

    /// <summary>
    /// 指定されたパスのBMS/bmsonファイルを読み込み、リストとフィルタを初期化します。
    /// </summary>
    public void LoadBmsFile(string bmsFilePath, string? bmsContent = null)
    {
        PerformanceDebugLogger.WriteDebug(nameof(FileListViewModel), $"=== FileListViewModel.LoadBmsFile Started for {Path.GetFileName(bmsFilePath)} ===");
        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timer = PerformanceDebugLogger.StartTimer();
        try
        {
            _bmsFileList = new BmsDefinitionManager(bmsFilePath, bmsContent);
            var fileList = _bmsFileList.CreateFileList();
            FileListItems = fileList;
            PerformanceDebugLogger.WriteDebug(nameof(FileListViewModel), $"BmsDefinitionManager construction and CreateFileList: {timer.Lap("BmsDefinitionManager construction and CreateFileList")} ms");

            InitializeInstrumentFilters(fileList);
            PerformanceDebugLogger.WriteDebug(nameof(FileListViewModel), $"FilterChips and InstrumentGroups generation: {timer.Lap("FilterChips and InstrumentGroups generation")} ms");

            if (_bmsFileList.MissingFiles.Count == 0 && fileList.Count > 0)
            {
                FileListLoaded?.Invoke(this, new FileListLoadedEventArgs
                {
                    FilePath = bmsFilePath,
                    FileCount = fileList.Count,
                    IsSuccess = true
                });
            }
            PerformanceDebugLogger.WriteDebug(nameof(FileListViewModel), $"=== FileListViewModel.LoadBmsFile Finished: {timerTotal.Lap("Total")} ms ===");
        }
        catch (Exception ex)
        {
            FileListLoaded?.Invoke(this, new FileListLoadedEventArgs
            {
                FilePath = bmsFilePath,
                IsSuccess = false,
                ErrorMessage = ex.Message
            });
        }
    }

    private void InitializeInstrumentFilters(ObservableCollection<BmsAudioFile> fileList)
    {
        var chips = _filterService?.GenerateFilterChips(fileList) ?? [];

        var instrumentGroups = chips
            .Select(static c => new InstrumentNameDetectionService.InstrumentGroup
            {
                Name = c.Keyword,
                Count = c.Count,
                IsSelected = true
            })
            .ToList();

        InstrumentGroups = new ObservableCollection<InstrumentNameDetectionService.InstrumentGroup>(instrumentGroups);
    }

    /// <summary>
    /// 楽器フィルタチップの選択状態を反転させます。
    /// </summary>
    public void ToggleChipSelection(FileListFilterService.SelectableFilterChip chip)
    {
        if (chip == null) return;

        chip.IsSelected = !chip.IsSelected;
        NotifySelectedKeywordsChanged();
    }

    /// <summary>
    /// 現在選択されているフィルタキーワードの配列を取得します。
    /// 最適化対象を絞り込む際に使用されます。
    /// </summary>
    public string[] GetSelectedKeywords()
    {
        return [.. FilterChips
            .Where(static chip => chip.IsSelected)
            .Select(static chip => chip.Keyword)];
    }

    private void NotifySelectedKeywordsChanged()
    {
        var selectedKeywords = GetSelectedKeywords();
        SelectedKeywordsChanged?.Invoke(this, new SelectedKeywordsChangedEventArgs
        {
            SelectedKeywords = selectedKeywords
        });
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
    }

    [RelayCommand]
    private void ToggleInstrumentFilter(object? parameter)
    {
        if (parameter is InstrumentNameDetectionService.InstrumentGroup instrumentGroup)
        {
            instrumentGroup.IsSelected = !instrumentGroup.IsSelected;
            ApplyInstrumentFilter();
        }
    }

    private void ApplyInstrumentFilter()
    {
        if (_filterService == null) return;

        var selectedInstruments = new HashSet<string>(
            InstrumentGroups.Where(static g => g.IsSelected).Select(static g => g.Name),
            StringComparer.OrdinalIgnoreCase);

        _filterService.ApplyInstrumentFilter(selectedInstruments);
    }

    /// <summary>
    /// 選択されたファイルをリスト表示から除外します（最適化対象から外す目的）。
    /// </summary>
    [RelayCommand]
    public void DeleteSelectedFiles(System.Collections.IList? items)
    {
        if (items == null) return;

        var filesToDelete = items.Cast<BmsAudioFile>().ToList();
        DeleteFiles(filesToDelete);
    }

    /// <summary>
    /// 指定されたファイルのコレクションをリストから除外します。
    /// </summary>
    public void DeleteFiles(IEnumerable<BmsAudioFile> filesToDelete)
    {
        if (filesToDelete == null) return;

        var itemsToRemove = filesToDelete.ToList();

        foreach (var file in itemsToRemove)
        {
            FileListItems.Remove(file);
        }
    }

    private void OnAudioPlaybackStateChanged(object? sender, AudioPreviewService.PlaybackStateChangedEventArgs e)
    {
        AudioPlaybackStateChanged?.Invoke(sender, e);
    }

    /// <summary>
    /// ファイルリスト読み込み完了イベントの引数を提供します。
    /// </summary>
    public class FileListLoadedEventArgs : EventArgs
    {
        public string FilePath { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 選択キーワード変更イベントの引数を提供します。
    /// </summary>
    public class SelectedKeywordsChangedEventArgs : EventArgs
    {
        public string[] SelectedKeywords { get; set; } = [];
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _audioPreviewService.PlaybackStateChanged -= OnAudioPlaybackStateChanged;
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
