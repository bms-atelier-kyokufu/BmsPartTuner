namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
/// <summary>
/// 並列オーディオ比較エンジンの実行パラメーター。
/// </summary>
internal record AudioComparisonParameters(
    IReadOnlyList<BmsAudioFile> FileList,
    IReadOnlyDictionary<string, ICachedSoundData> AudioCache,
    int[] ReplaceTable,
    int StartPoint,
    int EndPoint
);

/// <summary>
/// グループ単位での並列音声比較やスレッドセーフな置換テーブルの更新を行うオーディオ比較エンジンです。
/// RMS値による事前ソート（Sort &amp; Sweep）やUnion-Findを用いたマッチング管理により、効率的な比較を実現します。
/// </summary>
internal class ParallelAudioComparisonEngine(AudioComparisonParameters parameters)
{
    #region 定数定義

    /// <summary>進捗レポートの範囲（Phase 2 の幅）。</summary>
    private const int ProgressPhase2Range = AppConstants.Progress.ComparisonComplete - AppConstants.Progress.PreloadComplete;

    #endregion

    #region フィールド

    private readonly IReadOnlyList<BmsAudioFile> _fileList = parameters.FileList ?? throw new ArgumentNullException(nameof(parameters.FileList));
    private readonly int[] _replaceTable = parameters.ReplaceTable ?? throw new ArgumentNullException(nameof(parameters.ReplaceTable));
    private readonly int _startPoint = parameters.StartPoint;
    private readonly int _endPoint = parameters.EndPoint;
    private readonly IReadOnlyDictionary<string, ICachedSoundData> _audioCache = parameters.AudioCache ?? throw new ArgumentNullException(nameof(parameters.AudioCache));
    private readonly long[] _fileSizes = BuildFileSizeArray(parameters.FileList);

    private static long[] BuildFileSizeArray(IReadOnlyList<BmsAudioFile> fileList)
    {
        long[] sizes = new long[3844]; // Max Base62 "ZZ" is 3843
        if (fileList == null) return sizes;

        foreach (var file in fileList)
        {
            if (file.NumInteger >= 0 && file.NumInteger < sizes.Length)
            {
                sizes[file.NumInteger] = file.FileSize;
            }
        }
        return sizes;
    }

    #endregion

    #region RMSソート用構造体

    /// <summary>
    /// RMSソート用の軽量構造体。ヒープ割り当てを避け、スタック上で高速に処理します。
    /// RMS値で昇順ソートし、同じRMSの場合はファイル番号でソートすることで決定性を保証します。
    /// </summary>
    private readonly struct AudioEntry(int index, float rms, int fileNum, float[]? pivotDistances = null) : IComparable<AudioEntry>
    {
        public readonly int OriginalIndex = index;
        public readonly float Rms = rms;
        public readonly int FileNum = fileNum;
        public readonly float[]? PivotDistances = pivotDistances;

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
    /// グループ単位の音声ファイル比較を行います。
    /// 各グループを並列処理（Parallel.ForEach）し、グループ内ではSort &amp; Sweepを用いて効率的に比較します。
    /// スレッドセーフに置換テーブルを更新します。
    /// </summary>
    /// <param name="groups">ファイルインデックスのグループリスト。</param>
    /// <param name="r2Threshold">相関係数しきい値。</param>
    /// <param name="progress">進捗報告用のIProgress。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
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

        var timer = PerformanceDebugLogger.StartTimer();

        try
        {
            Parallel.ForEach(groups, parallelOptions, (group) =>
            {
                // check for cancellation at start of group processing
                parallelOptions.CancellationToken.ThrowIfCancellationRequested();

                int groupComparisons = 0;
                int groupMatches = 0;
                int groupSkipped = 0;

                CompareGroup(group, r2Threshold, ref processedCount, totalFiles, progress,
                    ref groupComparisons, ref groupMatches, ref groupSkipped, cancellationToken);

                Interlocked.Add(ref totalComparisons, groupComparisons);
                Interlocked.Add(ref totalMatches, groupMatches);
            });
        }
        catch (OperationCanceledException)
        {
            PerformanceDebugLogger.WriteDebug(nameof(ParallelAudioComparisonEngine), "=== CompareGroups Cancelled ===");
            throw;
        }

        PerformanceDebugLogger.WriteDebug(nameof(ParallelAudioComparisonEngine), $"=== CompareGroups Complete: {totalComparisons} comparisons, {timer.Lap("CompareGroups")}ms ===");
    }

    #endregion

    #region プライベートメソッド

