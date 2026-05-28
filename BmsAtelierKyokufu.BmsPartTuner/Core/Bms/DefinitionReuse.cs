using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMS定義の重複削減を統括するメインオーケストレータ（Facade）です。
/// 処理範囲の決定、音声データのプリロード、置換テーブルの作成、BMSファイルの書き換え・保存などの全体の処理フローを制御し、
/// 進捗や統計情報の集計を行います。
/// </summary>
public class DefinitionReuse
{
    #region フィールド

    private readonly IReadOnlyList<BmsAudioFile> _fileList;
    private IReadOnlyDictionary<string, ICachedSoundData> _audioCache;
    private readonly int[] _replaces = new int[AppConstants.Definition.ReplaceTableSize];
    private readonly DefinitionRangeManager _rangeManager;
    private DefinitionStatistics _statistics;
    private BmsFileRewriter? _rewriter;
    private readonly string? _inputBmsContent;

    #endregion

    #region コンストラクタ

    /// <summary>
    /// DefinitionReuseのインスタンスを作成します。
    /// ObservableCollectionはUI通知用で変更される可能性があるため、内部処理用に不変のスナップショットを作成します。
    /// </summary>
    /// <param name="fileList">処理対象の音声ファイルリスト。</param>
    /// <param name="audioCache">音声データキャッシュ。</param>
    /// <param name="inputBmsContent">BMSファイル内容（省略時はパスから読み込み）。</param>
    /// <exception cref="ArgumentNullException">fileListまたはaudioCacheがnullの場合。</exception>
    public DefinitionReuse(ObservableCollection<BmsAudioFile> fileList, IReadOnlyDictionary<string, ICachedSoundData> audioCache, string? inputBmsContent = null)
    {
        _fileList = fileList?.ToList() ?? throw new ArgumentNullException(nameof(fileList));
        _audioCache = audioCache ?? throw new ArgumentNullException(nameof(audioCache));
        _inputBmsContent = inputBmsContent;
        _rangeManager = new DefinitionRangeManager(_fileList);
        _statistics = new DefinitionStatistics(_fileList, _replaces,
            _rangeManager.StartPoint, _rangeManager.EndPoint);
    }

    #endregion

    #region パブリックメソッド

    /// <summary>
    /// BMS定義の重複削減処理を実行します。
    /// 音声データのプリロード、類似音声の比較と置換テーブルの作成、BMSファイルの書き換えを順に実行します。
    /// 音量差の影響を排除するためには正規化モードを指定し、処理時間を短縮するためには特定のキーワード（楽器種別など）によるフィルタリングが行えます。
    /// </summary>
    /// <param name="bmsFileName">入力BMSファイルのパス。</param>
    /// <param name="saveFileName">出力先ファイルのパス。</param>
    /// <param name="options">削減実行オプション。</param>
    /// <param name="normalizationMode">正規化モード（デフォルト: None）。</param>
    public void ReductDefinition(
        string bmsFileName,
        string saveFileName,
        DefinitionReductionOptions options,
        NormalizationMode normalizationMode = NormalizationMode.None)
    {
        ArgumentNullException.ThrowIfNull(options);
        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timer = PerformanceDebugLogger.StartTimer();
        var progress = options.Progress ?? new Progress<int>();
        progress.Report(0);

        _rangeManager.DetermineProcessingRange(options.StartDefinition, options.EndDefinition);

        // 範囲確定後に統計クラスを再初期化
        // Why: コンストラクタ時点では範囲が未確定(0-0)のため、正しい範囲で作り直す必要がある
        _statistics = new DefinitionStatistics(_fileList, _replaces,
            _rangeManager.StartPoint, _rangeManager.EndPoint);
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"DetermineProcessingRange: {timer.Lap("DetermineProcessingRange")} ms");

