using System.Diagnostics;
using System.Threading;
using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Audio;

/// <summary>
/// 並列オーディオ比較エンジン。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>グループ単位での並列音声比較</item>
/// <item>スレッドセーフな置換テーブル更新</item>
/// <item>進捗レポート</item>
/// </list>
/// 
/// <para>【最適化戦略】</para>
/// <list type="number">
/// <item>Sort &amp; Sweep: RMS値で事前ソートし、近傍のみを比較（$O(N^2)$ → $O(N \times K)$）</item>
/// <item>Union-Find: 推移的な一致関係を効率的に管理（$O(\alpha(n))$）</item>
/// <item>Parallel.ForEach: CPUコアを最大限に活用した並行演算</item>
/// </list>
/// 
/// <para>【並列化戦略】</para>
/// <list type="bullet">
/// <item>グループ間: 並列（Parallel.ForEach）</item>
/// <item>グループ内: 順次（メモリ効率）</item>
/// <item>最大並列度: CPUコア数</item>
/// </list>
/// 
/// <para>【スレッドセーフ】</para>
/// <list type="bullet">
/// <item>Interlocked操作（原子性保証）</item>
/// <item>CAS（Compare-And-Swap）</item>
/// <item>ロックレス設計</item>
/// </list>
/// </remarks>
internal class ParallelAudioComparisonEngine
{
    #region 定数定義

    /// <summary>進捗レポートの範囲（Phase 2 の幅）。</summary>
    private const int ProgressPhase2Range = AppConstants.Progress.ComparisonComplete - AppConstants.Progress.PreloadComplete;

    #endregion

    #region フィールド

    private readonly IReadOnlyList<WavFiles> _fileList;
    private readonly int[] _replaceTable;
    private readonly int _startPoint;
    private readonly int _endPoint;

    #endregion

    #region コンストラクタ

    /// <summary>
    /// ParallelAudioComparisonEngineのインスタンスを作成。
    /// </summary>
    /// <param name="fileList">音声ファイルリスト。</param>
    /// <param name="replaceTable">置換テーブル。</param>
    /// <param name="startPoint">処理開始位置。</param>
    /// <param name="endPoint">処理終了位置。</param>
    /// <exception cref="ArgumentNullException">fileListまたはreplaceTableがnullの場合。</exception>
    public ParallelAudioComparisonEngine(
        IReadOnlyList<WavFiles> fileList,
        int[] replaceTable,
        int startPoint,
        int endPoint)
    {
        _fileList = fileList ?? throw new ArgumentNullException(nameof(fileList));
        _replaceTable = replaceTable ?? throw new ArgumentNullException(nameof(replaceTable));
        _startPoint = startPoint;
        _endPoint = endPoint;
    }

    #endregion

    #region RMSソート用構造体

    /// <summary>
    /// RMSソート用の軽量構造体。
    /// </summary>
    /// <remarks>
    /// <para>【Why readonly struct】</para>
    /// ヒープ割り当てを避け、スタック上で高速に処理するため。
    /// 
    /// <para>【ソートキー】</para>
    /// <list type="bullet">
    /// <item>第1キー: RMS値（昇順）</item>
    /// <item>第2キー: ファイル番号（決定性の保証）</item>
    /// </list>
    /// </remarks>
    private readonly struct AudioEntry : IComparable<AudioEntry>
    {
        public readonly int OriginalIndex;
        public readonly float Rms;
        public readonly int FileNum;

        public AudioEntry(int index, float rms, int fileNum)
        {
            OriginalIndex = index;
            Rms = rms;
            FileNum = fileNum;
        }

        /// <summary>
        /// RMS値で昇順比較、同じRMSの場合はファイル番号で比較（決定性の保証）。
        /// </summary>
        public int CompareTo(AudioEntry other)
        {
            int rmsCompare = Rms.CompareTo(other.Rms);
            if (rmsCompare != 0)
                return rmsCompare;

            return FileNum.CompareTo(other.FileNum);
        }
    }

    #endregion

    #region パブリックメソッド

