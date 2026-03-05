using System.Collections.ObjectModel;
using System.Windows;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// ファイルリストViewModel。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>BMSファイルの読み込みとファイルリスト表示</item>
/// <item>楽器種別フィルタリング（キーワードベース）</item>
/// <item>音声プレビュー機能の制御</item>
/// <item>選択されたキーワードの管理</item>
/// </list>
/// 
/// <para>【フィルタリング機能】</para>
/// <list type="number">
/// <item>テキストフィルタ: ファイル名による部分一致検索</item>
/// <item>楽器種別フィルタ: kick, snare等のキーワードによる分類</item>
/// </list>
/// 
/// <para>【楽器種別検出】</para>
/// <see cref="InstrumentNameDetectionService"/>により、
/// ファイル名から楽器種別を自動検出し、フィルタチップとして表示します。
/// 
/// <para>【Why フィルタリング】</para>
/// BMSファイルには数百～数千のWAVファイルが含まれるため、
/// 楽器種別で絞り込むことで、特定のパート（ドラムのみ等）の
/// 最適化が可能になります。
/// 
/// <para>【音声プレビュー】</para>
/// ファイルリストでファイルを選択すると、
/// <see cref="AudioPreviewService"/>により自動的に音声が再生されます。
/// </remarks>
public partial class FileListViewModel : ObservableObject, IDisposable
{
    private readonly AudioPreviewService _audioPreviewService;
    private readonly InstrumentNameDetectionService _instrumentDetectionService;
    private FileListFilterService? _filterService;
    private FileList? _bmsFileList;
    private bool disposedValue;

    [ObservableProperty]
    private ObservableCollection<WavFiles> _fileListItems = new();

    [ObservableProperty]
    private WavFiles? _selectedFile;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private Visibility _clearFilterButtonVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private ObservableCollection<InstrumentNameDetectionService.InstrumentGroup> _instrumentGroups = new();

    [ObservableProperty]
    private ObservableCollection<FileListFilterService.SelectableFilterChip> _filterChips = new();

    /// <summary>BMSファイルリスト。</summary>
    public FileList? BmsFileList => _bmsFileList;

    /// <summary>ファイルリスト読み込み完了イベント。</summary>
    public event EventHandler<FileListLoadedEventArgs>? FileListLoaded;

    /// <summary>音声再生状態変更イベント。</summary>
    public event EventHandler<AudioPreviewService.PlaybackStateChangedEventArgs>? AudioPlaybackStateChanged;

    /// <summary>選択キーワード変更イベント。</summary>
    public event EventHandler<SelectedKeywordsChangedEventArgs>? SelectedKeywordsChanged;

    /// <summary>
    /// FileListViewModelを初期化。
    /// </summary>
    /// <param name="audioPreviewService">音声プレビューサービス。</param>
    /// <param name="instrumentDetectionService">楽器名検出サービス。</param>
    /// <exception cref="ArgumentNullException">引数がnullの場合。</exception>
    public FileListViewModel(
        AudioPreviewService audioPreviewService,
        InstrumentNameDetectionService instrumentDetectionService)
    {
        _audioPreviewService = audioPreviewService ?? throw new ArgumentNullException(nameof(audioPreviewService));
        _instrumentDetectionService = instrumentDetectionService ?? throw new ArgumentNullException(nameof(instrumentDetectionService));

        _audioPreviewService.PlaybackStateChanged += OnAudioPlaybackStateChanged;
    }