        var (_, loadedCache) = AudioCacheManager.PreloadAudioData(_fileList, progress, normalizationMode);
        _audioCache = loadedCache;
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"PreloadAudioData: {timer.Lap("PreloadAudioData")} ms");
        progress.Report(AppConstants.Progress.PreloadComplete);

        CreateReplaceTable(progress, options.R2Threshold, options.SelectedKeywords);
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"CreateReplaceTable: {timer.Lap("CreateReplaceTable")} ms");
        progress.Report(AppConstants.Progress.ComparisonComplete);

        _rewriter = new BmsFileRewriter(_fileList, _replaces,
            _rangeManager.StartPoint, _rangeManager.EndPoint, _inputBmsContent);
        var writeData = _rewriter.ReplaceAndAlignBmsFile(bmsFileName);
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"ReplaceAndAlignBmsFile: {timer.Lap("ReplaceAndAlignBmsFile")} ms");
        progress.Report(AppConstants.Progress.RewriteComplete);

        BmsFileWriter.WriteBmsFile(saveFileName, writeData);
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"WriteBmsFile to disk: {timer.Lap("WriteBmsFile to disk")} ms");

        // メモリ上のスライスを物理ディスクに書き出す
        FlushMemorySlicesToDisk(saveFileName);
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"FlushMemorySlicesToDisk: {timer.Lap("FlushMemorySlicesToDisk")} ms");

        if (options.IsPhysicalDeletionEnabled)
        {
            PerformPhysicalDeletion();
            PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"PerformPhysicalDeletion: {timer.Lap("PerformPhysicalDeletion")} ms");
        }

        progress.Report(AppConstants.Progress.Complete);

        long totalElapsed = timerTotal.Lap("Total");
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"=== DefinitionReuse completed in {totalElapsed} ms ({totalElapsed / 1000.0:F2}s) ===");

        _statistics.LogStatistics();
    }

    private void FlushMemorySlicesToDisk(string saveFileName)
    {
        if (_rewriter == null) return;
        string outDir = Path.GetDirectoryName(saveFileName) ?? string.Empty;
        if (string.IsNullOrEmpty(outDir)) return;

        foreach (var file in _rewriter.KeptFiles)
        {
            var fileName = Path.GetFileName(file.Name);
            if (VirtualAudioRegistry.TryGetStream(fileName, out var stream))
            {
                using (stream)
                {
                    var targetPath = Path.Combine(outDir, fileName);
                    using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: false);
                    stream.CopyTo(fs);
                }
            }
            else if (VirtualAudioRegistry.TryGetFile(fileName, out var data))
            {
                var targetPath = Path.Combine(outDir, fileName);
                File.WriteAllBytes(targetPath, data);
            }
        }
    }

    private void PerformPhysicalDeletion()
    {
        if (_rewriter == null) return;

        var unusedFiles = _fileList.Except(_rewriter.KeptFiles).ToList();
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"=== Physical Deletion: {unusedFiles.Count} files to delete ===");

        int deletedCount = 0;
        foreach (var file in unusedFiles)
        {
            try
            {
                if (File.Exists(file.Name))
                {
                    File.Delete(file.Name);
                    deletedCount++;
                    PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"Deleted: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"Failed to delete {file.Name}: {ex.Message}");
            }
        }
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"=== Physical Deletion Complete: {deletedCount}/{unusedFiles.Count} files deleted ===");
    }

    /// <summary>
    /// 削減後のユニークファイル数を取得します。自動最適化におけるエルボーポイント検出のための評価指標として使用されます。
    /// </summary>
    /// <returns>ユニークファイル数。</returns>
    public int GetUniqueFileCount()
    {
        return _statistics.GetUniqueFileCount();
    }

    /// <summary>
    /// 削減対象となった（未使用の）ファイルパスのリストを取得します。
    /// このメソッドは <see cref="ReductDefinition"/> 実行後に呼び出す必要があります。
    /// </summary>
    /// <returns>未使用ファイルのパスリスト。</returns>
    public List<string> GetUnusedFilePaths()
    {
        if (_rewriter == null || _rewriter.KeptFiles == null)
        {
            return [];
        }

        var keptFilePaths = new HashSet<string>(_rewriter.KeptFiles.Select(static f => f.Name), StringComparer.OrdinalIgnoreCase);
        var unusedFiles = new List<string>();

        foreach (var file in _fileList)
        {
            // 保持リストに含まれていないファイルは未使用
            // かつ、ファイルが存在するもののみ対象
            if (!keptFilePaths.Contains(file.Name))
            {
                unusedFiles.Add(file.Name);
            }
        }

        return unusedFiles;
    }

    #endregion

    #region プライベートメソッド

    /// <summary>
    /// 置換テーブルを作成します。
    /// 全ファイル総当たり比較（O(n²)）を避け、ファイルをグループ化して類似ファイルのみを比較（O(Σm²)）することで
    /// 計算量を大幅に削減し、高速にテーブルを構築します。
    /// </summary>
    /// <param name="progress">進捗報告用のIProgress。</param>
    /// <param name="r2val">相関係数のしきい値。</param>
    /// <param name="selectedKeywords">選択されたキーワード（nullまたは空の場合は全て処理）。</param>
    private void CreateReplaceTable(IProgress<int> progress, float r2val,
        IEnumerable<string>? selectedKeywords)
    {
        var timer = PerformanceDebugLogger.StartTimer();
        var groups = AudioFileGroupingStrategy.GroupFiles(_audioCache, _fileList, _rangeManager.StartPoint, _rangeManager.EndPoint, selectedKeywords);
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"    [CreateReplaceTable] GroupFiles (groups={groups.Count}): {timer.Lap("GroupFiles")} ms");

        var parameters = new AudioComparisonParameters(_fileList, _audioCache, _replaces, _rangeManager.StartPoint, _rangeManager.EndPoint);
        var comparisonEngine = new ParallelAudioComparisonEngine(parameters);
        comparisonEngine.CompareGroups(groups, r2val, progress);
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReuse), $"CompareGroups: {timer.Lap("CompareGroups")} ms");
    }

    #endregion
}