    /// <summary>
    /// グループ単位の音声ファイル比較。
    /// </summary>
    /// <param name="groups">ファイルインデックスのグループリスト。</param>
    /// <param name="r2Threshold">相関係数しきい値。</param>
    /// <param name="progress">進捗報告用のIProgress。</param>
    /// <remarks>
    /// <para>【処理内容】</para>
    /// <list type="number">
    /// <item>各グループを並列処理（Parallel.ForEach）</item>
    /// <item>グループ内でSort &amp; Sweep比較</item>
    /// <item>スレッドセーフに置換テーブルを更新</item>
    /// </list>
    /// 
    /// <para>【Why グループ並列】</para>
    /// グループ間は独立しているため、並列処理でCPUコアを最大限に活用できます。
    /// グループ内は順次処理することで、メモリアクセスの局所性を保ちます。
    /// </remarks>
    public void CompareGroups(
        IReadOnlyList<IReadOnlyList<int>> groups,
        float r2Threshold,
        IProgress<int> progress,
        CancellationToken cancellationToken = default)
    {
        int processedCount = 0;
        int totalFiles = groups.Sum(g => g.Count);
        int totalComparisons = 0;
        int totalMatches = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        var sw = Stopwatch.StartNew();

        try
        {
            Parallel.ForEach(groups, parallelOptions, (group) =>
            {
                // check for cancellation at start of group processing
                parallelOptions.CancellationToken.ThrowIfCancellationRequested();

                int groupComparisons = 0;
                int groupMatches = 0;
                int groupSkipped = 0;
                int groupOutOfRange = 0;
                int groupAlreadyProcessed = 0;
                int groupSelfMarked = 0;

                CompareGroup(group, r2Threshold, ref processedCount, totalFiles, progress,
                    ref groupComparisons, ref groupMatches, ref groupSkipped, ref groupOutOfRange,
                    ref groupAlreadyProcessed, ref groupSelfMarked, cancellationToken);

                Interlocked.Add(ref totalComparisons, groupComparisons);
                Interlocked.Add(ref totalMatches, groupMatches);
            });
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("=== CompareGroups Cancelled ===");
            throw;
        }

        sw.Stop();
        Debug.WriteLine($"=== CompareGroups Complete: {totalComparisons} comparisons, {sw.ElapsedMilliseconds}ms ===");
    }

    #endregion

    #region プライベートメソッド

    /// <summary>
    /// 単一グループの比較処理（Sort &amp; Sweep 最適化版）。
    /// </summary>
    /// <remarks>
    /// <para>【アルゴリズム】</para>
    /// <list type="number">
    /// <item>ファイルをRMS値でソート: $O(N \log N)$</item>
    /// <item>各ファイルに対して近傍のみを比較: $O(N \times K)$</item>
    /// <item>RMS差が大きいファイルはスキップ</item>
    /// </list>
    /// 
    /// <para>【計算量】</para>
    /// $O(N \log N) + O(N \times K)$（$K \ll N$）
    /// 
    /// <para>【Why Sort &amp; Sweep】</para>
    /// RMS値が近いファイルのみが音響的に類似している可能性が高いため、
    /// ソート後に近傍のみを比較することで、計算量を大幅に削減します。
    /// </remarks>
    private void CompareGroup(
        IReadOnlyList<int> group,
        float r2Threshold,
        ref int processedCount,
        int totalFiles,
        IProgress<int> progress,
        ref int comparisons,
        ref int matches,
        ref int skipped,
        ref int outOfRange,
        ref int alreadyProcessed,
        ref int selfMarked,
        CancellationToken cancellationToken)
    {
        if (group.Count == 1)
        {
            MarkSelf(group[0], ref processedCount, totalFiles, progress, ref selfMarked);
            return;
        }

        var entries = CreateSortedEntries(group);
        PerformSortAndSweep(entries, r2Threshold, ref processedCount, totalFiles, progress,
            ref comparisons, ref matches, ref skipped, ref outOfRange, ref alreadyProcessed, ref selfMarked, cancellationToken);
    }