    /// <summary>
    /// 単一グループの比較処理（Sort &amp; Sweep 最適化版）。
    /// RMS値でソート後、近傍ファイルのみを比較することで、計算量を大幅に削減します。
    /// </summary>
    private void CompareGroup(
        IReadOnlyList<int> group,
        float r2Threshold,
        ref int processedCount,
        int totalFiles,
        IProgress<int> progress,
        ref int comparisons,
        ref int matches,
        ref int skipped,
        CancellationToken cancellationToken)
    {
        if (group.Count == 1)
        {
            MarkSelf(group[0], ref processedCount, totalFiles, progress);
            return;
        }

        var entries = CreateSortedEntries(group);
        PerformSortAndSweep(entries, r2Threshold, ref processedCount, totalFiles, progress,
            ref comparisons, ref matches, ref skipped, cancellationToken);
    }

    /// <summary>
    /// 単一ファイルグループの処理（自分自身をマーク）。
    /// </summary>
    private void MarkSelf(int idx, ref int processedCount, int totalFiles, IProgress<int> progress)
    {
        int fileNum = _fileList[idx].NumInteger;
        if (fileNum >= _startPoint && fileNum <= _endPoint)
        {
            Interlocked.CompareExchange(ref _replaceTable[fileNum], fileNum, 0);
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

        bool usePruning = group.Count >= 10;
        int numPivots = Math.Min(3, group.Count);

        int[] pivotIndices = new int[numPivots];
        ICachedSoundData[] pivotData = new ICachedSoundData[numPivots];

        if (usePruning)
        {
            // グループ内から3つのピボットを選定（先頭、中間、末尾）
            pivotIndices[0] = group[0];
            pivotIndices[1] = group[group.Count / 2];
            pivotIndices[2] = group[group.Count - 1];

            for (int k = 0; k < numPivots; k++)
            {
                _audioCache.TryGetValue(_fileList[pivotIndices[k]].Name, out var pd);
                pivotData[k] = pd!;
            }
        }

        for (int i = 0; i < group.Count; i++)
        {
            int idx = group[i];
            _audioCache.TryGetValue(_fileList[idx].Name, out var cachedData);
            float rms = (cachedData == null) ? float.MaxValue : cachedData.TotalRms;

            float[]? distances = null;
            if (usePruning && cachedData != null)
            {
                distances = new float[numPivots];
                for (int k = 0; k < numPivots; k++)
                {
                    var pd = pivotData[k];
                    if (pd == null) continue;

                    if (ReferenceEquals(cachedData, pd))
                    {
                        distances[k] = 0f;
                        continue;
                    }

                    // ピボットとの相関係数（アライメント補正込み）を算出
                    int ch = (cachedData.GetActiveRegions()[0] == null || cachedData.GetActiveRegions()[0].Count == 0) ? 1 : 0;
                    var shorter = cachedData.TotalSamples < pd.TotalSamples ? cachedData : pd;
                    var longer = cachedData.TotalSamples < pd.TotalSamples ? pd : cachedData;

                    int shorterFrames = shorter.TotalSamples / shorter.Channels;
                    int longerFrames = longer.TotalSamples / longer.Channels;
                    var shorterSpan = shorter.GetRawSpan(ch, 0, shorterFrames);
                    var longerFullSpan = longer.GetRawSpan(ch, 0, longerFrames);

                    float r = FastWaveCompare.CalculateMaxCorrelation(shorter, longer, ch, shorterFrames, longerFrames, shorterSpan, longerFullSpan, out _);

                    // 相関 r を距離 d に変換: d = sqrt(2 * max(0, 1 - r))
                    distances[k] = (float)Math.Sqrt(2.0 * Math.Max(0.0, 1.0 - r));
                }
            }

            entries[i] = new AudioEntry(idx, rms, _fileList[idx].NumInteger, distances);
        }
        Array.Sort(entries);
        return entries;
    }

    /// <summary>
    /// Sort &amp; Sweepアルゴリズムで比較を実行します。
    /// RMS値の昇順にソートされたエントリに対して、各エントリと後続の近傍エントリのみを比較します。
    /// </summary>
    private void PerformSortAndSweep(
        AudioEntry[] entries,
        float r2Threshold,
        ref int processedCount,
        int totalFiles,
        IProgress<int> progress,
        ref int comparisons,
        ref int matches,
        ref int skipped,
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

            _audioCache.TryGetValue(_fileList[iIdx].Name, out var cachedData1);
            if (cachedData1 != null)
            {
                CompareWithNearbyEntries(entries, i, cachedData1, r2Threshold, ref comparisons, ref matches, ref skipped);
            }

            Interlocked.Increment(ref processedCount);
            ReportProgress(ref processedCount, totalFiles, progress);
        }
    }

