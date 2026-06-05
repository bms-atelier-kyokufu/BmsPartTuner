using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio;
namespace BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;

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
    private static readonly Logger<FileListViewModel> s_logger = new();

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
    /// 現在再生中の音声ファイル名。
    /// </summary>
    [ObservableProperty]
    public partial string? PlayingFileName { get; set; }

    /// <summary>
    /// 音声が再生中かどうかを表すフラグ。
    /// </summary>
    [ObservableProperty]
    public partial bool IsAudioPlaying { get; set; }

    /// <summary>
    /// チップ選択モードがシングル選択かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsSingleChipSelection { get; set; }

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

    partial void OnIsSingleChipSelectionChanged(bool value)
    {
        if (value)
        {
            // シングル選択モード移行時、現在複数選択されている場合は最初の1つだけ残して他を非選択にする
            bool foundFirst = false;
            foreach (var chip in FilterChips)
            {
                if (chip.IsSelected)
                {
                    if (!foundFirst)
                    {
                        foundFirst = true;
                    }
                    else
                    {
                        chip.IsSelected = false;
                    }
                }
            }
            NotifySelectedKeywordsChanged();
        }
    }

    partial void OnFilterChipsChanged(ObservableCollection<FileListFilterService.SelectableFilterChip> value)
    {
        if (value != null && IsSingleChipSelection)
        {
            // シングル選択モードのとき、新たに読み込まれたチップセットでも最初の1つだけ選択状態にし、残りは非選択にする
            bool foundFirst = false;
            foreach (var chip in value)
            {
                if (chip.IsSelected)
                {
                    if (!foundFirst)
                    {
                        foundFirst = true;
                    }
                    else
                    {
                        chip.IsSelected = false;
                    }
                }
            }
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
    public async Task LoadBmsFileAsync(string bmsFilePath, string? bmsContent = null)
    {
        s_logger.WriteDebug($"=== FileListViewModel.LoadBmsFileAsync Started for {Path.GetFileName(bmsFilePath)} ===");
        var timerTotal = s_logger.StartTimer();
        try
        {
            var (bmsFileList, fileList, instrumentGroups) = await Task.Run(() =>
            {
                var timer = s_logger.StartTimer();
                var manager = new BmsDefinitionManager(bmsFilePath, bmsContent);
                var list = manager.CreateFileList();
                s_logger.WriteDebug($"BmsDefinitionManager construction and CreateFileList: {timer.Lap("BmsDefinitionManager construction and CreateFileList")} ms");

                var chips = _filterService?.GenerateFilterChips(list) ?? [];
                var groups = chips
                    .Select(static c => new InstrumentNameDetectionService.InstrumentGroup
                    {
                        Name = c.Keyword,
                        Count = c.Count,
                        IsSelected = true
                    })
                    .ToList();
                s_logger.WriteDebug($"FilterChips and InstrumentGroups generation: {timer.Lap("FilterChips and InstrumentGroups generation")} ms");

                return (manager, list, groups);
            });

            // UIスレッド上でプロパティを更新
            _bmsFileList = bmsFileList;
            FileListItems = fileList;
            InstrumentGroups = new ObservableCollection<InstrumentNameDetectionService.InstrumentGroup>(instrumentGroups);

            if (_bmsFileList.MissingFiles.Count == 0 && fileList.Count > 0)
            {
                FileListLoaded?.Invoke(this, new FileListLoadedEventArgs
                {
                    FilePath = bmsFilePath,
                    FileCount = fileList.Count,
                    IsSuccess = true
                });
                WeakReferenceMessenger.Default.Send(new FileListLoadedMessage(true, bmsFilePath, string.Empty));
            }
            s_logger.WriteDebug($"=== FileListViewModel.LoadBmsFileAsync Finished: {timerTotal.Lap("Total")} ms ===");
        }
        catch (Exception ex)
        {
            FileListLoaded?.Invoke(this, new FileListLoadedEventArgs
            {
                FilePath = bmsFilePath,
                IsSuccess = false,
                ErrorMessage = ex.Message
            });
            WeakReferenceMessenger.Default.Send(new FileListLoadedMessage(false, bmsFilePath, ex.Message));
        }
    }

    // InitializeInstrumentFiltersはTask.Run内部に統合されたため削除

    /// <summary>
    /// 楽器フィルタチップの選択状態を反転させます。
    /// </summary>
    public void ToggleChipSelection(FileListFilterService.SelectableFilterChip chip)
    {
        if (chip == null) return;

        if (IsSingleChipSelection)
        {
            if (chip.IsSelected)
            {
                chip.IsSelected = false;
            }
            else
            {
                // 他のチップを非選択にしてから、対象のチップを選択状態にする
                foreach (var other in FilterChips)
                {
                    if (other != chip && other.IsSelected)
                    {
                        other.IsSelected = false;
                    }
                }
                chip.IsSelected = true;
            }
        }
        else
        {
            chip.IsSelected = !chip.IsSelected;
        }
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

    public event EventHandler? InstrumentFilterChanged;

    private void ApplyInstrumentFilter()
    {
        InstrumentFilterChanged?.Invoke(this, EventArgs.Empty);
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
        IsAudioPlaying = e.IsPlaying;
        PlayingFileName = e.IsPlaying ? e.FileName : null;
        AudioPlaybackStateChanged?.Invoke(sender, e);
        WeakReferenceMessenger.Default.Send(new AudioPlaybackStateChangedMessage(e.IsLoading, e.IsPlaying, e.FileName));
    }

    /// <summary>
    /// 音声ファイルの再生を停止します。
    /// </summary>
    public void StopPlayback()
    {
        _audioPreviewService.StopCurrentPlayback();
    }

    /// <summary>
    /// 指定された音声ファイルを非同期に再生します。
    /// </summary>
    public async Task PlayAudioFileAsync(BmsAudioFile file)
    {
        if (file == null) return;
        await _audioPreviewService.PreviewAudioAsync(file.Name);
    }

    /// <summary>
    /// 再生・停止の状態を切り替えるトグルコマンド。
    /// </summary>
    [RelayCommand]
    private async Task TogglePlaybackAsync(BmsAudioFile? file)
    {
        if (file == null) return;

        var currentFileName = Path.GetFileName(file.Name);
        var isPlaying = IsAudioPlaying && string.Equals(currentFileName, PlayingFileName, StringComparison.OrdinalIgnoreCase);

        if (isPlaying)
        {
            StopPlayback();
        }
        else
        {
            SelectedFile = file;
            await PlayAudioFileAsync(file);
        }
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