    /// <summary>
    /// フィルタテキスト変更時の処理。
    /// </summary>
    /// <param name="value">新しいフィルタテキスト。</param>
    /// <remarks>
    /// フィルタテキストが空の場合、クリアボタンを非表示にします。
    /// </remarks>
    partial void OnFilterTextChanged(string value)
    {
        ClearFilterButtonVisibility = string.IsNullOrWhiteSpace(value) ?
            Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// 選択ファイル変更時の処理。
    /// </summary>
    /// <param name="value">新しい選択ファイル。</param>
    /// <remarks>
    /// ファイルが選択されると、自動的に音声プレビューを開始します。
    /// デバウンス機能により、連続選択時は最後のファイルのみ再生されます。
    /// </remarks>
    partial void OnSelectedFileChanged(WavFiles? value)
    {
        if (value != null)
        {
            _ = _audioPreviewService.PreviewAudioAsync(value.Name);
        }
    }

    /// <summary>
    /// フィルタサービスを設定。
    /// </summary>
    /// <param name="filterService">フィルタサービス。</param>
    /// <remarks>
    /// DIコンテナから注入されたフィルタサービスを設定します。
    /// </remarks>
    public void SetFilterService(FileListFilterService filterService)
    {
        _filterService = filterService;
    }

    /// <summary>
    /// BMSファイルを読み込み。
    /// </summary>
    /// <param name="bmsFilePath">BMSファイルのパス。</param>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item><see cref="FileList"/>を生成</item>
    /// <item>ファイルリストを作成</item>
    /// <item>楽器種別を検出してフィルタチップを生成</item>
    /// <item><see cref="FileListLoaded"/>イベントを発火</item>
    /// </list>
    /// 
    /// <para>【エラーハンドリング】</para>
    /// 読み込みエラー時は、<see cref="FileListLoaded"/>イベントで
    /// IsSuccess=falseとエラーメッセージを通知します。
    /// </remarks>
    public void LoadBmsFile(string bmsFilePath)
    {
        try
        {
            _bmsFileList = new FileList(bmsFilePath);
            var fileList = _bmsFileList.CreateFileList();
            FileListItems = fileList;

            var chips = _filterService?.GenerateFilterChips(fileList) ?? new List<FileListFilterService.FilterChip>();

            var instrumentGroups = chips
                .Select(c => new InstrumentNameDetectionService.InstrumentGroup
                {
                    Name = c.Keyword,
                    Count = c.Count,
                    IsSelected = true
                })
                .ToList();

            InstrumentGroups = new ObservableCollection<InstrumentNameDetectionService.InstrumentGroup>(instrumentGroups);

            if (_bmsFileList.MissingFiles.Count == 0 && fileList.Count > 0)
            {
                FileListLoaded?.Invoke(this, new FileListLoadedEventArgs
                {
                    FilePath = bmsFilePath,
                    FileCount = fileList.Count,
                    IsSuccess = true
                });
            }
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

    /// <summary>
    /// フィルタチップの選択状態を切り替え。
    /// </summary>
    /// <param name="chip">対象のフィルタチップ。</param>
    /// <remarks>
    /// チップの選択/解除を切り替え、選択キーワード変更イベントを発火します。
    /// </remarks>
    public void ToggleChipSelection(FileListFilterService.SelectableFilterChip chip)
    {
        if (chip == null) return;

        chip.IsSelected = !chip.IsSelected;
        NotifySelectedKeywordsChanged();
    }

    /// <summary>
    /// 選択されたキーワードを取得。
    /// </summary>
    /// <returns>選択されたキーワードの配列。</returns>
    /// <remarks>
    /// <para>【用途】</para>
    /// 定義削減処理時に、特定の楽器種別のみを対象にする場合に使用されます。
    /// 
    /// <para>【例】</para>
    /// ["kick", "snare"] → ドラム系のみを処理
    /// </remarks>
    public string[] GetSelectedKeywords()
    {
        return FilterChips
            .Where(chip => chip.IsSelected)
            .Select(chip => chip.Keyword)
            .ToArray();
    }

    private void NotifySelectedKeywordsChanged()
    {
        var selectedKeywords = GetSelectedKeywords();
        SelectedKeywordsChanged?.Invoke(this, new SelectedKeywordsChangedEventArgs
        {
            SelectedKeywords = selectedKeywords
        });
    }

    /// <summary>
    /// フィルタをクリア。
    /// </summary>
    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
    }

    /// <summary>
    /// 楽器フィルタを切り替え。
    /// </summary>
    /// <param name="parameter">楽器グループ。</param>
    /// <remarks>
    /// 楽器グループの選択/解除を切り替え、フィルタを適用します。
    /// </remarks>
    [RelayCommand]
    private void ToggleInstrumentFilter(object? parameter)
    {
        if (parameter is InstrumentNameDetectionService.InstrumentGroup instrumentGroup)
        {
            instrumentGroup.IsSelected = !instrumentGroup.IsSelected;
            ApplyInstrumentFilter();
        }
    }

    /// <summary>
    /// 楽器フィルタを適用。
    /// </summary>
    /// <remarks>
    /// 選択された楽器グループに基づいて、ファイルリストをフィルタリングします。
    /// </remarks>
    private void ApplyInstrumentFilter()
    {
        if (_filterService == null) return;

        var selectedInstruments = new HashSet<string>(
            InstrumentGroups.Where(g => g.IsSelected).Select(g => g.Name),
            StringComparer.OrdinalIgnoreCase);

        _filterService.ApplyInstrumentFilter(selectedInstruments);
    }

    /// <summary>
    /// 選択されたファイルをリストから削除します。
    /// </summary>
    /// <param name="items">削除対象のファイルリスト（IList）。</param>
    /// <remarks>
    /// <para>【用途】</para>
    /// ユーザーがリストから不要なファイルを除外するために使用します。
    /// UIでの複数選択に対応しています。
    /// </remarks>
    [RelayCommand]
    public void DeleteSelectedFiles(System.Collections.IList? items)
    {
        if (items == null) return;

        var filesToDelete = items.Cast<WavFiles>().ToList();
        DeleteFiles(filesToDelete);
    }

    /// <summary>
    /// 指定されたファイルをリストから削除します。
    /// </summary>
    /// <param name="filesToDelete">削除するファイルのリスト。</param>
    public void DeleteFiles(IEnumerable<WavFiles> filesToDelete)
    {
        if (filesToDelete == null) return;

        // リストを複製して操作（foreach中のコレクション変更を避けるため）
        var itemsToRemove = filesToDelete.ToList();

        foreach (var file in itemsToRemove)
        {
            if (FileListItems.Contains(file))
            {
                FileListItems.Remove(file);
            }
        }
    }

    private void OnAudioPlaybackStateChanged(object? sender, AudioPreviewService.PlaybackStateChangedEventArgs e)
    {
        AudioPlaybackStateChanged?.Invoke(sender, e);
    }

    /// <summary>
    /// ファイルリスト読み込み完了イベントの引数。
    /// </summary>
    public class FileListLoadedEventArgs : EventArgs
    {
        /// <summary>ファイルパス。</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>ファイル数。</summary>
        public int FileCount { get; set; }

        /// <summary>成功フラグ。</summary>
        public bool IsSuccess { get; set; }

        /// <summary>エラーメッセージ。</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 選択キーワード変更イベントの引数。
    /// </summary>
    public class SelectedKeywordsChangedEventArgs : EventArgs
    {
        /// <summary>選択されたキーワード。</summary>
        public string[] SelectedKeywords { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// リソースを解放（内部実装）。
    /// </summary>
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

    /// <summary>
    /// リソースを解放。
    /// </summary>
    void IDisposable.Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
