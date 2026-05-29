using BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;
using System.Collections.Concurrent;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;

/// <summary>
/// 複数のしきい値で並列シミュレーションを実行するエンジンです。
/// Union-Find方式を用いた高速なユニークファイル数カウントと、グループ単位での並列処理により
/// 計算量を最小化しつつ、最適な削減率のシミュレーションを行います。
/// </summary>
/// <exception cref="ArgumentNullException">fileListがnullの場合。</exception>
[ADRAnchor("M-04", nameof(SimulationEngine))]
internal class SimulationEngine(
    IReadOnlyList<BmsAudioFile> fileList,
    IReadOnlyDictionary<string, ICachedSoundData> audioCache,
    int startPoint,
    int endPoint)
{
    private readonly IReadOnlyList<BmsAudioFile> _fileList = fileList ?? throw new ArgumentNullException(nameof(fileList));
    private readonly int _startPoint = startPoint;
    private readonly int _endPoint = endPoint;
    private readonly IReadOnlyDictionary<string, ICachedSoundData> _audioCache = audioCache ?? throw new ArgumentNullException(nameof(audioCache));
    private readonly int _parallelDegree = Math.Max(1, Environment.ProcessorCount - 1);

    /// <summary>
    /// 並列シミュレーション実行（詳細進捗版）。
    /// </summary>
    /// <param name="rangeMin">しきい値の最小値。</param>
    /// <param name="rangeMax">しきい値の最大値。</param>
    /// <param name="step">しきい値のステップ幅。</param>
    /// <param name="progress">進捗報告用のIProgress（0.0～1.0の範囲）。</param>
    /// <returns>シミュレーション結果のリスト（しきい値降順）。</returns>
    public IReadOnlyList<SimulationPoint> RunParallelSimulationDetailed(
        float rangeMin,
        float rangeMax,
        float step,
        IProgress<double>? progress)
    {
        IReadOnlyList<float> thresholds = GenerateThresholds(rangeMin, rangeMax, step);
        var results = new ConcurrentBag<SimulationPoint>();
        int completed = 0;
        int total = thresholds.Count;

        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), "=== RunParallelSimulationDetailed Start ===");
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Parallel simulation: {total} thresholds, {_parallelDegree} threads");
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Range: {rangeMin:F2} - {rangeMax:F2}, Step: {step:F2}");

        int cachedCount = _audioCache.Count;
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Cached audio files: {cachedCount}/{_fileList.Count}");

        var groups = AudioFileGroupingStrategy.GroupFiles(_audioCache, _fileList, _startPoint, _endPoint, null);

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _parallelDegree };
        var timer = PerformanceDebugLogger.StartTimer();

        Parallel.ForEach(thresholds, parallelOptions, threshold =>
        {
            try
            {
                int fileCount = SimulateThreshold(threshold, groups);
                results.Add(new SimulationPoint(threshold, fileCount));

                int current = System.Threading.Interlocked.Increment(ref completed);

                // 進捗を0.0～1.0の範囲で報告（0.0～0.7は計算、0.7～1.0は統計用）
                double percentage = (double)current / total * 0.7;
                progress?.Report(percentage);
            }
            catch (Exception ex)
            {
                PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"ERROR: Simulation failed at threshold={threshold:F2}: {ex.Message}");
                PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"  StackTrace: {ex.StackTrace}");
            }
        });

        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), "=== RunParallelSimulationDetailed Complete ===");
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Completed {results.Count} simulations in {timer.Lap("RunParallelSimulationDetailed")} ms");

        return [.. results.OrderByDescending(r => r.Threshold)];
    }

    /// <summary>
    /// 各しきい値でシミュレーションを順次実行（しきい値降順）し、Base36またはBase62の制限条件を
    /// 満たした段階で早期終了することで無駄な計算を省きます。
    /// 結果はしきい値降順のリストとして返されます。
    /// </summary>
    /// <param name="rangeMin">しきい値の最小値。</param>
    /// <param name="rangeMax">しきい値の最大値。</param>
    /// <param name="step">しきい値のステップ幅。</param>
    /// <param name="progress">進捗報告用のIProgress。</param>
    /// <returns>シミュレーション結果のリスト（しきい値降順）。</returns>
    public IReadOnlyList<SimulationPoint> RunParallelSimulation(
        float rangeMin,
        float rangeMax,
        float step,
        IProgress<int>? progress)
    {
        IReadOnlyList<float> thresholds = GenerateThresholds(rangeMin, rangeMax, step);
        var results = new List<SimulationPoint>();
        int completed = 0;
        int total = thresholds.Count;
        const int Base36Limit = AppConstants.Definition.MaxNumberBase36;
        const int Base62Limit = AppConstants.Definition.ReplaceTableSize;
        bool base36Found = false;
        bool base62Found = false;
        float base36Threshold = 0f;
        float base62Threshold = 0f;

        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), "=== RunParallelSimulation Start (with early termination) ===");
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Sequential simulation: {total} thresholds max");
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Range: {rangeMin:F2} - {rangeMax:F2}, Step: {step:F2}");
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"File range: {_startPoint} - {_endPoint}");
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Base36 limit: {Base36Limit} files");
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Base62 limit: {Base62Limit} files");

        // 音声キャッシュの確認
        int cachedCount = _audioCache.Count;
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Cached audio files: {cachedCount}/{_fileList.Count}");

        if (cachedCount == 0)
        {
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), "CRITICAL ERROR: No cached audio data! All simulations will return original count.");
        }

        var timer = PerformanceDebugLogger.StartTimer();

        // グループ分けを事前に1回だけ計算
        var timerGroup = PerformanceDebugLogger.StartTimer();
        var groups = AudioFileGroupingStrategy.GroupFiles(_audioCache, _fileList, _startPoint, _endPoint, null);
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"AudioFileGroupingStrategy.GroupFiles: {timerGroup.Lap("AudioFileGroupingStrategy.GroupFiles")} ms");

        // 順次実行（しきい値降順）
        foreach (var threshold in thresholds)
        {
            try
            {
                int fileCount = SimulateThreshold(threshold, groups);
                results.Add(new SimulationPoint(threshold, fileCount));

                completed++;

                // Base62条件チェック
                if (!base62Found && fileCount <= Base62Limit)
                {
                    base62Found = true;
                    base62Threshold = threshold;
                    PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"=== Base62 condition met at threshold={threshold:F2} ===");
                    PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"File count: {fileCount} <= {Base62Limit}");
                }

                // Base36条件チェック
                if (!base36Found && fileCount <= Base36Limit)
                {
                    base36Found = true;
                    base36Threshold = threshold;
                    PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"=== Base36 condition met at threshold={threshold:F2} ===");
                    PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"File count: {fileCount} <= {Base36Limit}");
                    PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Skipping remaining {total - completed} simulations");
                    break;
                }

                if (completed % 10 == 0)
                {
                    int percentage = (int)((float)completed / total * 70);
                    progress?.Report(percentage);
                }
            }
            catch (Exception ex)
            {
                PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"ERROR: Simulation failed at threshold={threshold:F2}: {ex.Message}");
                PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"  StackTrace: {ex.StackTrace}");
            }
        }

        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), "=== RunParallelSimulation Complete ===");
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Completed {results.Count}/{total} simulations in {timer.Lap("RunParallelSimulation")} ms");
        PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Saved {total - completed} simulations due to early termination");

        // Base36/Base62の結果を報告
        if (base62Found)
        {
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Base62 threshold: {base62Threshold:F2}");
        }
        else
        {
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), "Base62 condition not met in simulation range");
        }

        if (base36Found)
        {
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"Base36 threshold: {base36Threshold:F2}");
        }
        else
        {
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), "Base36 condition not met in simulation range");
        }

        // 結果の統計
        if (results.Count > 0)
        {
            var minFiles = results.Min(static r => r.FileCount);
            var maxFiles = results.Max(static r => r.FileCount);
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"File count range: {minFiles} - {maxFiles}");

            if (minFiles == maxFiles)
            {
                PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), "WARNING: All simulations returned the same file count - no reduction detected!");
            }
        }

        // 進捗を70%に設定（完了）
        progress?.Report(70);

        return results;
    }

    /// <summary>
    /// 単一しきい値でのシミュレーションを実行します。
    /// ファイルをグループ化して並列処理し、Union-Findで置換テーブルを構築後、
    /// 自分自身がルートであるファイル（ユニークファイル）の数をカウントします。
    /// </summary>
    /// <param name="threshold">相関係数しきい値。</param>
    /// <param name="groups">グループ化されたファイルインデックス。</param>
    /// <returns>ユニークファイル数。</returns>
    private int SimulateThreshold(float threshold, IReadOnlyList<IReadOnlyList<int>> groups)
    {
        var uf = new UnionFind(AppConstants.Definition.ReplaceTableSize); // BMSの最大定義番号

        int totalComparisons = 0;
        int totalMatches = 0;

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _parallelDegree };

        Parallel.ForEach(groups, parallelOptions, group =>
        {
            int groupComparisons = 0;
            int groupMatches = 0;
            ProcessGroup(group, uf, threshold, ref groupComparisons, ref groupMatches);

            System.Threading.Interlocked.Add(ref totalComparisons, groupComparisons);
            System.Threading.Interlocked.Add(ref totalMatches, groupMatches);
        });

        // Union-Findで代表値を辿ってユニークファイル数をカウント
        int uniqueCount = 0;
        int totalInRange = 0;
        int notProcessed = 0;

        foreach (BmsAudioFile file in _fileList)
        {
            int fileNum = file.NumInteger;
            if (fileNum >= _startPoint && fileNum <= _endPoint)
            {
                totalInRange++;

                // 代表値（ルート）を見つける
                int root = uf.Find(fileNum);

                // 自分がルートならユニークファイル
                if (root == fileNum)
                {
                    uniqueCount++;
                }
                else if (root == 0)
                {
                    notProcessed++;
                }
            }
        }

        // 詳細ログ（しきい値0.23の場合）
        if (Math.Abs(threshold - 0.23f) < 0.005f)
        {
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"=== Simulation Threshold {threshold:F2} Detail ===");
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"  Total in range: {totalInRange}");
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"  Unique (self-ref): {uniqueCount}");
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"  Not processed (==0): {notProcessed}");
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"  Total comparisons: {totalComparisons}");
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"  Total matches: {totalMatches}");
        }

        // 最初の数回のシミュレーションで詳細ログ
        if (threshold >= 0.98f || threshold <= 0.07f || Math.Abs(threshold - 0.50f) < 0.01f)
        {
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"  Threshold {threshold:F2}: Groups={groups.Count}, Comparisons={totalComparisons}, Matches={totalMatches}, Unique={uniqueCount}");
        }

        return uniqueCount;
    }

    /// <summary>
    /// グループ比較（Union-Find方式・スレッドセーフ）。
    /// </summary>
    private void ProcessGroup(
        IReadOnlyList<int> group,
        UnionFind uf,
        float threshold,
        ref int comparisons,
        ref int matches)
    {
        if (group == null || group.Count == 0) return;

        // 単一ファイルのグループでも自分自身を登録
        if (group.Count == 1)
        {
            int idx = group[0];
            int fileNum = _fileList[idx].NumInteger;

            if (fileNum >= _startPoint && fileNum <= _endPoint)
            {
                uf.TryMarkSelf(fileNum);
            }
            return;
        }

        List<(int OriginalIndex, float Rms)> entries = CreateSortedEntries(group);
        int n = entries.Count;

        for (int i = 0; i < n; i++)
        {
            int iIdx = entries[i].OriginalIndex;
            int iVal = _fileList[iIdx].NumInteger;

            if (iVal < _startPoint || iVal > _endPoint) continue;

            // 自分自身をマーク
            if (!uf.TryMarkSelf(iVal))
                continue;

            float rms1 = entries[i].Rms;
            (float min, float max) = CalculateRmsRange(rms1);

            for (int j = i + 1; j < n; j++)
            {
                float rms2 = entries[j].Rms;

                // Early break: sorted by RMS
                if (rms2 > max) break;

                int jIdx = entries[j].OriginalIndex;
                int jVal = _fileList[jIdx].NumInteger;

                if (jVal < _startPoint || jVal > _endPoint) continue;
                if (uf.IsMapped(jVal)) continue;

                // Fast path checking (Name & Fingerprint)
                if (TryFastPathMatch(iIdx, jIdx, uf, iVal, jVal, ref matches))
                {
                    continue;
                }

                // RMS range check
                if (rms2 < min || rms2 > max)
                    continue;

                // Actual audio comparison
                CompareAndMergeAudio(iIdx, jIdx, iVal, jVal, threshold, uf, ref comparisons, ref matches);
            }
        }
    }

    private bool TryFastPathMatch(
        int iIdx,
        int jIdx,
        UnionFind uf,
        int iVal,
        int jVal,
        ref int matches)
    {
        // Fast path: exact name match
        if (_fileList[iIdx].Name.Equals(_fileList[jIdx].Name))
        {
            if (uf.TryLink(jVal, iVal))
            {
                System.Threading.Interlocked.Increment(ref matches);
            }
            return true;
        }

        // Fast path: fingerprint match
        if (!string.IsNullOrEmpty(_fileList[iIdx].AudioFingerprint) &&
            _fileList[iIdx].AudioFingerprint.Equals(_fileList[jIdx].AudioFingerprint))
        {
            if (uf.TryLink(jVal, iVal))
            {
                System.Threading.Interlocked.Increment(ref matches);
            }
            return true;
        }

        return false;
    }

    private void CompareAndMergeAudio(
        int iIdx,
        int jIdx,
        int iVal,
        int jVal,
        float threshold,
        UnionFind uf,
        ref int comparisons,
        ref int matches)
    {
        try
        {
            _audioCache.TryGetValue(_fileList[iIdx].Name, out var cachedData1);
            _audioCache.TryGetValue(_fileList[jIdx].Name, out var cachedData2);

            if (cachedData1 != null && cachedData2 != null)
            {
                System.Threading.Interlocked.Increment(ref comparisons);

                bool isMatch = FastWaveCompare.IsMatch(
                    cachedData1,
                    cachedData2,
                    threshold);

                if (isMatch)
                {
                    // Union-Find: 統合
                    uf.Union(iVal, jVal);
                    System.Threading.Interlocked.Increment(ref matches);
                }
            }
        }
        catch (Exception ex)
        {
            PerformanceDebugLogger.WriteDebug(nameof(SimulationEngine), $"ERROR: Audio comparison failed [{iIdx}] vs [{jIdx}]: {ex.Message}");
        }
    }

    /// <summary>
    /// グループをRMS順にソート。
    /// </summary>
    private List<(int OriginalIndex, float Rms)> CreateSortedEntries(IReadOnlyList<int> group)
    {
        var entries = new List<(int OriginalIndex, float Rms)>(group.Count);

        foreach (var idx in group)
        {
            _audioCache.TryGetValue(_fileList[idx].Name, out var cachedData);
            if (cachedData != null)
            {
                entries.Add((idx, cachedData.TotalRms));
            }
        }

        entries.Sort(static (a, b) => a.Rms.CompareTo(b.Rms));
        return entries;
    }

    /// <summary>
    /// RMS比較範囲の計算。
    /// </summary>
    internal static (float min, float max) CalculateRmsRange(float rms)
    {
        if (rms < AppConstants.AudioComparison.SilenceRmsThreshold)
        {
            return (0.0f, AppConstants.AudioComparison.SilenceRmsUpperBound);
        }

        return (rms * AppConstants.AudioComparison.RmsLowerBoundRatio, rms * AppConstants.AudioComparison.RmsUpperBoundRatio);
    }

    /// <summary>
    /// しきい値リストの生成。
    /// </summary>
    internal static IReadOnlyList<float> GenerateThresholds(float min, float max, float step)
    {
        var thresholds = new List<float>();

        for (float r2 = max; r2 >= min; r2 -= step)
        {
            thresholds.Add((float)Math.Round(r2, 2));
        }

        return thresholds;
    }

    /// <summary>
    /// オーディオエントリ（軽量構造体）。
    /// </summary>
    private readonly struct AudioEntry(int index, float rms)
    {
        public readonly int OriginalIndex = index;
        public readonly float Rms = rms;
    }
}
