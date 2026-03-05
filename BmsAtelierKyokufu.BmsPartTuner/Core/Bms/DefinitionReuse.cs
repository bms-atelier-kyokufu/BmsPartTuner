using System.Collections.ObjectModel;
using System.Diagnostics;
using BmsAtelierKyokufu.BmsPartTuner.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

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
/// <item>BMSファイルの書き換え（<see cref="BmsFileRewriter"/>）</item>
/// <item>ファイル保存</item>
/// </list>
/// 
/// <para>【設計パターン】</para>
/// Orchestrator（Facade）パターン: 複雑なサブシステムを単純なインターフェースで提供。
/// </remarks>
public class DefinitionReuse
{
    #region フィールド

    private readonly IReadOnlyList<WavFiles> _fileList;
    private readonly int[] _replaces = new int[AppConstants.Definition.ReplaceTableSize];
    private readonly DefinitionRangeManager _rangeManager;
    private DefinitionStatistics _statistics;
    private BmsFileRewriter? _rewriter;

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
    public DefinitionReuse(ObservableCollection<WavFiles> fileList)
    {
        _fileList = fileList?.ToList() ?? throw new ArgumentNullException(nameof(fileList));
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
    /// <param name="progress">進捗報告用のIProgress（0-100%）。</param>
    /// <param name="r2Val">相関係数のしきい値（0.0～1.0、推奨: 0.95）。</param>
    /// <param name="saveFileName">出力先ファイルのパス。</param>
    /// <param name="defStart">処理範囲の開始定義番号。</param>
    /// <param name="defEnd">処理範囲の終了定義番号（0=自動検出）。</param>
    /// <param name="normalizationMode">正規化モード（デフォルト: None）。</param>
    /// <param name="selectedKeywords">選択されたキーワード（nullまたは空の場合は全て処理）。</param>
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
        IProgress<int> progress,
        float r2Val,
        string saveFileName,
        int defStart,
        int defEnd,
        bool isPhysicalDeletionEnabled,
        Models.NormalizationMode normalizationMode = Models.NormalizationMode.None,
        IEnumerable<string>? selectedKeywords = null)
    {
        var sw = Stopwatch.StartNew();
        progress.Report(0);

        _rangeManager.DetermineProcessingRange(defStart, defEnd);

        // 範囲確定後に統計クラスを再初期化
        // Why: コンストラクタ時点では範囲が未確定(0-0)のため、正しい範囲で作り直す必要がある
        _statistics = new DefinitionStatistics(_fileList, _replaces,
            _rangeManager.StartPoint, _rangeManager.EndPoint);

        Debug.WriteLine("=== Phase 1: Preloading audio data ===");
        AudioCacheManager.PreloadAudioData(_fileList, progress, normalizationMode);
        progress.Report(AppConstants.Progress.PreloadComplete);

        Debug.WriteLine("=== Phase 2: Creating replace table ===");
        CreateReplaceTable(progress, r2Val, selectedKeywords);
        progress.Report(AppConstants.Progress.ComparisonComplete);

        Debug.WriteLine("=== Phase 3: Rewriting and Aligning BMS file ===");
        _rewriter = new BmsFileRewriter(_fileList, _replaces,
            _rangeManager.StartPoint, _rangeManager.EndPoint);
        var writeData = _rewriter.ReplaceAndAlignBmsFile(bmsFileName);
        progress.Report(AppConstants.Progress.RewriteComplete);

        _rewriter.WriteBmsFile(saveFileName, writeData);

        if (isPhysicalDeletionEnabled)
        {
            PerformPhysicalDeletion(progress);
        }

        progress.Report(AppConstants.Progress.Complete);

        sw.Stop();
        Debug.WriteLine($"=== DefinitionReuse completed in {sw.ElapsedMilliseconds} ms ({sw.ElapsedMilliseconds / 1000.0:F2}s) ===");

        _statistics.LogStatistics();
    }

    private void PerformPhysicalDeletion(IProgress<int> progress)
    {
        if (_rewriter == null) return;

        var unusedFiles = _fileList.Except(_rewriter.KeptFiles).ToList();
        Debug.WriteLine($"=== Physical Deletion: {unusedFiles.Count} files to delete ===");

        int deletedCount = 0;
        foreach (var file in unusedFiles)
        {
            try
            {
                if (File.Exists(file.Name))
                {
                    File.Delete(file.Name);
                    deletedCount++;
                    Debug.WriteLine($"Deleted: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete {file.Name}: {ex.Message}");
            }
        }
        Debug.WriteLine($"=== Physical Deletion Complete: {deletedCount}/{unusedFiles.Count} files deleted ===");
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
            return new List<string>();
        }

        var keptFilePaths = new HashSet<string>(_rewriter.KeptFiles.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
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
        var groupingStrategy = new AudioFileGroupingStrategy();
        var groups = groupingStrategy.GroupFiles(_fileList,
            _rangeManager.StartPoint, _rangeManager.EndPoint, selectedKeywords);

        var comparisonEngine = new ParallelAudioComparisonEngine(_fileList, _replaces,
            _rangeManager.StartPoint, _rangeManager.EndPoint);
        comparisonEngine.CompareGroups(groups, r2val, progress);
    }

    #endregion
}