    /// <summary>
    /// 単一ファイルグループの処理（自分自身をマーク）。
    /// </summary>
    private void MarkSelf(int idx, ref int processedCount, int totalFiles, IProgress<int> progress, ref int selfMarked)
    {
        int fileNum = _fileList[idx].NumInteger;
        if (fileNum >= _startPoint && fileNum <= _endPoint)
        {
            if (Interlocked.CompareExchange(ref _replaceTable[fileNum], fileNum, 0) == 0)
            {
                Interlocked.Increment(ref selfMarked);
            }
        }
        Interlocked.Increment(ref processedCount);
        ReportProgress(ref processedCount, totalFiles, progress);
    }

    /// <summary>
    /// グループ内のファイルをRMS値でソートしたエントリ配列を作成。
    /// </summary>
    private AudioEntry[] CreateSortedEntries(IReadOnlyList<int> group)
    {
        var entries = new AudioEntry[group.Count];
        for (int i = 0; i < group.Count; i++)
        {
            int idx = group[i];
            var cachedData = _fileList[idx].CachedData;
            float rms = (cachedData == null) ? float.MaxValue : cachedData.TotalRms;
            entries[i] = new AudioEntry(idx, rms, _fileList[idx].NumInteger);
        }
        Array.Sort(entries);
        return entries;
    }

