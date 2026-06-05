namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Comparison;
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
[ADRAnchor("OPT-11", nameof(ParallelAudioComparisonEngine))]
[ADRAnchor("OPT-01", nameof(ParallelAudioComparisonEngine))]
internal class ParallelAudioComparisonEngine(AudioComparisonParameters parameters)
{
    private static readonly Logger<ParallelAudioComparisonEngine> s_logger = new();
    #region 定数定義

    /// <summary>進捗レポートの範囲（Phase 2 の幅）。</summary>
    private const int ProgressPhase2Range = AppConstants.Progress.ComparisonComplete - AppConstants.Progress.PreloadComplete;

    #endregion

    #region フィールド

    private readonly IReadOnlyList<BmsAudioFile> _fileList
        = parameters.FileList ?? throw new ArgumentNullException(nameof(parameters.FileList));
    private readonly int _startPoint = parameters.StartPoint;
    private readonly int _endPoint = parameters.EndPoint;
    private readonly IReadOnlyDictionary<string, ICachedSoundData> _audioCache
        = parameters.AudioCache ?? throw new ArgumentNullException(nameof(parameters.AudioCache));
    private readonly ThreadSafeReplaceTable _tableManager = new(parameters.ReplaceTable, BuildFileSizeArray(parameters.FileList));

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
    private readonly struct AudioEntry : IComparable<AudioEntry>
    {
        public readonly int OriginalIndex;
        public readonly float Rms;
        public readonly int FileNum;
        public readonly float Dist0;
        public readonly float Dist1;
        public readonly float Dist2;
        public readonly bool HasDistances;

        public AudioEntry(int index, float rms, int fileNum)
        {
            OriginalIndex = index;
            Rms = rms;
            FileNum = fileNum;
            Dist0 = 0f;
            Dist1 = 0f;
            Dist2 = 0f;
            HasDistances = false;
        }

        public AudioEntry(int index, float rms, int fileNum, float dist0, float dist1, float dist2)
        {
            OriginalIndex = index;
            Rms = rms;
            FileNum = fileNum;
            Dist0 = dist0;
            Dist1 = dist1;
            Dist2 = dist2;
            HasDistances = true;
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
    /// グループ単位の音声ファイル比較を行います。
    /// 各グループを並列処理（Parallel.ForEach）し、グループ内ではSort &amp; Sweepを用いて効率的に比較します。
    /// スレッドセーフに置換テーブルを更新します。
    /// </summary>
    /// <param name="groups">ファイルインデックスのグループリスト。</param>
    /// <param name="r2Threshold">相関係数しきい値。</param>
    public void CompareGroups(
        IReadOnlyList<IReadOnlyList<int>> groups,
        float r2Threshold,
        IOperationContext? opContext = null)
    {
        int totalFiles = groups.Sum(g => g.Count);
        int totalComparisons = 0;
        int totalMatches = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = opContext?.CancellationToken ?? CancellationToken.None
        };

        var context = new ComparisonContext(r2Threshold, totalFiles, opContext);
        var timer = s_logger.StartTimer();

        try
        {
            Parallel.ForEach(groups, parallelOptions, (group) =>
            {
                // check for cancellation at start of group processing
                parallelOptions.CancellationToken.ThrowIfCancellationRequested();

                var stats = new GroupComparisonStats();

                CompareGroup(group, context, ref stats);

                Interlocked.Add(ref totalComparisons, stats.Comparisons);
                Interlocked.Add(ref totalMatches, stats.Matches);
            });
        }
        catch (OperationCanceledException)
        {
            s_logger.WriteDebug("=== CompareGroups Cancelled ===");
            throw;
        }

        s_logger.WriteDebug($"=== CompareGroups Complete: {totalComparisons} comparisons, {timer.Lap("CompareGroups")}ms ===");
    }

    #endregion

    #region プライベートメソッド

    /// <summary>
    /// 単一グループの比較処理（Sort &amp; Sweep 最適化版）。
    /// RMS値でソート後、近傍ファイルのみを比較することで、計算量を大幅に削減します。
    /// </summary>
    private void CompareGroup(
        IReadOnlyList<int> group,
        ComparisonContext context,
        ref GroupComparisonStats stats)
    {
        if (group.Count == 1)
        {
            MarkSelf(group[0], context);
            return;
        }

        var entries = CreateSortedEntries(group);
        PerformSortAndSweep(entries, context, ref stats);
    }

    /// <summary>
    /// 単一ファイルグループの処理（自分自身をマーク）。
    /// </summary>
    private void MarkSelf(int idx, ComparisonContext context)
    {
        int fileNum = _fileList[idx].NumInteger;
        if (fileNum >= _startPoint && fileNum <= _endPoint)
        {
            _tableManager.MarkSelf(fileNum);
        }
        context.IncrementProcessedCount();
    }

    /// <summary>
    /// グループ内のファイルをRMS値でソートしたエントリ配列を作成。
    /// </summary>
    private AudioEntry[] CreateSortedEntries(IReadOnlyList<int> group)
    {
        var entries = new AudioEntry[group.Count];
        bool usePruning = group.Count >= 10;

        if (usePruning)
        {
            // グループ内から3つのピボットを選定（先頭、中間、末尾）
            int pivotIdx0 = group[0];
            int pivotIdx1 = group[group.Count / 2];
            int pivotIdx2 = group[group.Count - 1];

            _audioCache.TryGetValue(_fileList[pivotIdx0].Name, out var pd0);
            _audioCache.TryGetValue(_fileList[pivotIdx1].Name, out var pd1);
            _audioCache.TryGetValue(_fileList[pivotIdx2].Name, out var pd2);

            for (int i = 0; i < group.Count; i++)
            {
                int idx = group[i];
                _audioCache.TryGetValue(_fileList[idx].Name, out var cachedData);
                float rms = (cachedData == null) ? float.MaxValue : cachedData.TotalRms;

                if (cachedData != null && pd0 != null && pd1 != null && pd2 != null)
                {
                    float d0 = CalculateDistance(cachedData, pd0);
                    float d1 = CalculateDistance(cachedData, pd1);
                    float d2 = CalculateDistance(cachedData, pd2);
                    entries[i] = new AudioEntry(idx, rms, _fileList[idx].NumInteger, d0, d1, d2);
                }
                else
                {
                    entries[i] = new AudioEntry(idx, rms, _fileList[idx].NumInteger);
                }
            }
        }
        else
        {
            for (int i = 0; i < group.Count; i++)
            {
                int idx = group[i];
                _audioCache.TryGetValue(_fileList[idx].Name, out var cachedData);
                float rms = (cachedData == null) ? float.MaxValue : cachedData.TotalRms;
                entries[i] = new AudioEntry(idx, rms, _fileList[idx].NumInteger);
            }
        }

        Array.Sort(entries);
        return entries;
    }

    /// <summary>
    /// キャッシュされた音声データとピボット音声データとの間の距離を計算します。
    /// </summary>
    private static float CalculateDistance(ICachedSoundData cachedData, ICachedSoundData pd)
    {
        if (ReferenceEquals(cachedData, pd))
        {
            return 0f;
        }

        // ピボットとの相関係数（アライメント補正込み）を算出
        int ch = (cachedData.GetActiveRegions()[0] == null || cachedData.GetActiveRegions()[0].Count == 0) ? 1 : 0;
        var shorter = cachedData.TotalSamples < pd.TotalSamples ? cachedData : pd;
        var longer = cachedData.TotalSamples < pd.TotalSamples ? pd : cachedData;

        int shorterFrames = shorter.TotalSamples / shorter.Channels;
        int longerFrames = longer.TotalSamples / longer.Channels;
        var shorterSpan = shorter.GetRawSpan(ch, 0, shorterFrames);
        var longerFullSpan = longer.GetRawSpan(ch, 0, longerFrames);

        var parameters = new WaveComparisonParameters(shorter, longer, ch, shorterFrames, longerFrames, shorterSpan, longerFullSpan);
        float r = FastWaveCompare.CalculateMaxCorrelation(parameters).Correlation;

        // 相関 r を距離 d に変換: d = sqrt(2 * max(0, 1 - r))
        return (float)Math.Sqrt(2.0 * Math.Max(0.0, 1.0 - r));
    }

    /// <summary>
    /// Sort &amp; Sweepアルゴリズムで比較を実行します（対角線走査・Diagonal Sweep）。
    /// 近いエントリから優先的に比較することで、推移律（Union-FindとAnti-Set）の恩恵を最大化します。
    /// </summary>
    private void PerformSortAndSweep(
        AudioEntry[] entries,
        ComparisonContext context,
        ref GroupComparisonStats stats)
    {
        float dThreshold = (float)Math.Sqrt(2.0 * Math.Max(0.0, 1.0 - context.R2Threshold));
        const float epsilon = 1e-4f;

        // 自分自身のエントリに対するマークおよび進捗報告（直列実行の初期化）
        for (int i = 0; i < entries.Length; i++)
        {
            int iVal = _fileList[entries[i].OriginalIndex].NumInteger;
            if (iVal >= _startPoint && iVal <= _endPoint)
            {
                _tableManager.MarkSelf(iVal);
            }
            context.IncrementProcessedCount();
        }

        // 範囲内（startPoint 〜 endPoint）のエントリのみを事前抽出してループ対象を削減（メモリアロケーション回避のため ArrayPool を使用）
        var inRangeEntries = ArrayPool<AudioEntry>.Shared.Rent(entries.Length);
        int inRangeCount = 0;

        try
        {
            for (int i = 0; i < entries.Length; i++)
            {
                int val = _fileList[entries[i].OriginalIndex].NumInteger;
                if (val >= _startPoint && val <= _endPoint)
                {
                    inRangeEntries[inRangeCount++] = entries[i];
                }
            }

            // 対角線走査: 距離 d (1 から N-1 まで)
            for (int d = 1; d < inRangeCount; d++)
            {
                context.OperationContext?.ThrowIfCancellationRequested();

                for (int i = 0; i < inRangeCount - d; i++)
                {
                    int j = i + d;

                    int iIdx = inRangeEntries[i].OriginalIndex;
                    int jIdx = inRangeEntries[j].OriginalIndex;

                    int jVal = _fileList[jIdx].NumInteger;
                    if (_tableManager.IsMapped(jVal)) continue;

                    // 枝刈り (三角不等式)
                    if (inRangeEntries[i].HasDistances && inRangeEntries[j].HasDistances)
                    {
                        float limit = dThreshold + epsilon;
                        if (Math.Abs(inRangeEntries[i].Dist0 - inRangeEntries[j].Dist0) > limit ||
                            Math.Abs(inRangeEntries[i].Dist1 - inRangeEntries[j].Dist1) > limit ||
                            Math.Abs(inRangeEntries[i].Dist2 - inRangeEntries[j].Dist2) > limit)
                        {
                            stats.Skipped++;
                            continue;
                        }
                    }

                    CompareFilePair(iIdx, jIdx, context, ref stats);
                }
            }
        }
        finally
        {
            System.Buffers.ArrayPool<AudioEntry>.Shared.Return(inRangeEntries);
        }
    }

    /// <summary>
    /// ファイルペアの波形を詳細に比較し、一致する場合は置換テーブルを更新します。
    /// 比較の前に高速チェック（Anti-Set、ファイル名、フィンガープリント）を行い、不要な処理をスキップします。
    /// </summary>
    private void CompareFilePair(int iIdx, int jIdx, ComparisonContext context, ref GroupComparisonStats stats)
    {
        int iVal = _fileList[iIdx].NumInteger;
        int jVal = _fileList[jIdx].NumInteger;

        if (_tableManager.IsMapped(jVal)) return;

        // Anti-Set check
        int rootI = _tableManager.FindRead(iVal);
        int rootJ = _tableManager.FindRead(jVal);

        if (rootI == rootJ) return;
        if (_tableManager.IsKnownMismatch(rootI, rootJ))
        {
            stats.Skipped++;
            return;
        }

        if (_fileList[iIdx].Name.Equals(_fileList[jIdx].Name) ||
            (!string.IsNullOrEmpty(_fileList[iIdx].AudioFingerprint) && _fileList[iIdx].AudioFingerprint.Equals(_fileList[jIdx].AudioFingerprint)))
        {
            _tableManager.UpdateReplaceTable(iVal, jVal);
            stats.Matches++;
            return;
        }

        _audioCache.TryGetValue(_fileList[iIdx].Name, out var cachedData1);
        _audioCache.TryGetValue(_fileList[jIdx].Name, out var cachedData2);
        if (cachedData1 == null || cachedData2 == null) { stats.Skipped++; return; }

        stats.Comparisons++;
        bool isMatch = FastWaveCompare.IsMatch(cachedData1, cachedData2, context.R2Threshold);

        if (isMatch)
        {
            _tableManager.UpdateReplaceTable(iVal, jVal);
            stats.Matches++;
        }
        else
        {
            rootI = _tableManager.FindRead(iVal);
            rootJ = _tableManager.FindRead(jVal);
            _tableManager.MarkAsMismatch(rootI, rootJ);
        }
    }

    #endregion

    #region パラメーターオブジェクト定義

    /// <summary>
    /// 全体的な比較処理のパラメーターと進捗状況を保持するコンテキストクラスです。
    /// </summary>
    private class ComparisonContext(
        float r2Threshold,
        int totalFiles,
        BmsAtelierKyokufu.BmsPartTuner.Core.Context.IOperationContext? opContext)
    {
        private int _processedCount;

        public float R2Threshold { get; } = r2Threshold;
        public int TotalFiles { get; } = totalFiles;
        public BmsAtelierKyokufu.BmsPartTuner.Core.Context.IOperationContext? OperationContext { get; } = opContext;

        /// <summary>
        /// 処理済みファイル数を thread-safe にインクリメントし、必要に応じて進捗状況を報告します。
        /// </summary>
        public void IncrementProcessedCount()
        {
            int current = Interlocked.Increment(ref _processedCount);
            if (current % 100 == 0 || current == TotalFiles)
            {
                int percentage = AppConstants.Progress.PreloadComplete + (int)((float)current / TotalFiles * ProgressPhase2Range);
                OperationContext?.ReportProgress(percentage);
            }
        }
    }

    /// <summary>
    /// 単一のグループ内での比較統計情報を保持するオブジェクトです。
    /// </summary>
    private ref struct GroupComparisonStats
    {
        public int Comparisons { get; set; }
        public int Matches { get; set; }
        public int Skipped { get; set; }
    }

    #endregion
}


