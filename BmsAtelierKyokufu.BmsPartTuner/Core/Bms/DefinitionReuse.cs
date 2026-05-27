using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMS定義の重複削減を統括するメインオーケストレータ。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>全体の処理フロー制御</item>
/// <item>サブシステムの協調（範囲管理、統計情報、ファイル書き換え）</item>
/// <item>進捗管理と統計情報の集計</item>
/// </list>
///
/// <para>【処理フロー】</para>
/// <list type="number">
/// <item>処理範囲の決定（<see cref="DefinitionRangeManager"/>）</item>
/// <item>音声データのプリロード（<see cref="AudioCacheManager"/>）</item>
/// <item>置換テーブルの作成（<see cref="ParallelAudioComparisonEngine"/>）</item>
/// <item>BMSファイルの書き換え（<see cref="BmsFileRewriter"/>）および書き込み（<see cref="BmsFileWriter"/>）</item>
/// <item>ファイル保存</item>
/// </list>
///
/// <para>【設計パターン】</para>
/// Orchestrator（Facade）パターン: 複雑なサブシステムを単純なインターフェースで提供。
/// </remarks>
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
    /// </summary>
    /// <param name="fileList">処理対象の音声ファイルリスト。</param>
    /// <exception cref="ArgumentNullException">fileListがnullの場合。</exception>
    /// <remarks>
    /// <para>【Why ToList()でコピー】</para>
    /// <see cref="ObservableCollection{T}"/>はUI通知用で変更される可能性があるため、
    /// 内部処理用に不変のスナップショットを作成します。
    /// </remarks>
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
    /// BMS定義の重複削減を実行します。
    /// </summary>
    /// <param name="bmsFileName">入力BMSファイルのパス。</param>
    /// <param name="saveFileName">出力先ファイルのパス。</param>
    /// <param name="options">削減実行オプション。</param>
    /// <param name="normalizationMode">正規化モード（デフォルト: None）。</param>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>処理範囲の決定（10%）</item>
    /// <item>音声データのプリロード（10-80%）</item>
    /// <item>置換テーブルの作成（80-90%）</item>
    /// <item>BMSファイルの書き換え（90-100%）</item>
    /// <item>ファイル保存（100%）</item>
    /// </list>
    ///
    /// <para>【パラメータ調整ガイド】</para>
    /// <list type="bullet">
    /// <item>r2Val=0.98: 厳密（ほぼ同一のみ統合）</item>
    /// <item>r2Val=0.95: 標準（推奨、似た音源を統合）</item>
    /// <item>r2Val=0.90: 緩い（やや異なる音源も統合）</item>
    /// </list>
    ///
    /// <para>【Why normalizationMode】</para>
    /// 音量差が大きいファイル群を比較する場合、波形を正規化することで
    /// 音量の影響を排除し、波形の形状のみを比較できます。
    ///
    /// <para>【Why selectedKeywords】</para>
    /// 特定の楽器種別（例: "kick", "snare"）のみを処理対象にすることで、
    /// 処理時間を短縮できます。nullまたは空の場合は全ファイルを処理します。
    /// </remarks>
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
        PerformanceDebugLogger.WriteLine($"  [ReductDefinition] DetermineProcessingRange: {timer.Lap("DetermineProcessingRange")} ms");

        var (_, loadedCache) = AudioCacheManager.PreloadAudioData(_fileList, progress, normalizationMode);
        _audioCache = loadedCache;
        PerformanceDebugLogger.WriteLine($"  [ReductDefinition] PreloadAudioData: {timer.Lap("PreloadAudioData")} ms");
        progress.Report(AppConstants.Progress.PreloadComplete);

        CreateReplaceTable(progress, options.R2Threshold, options.SelectedKeywords);
        PerformanceDebugLogger.WriteLine($"  [ReductDefinition] CreateReplaceTable: {timer.Lap("CreateReplaceTable")} ms");
        progress.Report(AppConstants.Progress.ComparisonComplete);

        _rewriter = new BmsFileRewriter(_fileList, _replaces,
            _rangeManager.StartPoint, _rangeManager.EndPoint, _inputBmsContent);
        var writeData = _rewriter.ReplaceAndAlignBmsFile(bmsFileName);
        PerformanceDebugLogger.WriteLine($"  [ReductDefinition] ReplaceAndAlignBmsFile: {timer.Lap("ReplaceAndAlignBmsFile")} ms");
        progress.Report(AppConstants.Progress.RewriteComplete);

        BmsFileWriter.WriteBmsFile(saveFileName, writeData);
        PerformanceDebugLogger.WriteLine($"  [ReductDefinition] WriteBmsFile to disk: {timer.Lap("WriteBmsFile to disk")} ms");

        // メモリ上のスライスを物理ディスクに書き出す
        FlushMemorySlicesToDisk(saveFileName);
        PerformanceDebugLogger.WriteLine($"  [ReductDefinition] FlushMemorySlicesToDisk: {timer.Lap("FlushMemorySlicesToDisk")} ms");

        if (options.IsPhysicalDeletionEnabled)
        {
            PerformPhysicalDeletion();
            PerformanceDebugLogger.WriteLine($"  [ReductDefinition] PerformPhysicalDeletion: {timer.Lap("PerformPhysicalDeletion")} ms");
        }

        progress.Report(AppConstants.Progress.Complete);

        long totalElapsed = timerTotal.Lap("Total");
        PerformanceDebugLogger.WriteLine($"=== DefinitionReuse completed in {totalElapsed} ms ({totalElapsed / 1000.0:F2}s) ===");

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
        PerformanceDebugLogger.WriteLine($"=== Physical Deletion: {unusedFiles.Count} files to delete ===");

        int deletedCount = 0;
        foreach (var file in unusedFiles)
        {
            try
            {
                if (File.Exists(file.Name))
                {
                    File.Delete(file.Name);
                    deletedCount++;
                    PerformanceDebugLogger.WriteLine($"Deleted: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                PerformanceDebugLogger.WriteLine($"Failed to delete {file.Name}: {ex.Message}");
            }
        }
        PerformanceDebugLogger.WriteLine($"=== Physical Deletion Complete: {deletedCount}/{unusedFiles.Count} files deleted ===");
    }

    /// <summary>
    /// 削減後のユニークファイル数を取得します。
    /// </summary>
    /// <returns>ユニークファイル数。</returns>
    /// <remarks>
    /// <para>【用途】</para>
    /// 自動最適化（<see cref="Core.Optimization.CorrelationThresholdOptimizer"/>）において、
    /// エルボーポイント検出のための評価指標として使用されます。
    /// </remarks>
    public int GetUniqueFileCount()
    {
        return _statistics.GetUniqueFileCount();
    }

    /// <summary>
    /// 削減対象となった（未使用の）ファイルパスのリストを取得します。
    /// </summary>
    /// <returns>未使用ファイルのパスリスト。</returns>
    /// <remarks>
    /// <para>【前提条件】</para>
    /// <see cref="ReductDefinition"/> が実行済みであること。
    /// </remarks>
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
    /// </summary>
    /// <param name="progress">進捗報告用のIProgress。</param>
    /// <param name="r2val">相関係数のしきい値。</param>
    /// <param name="selectedKeywords">選択されたキーワード（nullまたは空の場合は全て処理）。</param>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>ファイルをグループ化（<see cref="AudioFileGroupingStrategy"/>）</item>
    /// <item>グループ単位で並列比較（<see cref="ParallelAudioComparisonEngine"/>）</item>
    /// <item>置換テーブルを更新（スレッドセーフなCAS操作）</item>
    /// </list>
    ///
    /// <para>【Why グループ化】</para>
    /// 全ファイル総当たり比較（O(n²)）を避け、類似ファイルのみを比較（O(Σm²)）することで
    /// 計算量を大幅に削減します（約800倍高速化）。
    /// </remarks>
    private void CreateReplaceTable(IProgress<int> progress, float r2val,
        IEnumerable<string>? selectedKeywords)
    {
        var timer = PerformanceDebugLogger.StartTimer();
        var groups = AudioFileGroupingStrategy.GroupFiles(_audioCache, _fileList, _rangeManager.StartPoint, _rangeManager.EndPoint, selectedKeywords);
        PerformanceDebugLogger.WriteLine($"    [CreateReplaceTable] GroupFiles (groups={groups.Count}): {timer.Lap("GroupFiles")} ms");

        var parameters = new AudioComparisonParameters(_fileList, _audioCache, _replaces, _rangeManager.StartPoint, _rangeManager.EndPoint);
        var comparisonEngine = new ParallelAudioComparisonEngine(parameters);
        comparisonEngine.CompareGroups(groups, r2val, progress);
        PerformanceDebugLogger.WriteLine($"    [CreateReplaceTable] CompareGroups: {timer.Lap("CompareGroups")} ms");
    }

    #endregion
}