    /// <summary>
    /// Sort &amp; Sweepアルゴリズムで比較を実行。
    /// </summary>
    /// <remarks>
    /// RMS値の昇順にソートされたエントリに対して、
    /// 各エントリと後続の近傍エントリのみを比較します。
    /// </remarks>
    private void PerformSortAndSweep(
        AudioEntry[] entries,
        float r2Threshold,
        ref int processedCount,
        int totalFiles,
        IProgress<int> progress,
        ref int comparisons,
        ref int matches,
        ref int skipped,
        ref int outOfRange,
        ref int alreadyProcessed,
        ref int selfMarked,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int iIdx = entries[i].OriginalIndex;
            int iVal = _fileList[iIdx].NumInteger;

            if (iVal < _startPoint || iVal > _endPoint || _replaceTable[iVal] != 0)
            {
                Interlocked.Increment(ref processedCount);
                continue;
            }

            if (Interlocked.CompareExchange(ref _replaceTable[iVal], iVal, 0) != 0)
            {
                Interlocked.Increment(ref processedCount);
                continue;
            }

            var cachedData1 = _fileList[iIdx].CachedData;
            if (cachedData1 != null)
            {
                CompareWithNearbyEntries(entries, i, cachedData1, r2Threshold, ref comparisons, ref matches, ref skipped);
            }

            Interlocked.Increment(ref processedCount);
            ReportProgress(ref processedCount, totalFiles, progress);
        }
    }

    /// <summary>
    /// 近傍エントリとの比較。
    /// </summary>
    /// <remarks>
    /// <para>【早期終了条件】</para>
    /// RMS差がしきい値を超えた時点で比較を打ち切ります。
    /// ソート済みなので、それ以降のファイルも条件を満たしません。
    /// </remarks>
    private void CompareWithNearbyEntries(AudioEntry[] entries, int currentIndex, CachedSoundData cachedData1, float r2Threshold, ref int comparisons, ref int matches, ref int skipped)
    {
        float rms1 = entries[currentIndex].Rms;
        var thresholds = CalculateRmsThresholds(rms1);

        for (int j = currentIndex + 1; j < entries.Length; j++)
        {
            if (entries[j].Rms > thresholds.max) break;
            CompareFilePair(entries[currentIndex].OriginalIndex, entries[j].OriginalIndex, cachedData1, r2Threshold, ref comparisons, ref matches, ref skipped);
        }
    }

    /// <summary>
    /// RMS類似性判定のしきい値を計算。
    /// </summary>
    /// <returns>最小値と最大値のタプル。</returns>
    /// <remarks>
    /// <para>【しきい値】</para>
    /// <list type="bullet">
    /// <item>通常: RMS × (0.8～1.25)（±20～25%）</item>
    /// <item>無音ファイル: 0～0.002（特別扱い）</item>
    /// </list>
    /// </remarks>
    private (float min, float max) CalculateRmsThresholds(float rms)
    {
        if (rms < AppConstants.AudioComparison.SilenceRmsThreshold) return (0f, AppConstants.AudioComparison.SilenceRmsUpperBound);
        return (rms * AppConstants.AudioComparison.RmsLowerBoundRatio, rms * AppConstants.AudioComparison.RmsUpperBoundRatio);
    }

    /// <summary>
    /// ファイルペアの比較。
    /// </summary>
    /// <remarks>
    /// <para>【比較ステップ】</para>
    /// <list type="number">
    /// <item>範囲チェック</item>
    /// <item>高速チェック（ファイル名、フィンガープリント）</item>
    /// <item>詳細波形比較（<see cref="FastWaveCompare"/>）</item>
    /// <item>一致した場合、置換テーブル更新</item>
    /// </list>
    /// </remarks>
    private void CompareFilePair(int iIdx, int jIdx, CachedSoundData cachedData1, float r2Threshold, ref int comparisons, ref int matches, ref int skipped)
    {
        int iVal = _fileList[iIdx].NumInteger;
        int jVal = _fileList[jIdx].NumInteger;

        if (jVal < _startPoint || jVal > _endPoint || _replaceTable[jVal] != 0) return;

        if (_fileList[iIdx].Name.Equals(_fileList[jIdx].Name) ||
            (!string.IsNullOrEmpty(_fileList[iIdx].AudioFingerprint) && _fileList[iIdx].AudioFingerprint.Equals(_fileList[jIdx].AudioFingerprint)))
        {
            UpdateReplaceTable(iIdx, jIdx);
            Interlocked.Increment(ref matches);
            return;
        }

        var cachedData2 = _fileList[jIdx].CachedData;
        if (cachedData2 == null) { Interlocked.Increment(ref skipped); return; }

        Interlocked.Increment(ref comparisons);
        if (FastWaveCompare.IsMatch(cachedData1, cachedData2, r2Threshold))
        {
            UpdateReplaceTable(iIdx, jIdx);
            Interlocked.Increment(ref matches);
        }
    }

    /// <summary>
    /// 置換テーブルの更新（Union-Find 方式）。
    /// </summary>
    /// <remarks>
    /// <para>【Union-Findアルゴリズム】</para>
    /// 経路圧縮により推移的なマッチングを効率的に管理（$O(\alpha(n))$）。
    /// 
    /// <para>【スレッドセーフ】</para>
    /// CompareExchangeによるCAS操作で、ロックレスな更新を実現します。
    /// 
    /// <para>【例】</para>
    /// A=B, B=C → A=C（推移的統合）
    /// </remarks>
    private void UpdateReplaceTable(int i, int j)
    {
        int rootI = FindRoot(_fileList[i].NumInteger);
        int rootJ = FindRoot(_fileList[j].NumInteger);

        if (rootI == rootJ) return;

        int minRoot = Math.Min(rootI, rootJ);
        int maxRoot = Math.Max(rootI, rootJ);

        Interlocked.CompareExchange(ref _replaceTable[maxRoot], minRoot, 0);
        Interlocked.CompareExchange(ref _replaceTable[maxRoot], minRoot, maxRoot);
    }

    /// <summary>
    /// Union-Findのルート検索（経路圧縮付き）。
    /// </summary>
    /// <remarks>
    /// 経路圧縮により、2回目以降のルート検索が高速化されます。
    /// </remarks>
    private int FindRoot(int fileNum)
    {
        int current = fileNum;
        int parent = _replaceTable[current];

        if (parent == 0 || parent == current) return current;

        int root = FindRoot(parent);
        if (root != parent) Interlocked.CompareExchange(ref _replaceTable[current], root, parent);
        return root;
    }

    /// <summary>
    /// 進捗レポート。
    /// </summary>
    /// <remarks>
    /// 100ファイルごとまたは完了時に進捗を報告することで、
    /// オーバーヘッドを削減します。
    /// </remarks>
    private void ReportProgress(ref int processedCount, int totalCount, IProgress<int> progress)
    {
        int current = processedCount;
        if (current % 100 == 0 || current == totalCount)
        {
            int percentage = AppConstants.Progress.PreloadComplete + (int)((float)current / totalCount * ProgressPhase2Range);
            progress.Report(percentage);
        }
    }

    #endregion
}