    /// <summary>
    /// 近傍エントリとの比較を行います。
    /// </summary>
    private void CompareWithNearbyEntries(AudioEntry[] entries, int currentIndex, ICachedSoundData cachedData1, float r2Threshold, ref int comparisons, ref int matches, ref int skipped)
    {
        float dThreshold = (float)Math.Sqrt(2.0 * Math.Max(0.0, 1.0 - r2Threshold));
        const float epsilon = 1e-4f;
        var pivotDistances1 = entries[currentIndex].PivotDistances;

        for (int j = currentIndex + 1; j < entries.Length; j++)
        {
            // 三角不等式による枝刈り
            if (pivotDistances1 != null)
            {
                var pivotDistances2 = entries[j].PivotDistances;
                if (pivotDistances2 != null)
                {
                    bool skip = false;
                    for (int k = 0; k < pivotDistances1.Length; k++)
                    {
                        if (Math.Abs(pivotDistances1[k] - pivotDistances2[k]) > dThreshold + epsilon)
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip)
                    {
                        Interlocked.Increment(ref skipped);
                        continue;
                    }
                }
            }

            CompareFilePair(entries[currentIndex].OriginalIndex, entries[j].OriginalIndex, cachedData1, r2Threshold, ref comparisons, ref matches, ref skipped);
        }
    }



    /// <summary>
    /// ファイルペアの波形を詳細に比較し、一致する場合は置換テーブルを更新します。
    /// 比較の前に高速チェック（ファイル名やフィンガープリント）を行い、不要な処理をスキップします。
    /// </summary>
    private void CompareFilePair(int iIdx, int jIdx, ICachedSoundData cachedData1, float r2Threshold, ref int comparisons, ref int matches, ref int skipped)
    {
        _ = _fileList[iIdx].NumInteger;
        int jVal = _fileList[jIdx].NumInteger;

        if (jVal < _startPoint || jVal > _endPoint || _replaceTable[jVal] != 0) return;

        if (_fileList[iIdx].Name.Equals(_fileList[jIdx].Name) ||
            (!string.IsNullOrEmpty(_fileList[iIdx].AudioFingerprint) && _fileList[iIdx].AudioFingerprint.Equals(_fileList[jIdx].AudioFingerprint)))
        {
            UpdateReplaceTable(iIdx, jIdx);
            Interlocked.Increment(ref matches);
            return;
        }

        _audioCache.TryGetValue(_fileList[jIdx].Name, out var cachedData2);
        if (cachedData2 == null) { Interlocked.Increment(ref skipped); return; }

        Interlocked.Increment(ref comparisons);
        bool isMatch = FastWaveCompare.IsMatch(cachedData1, cachedData2, r2Threshold);


        if (isMatch)
        {
            UpdateReplaceTable(iIdx, jIdx);
            Interlocked.Increment(ref matches);
        }
    }

    /// <summary>
    /// 置換テーブルを更新します（Union-Find 方式）。
    /// 経路圧縮により推移的なマッチングを効率的に管理し、CompareExchangeによるCAS操作でスレッドセーフな更新を実現します。
    /// </summary>
    private void UpdateReplaceTable(int i, int j)
    {
        int rootI = FindRoot(_fileList[i].NumInteger);
        int rootJ = FindRoot(_fileList[j].NumInteger);

        if (rootI == rootJ) return;

        long sizeI = rootI >= 0 && rootI < _fileSizes.Length ? _fileSizes[rootI] : 0;
        long sizeJ = rootJ >= 0 && rootJ < _fileSizes.Length ? _fileSizes[rootJ] : 0;

        int newRoot, newChild;
        if (sizeI > sizeJ)
        {
            newRoot = rootI;
            newChild = rootJ;
        }
        else if (sizeJ > sizeI)
        {
            newRoot = rootJ;
            newChild = rootI;
        }
        else
        {
            newRoot = Math.Min(rootI, rootJ);
            newChild = Math.Max(rootI, rootJ);
        }

        Interlocked.CompareExchange(ref _replaceTable[newChild], newRoot, 0);
        Interlocked.CompareExchange(ref _replaceTable[newChild], newRoot, newChild);
    }

    /// <summary>
    /// Union-Findのルート検索を行います。経路圧縮により2回目以降の検索が高速化されます。
    /// </summary>
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
    /// 100ファイルごと、または完了時に進捗を報告し、オーバーヘッドを削減します。
    /// </summary>
    private static void ReportProgress(ref int processedCount, int totalCount, IProgress<int> progress)
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
