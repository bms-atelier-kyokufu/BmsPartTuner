using System.Collections.Concurrent;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;

/// <summary>
/// 高速並列シミュレーションエンジン。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>複数のしきい値で並列シミュレーションを実行</item>
/// <item>Union-Find方式による高速なユニークファイル数カウント</item>
/// <item>グループ単位の並列処理で$O(N^2)$を最小化</item>
/// </list>
///
/// <para>【並列化戦略】</para>
/// <list type="number">
/// <item>しきい値レベル: 各しきい値を並列実行（Parallel.ForEach）</item>
/// <item>グループレベル: 各グループを並列実行（Parallel.ForEach）</item>
/// <item>最大並列度: CPUコア数 - 1（システムリソース確保のため）</item>
/// </list>
///
/// <para>【Union-Findアルゴリズム】</para>
/// 推移的なマッチング関係を効率的に管理:
/// A=B, B=C → A=C（自動的に統合）
/// 計算量: $O(\alpha(n))$（逆アッカーマン関数、実質定数時間）
///
/// <para>【スレッドセーフ設計】</para>
/// <list type="bullet">
/// <item>Interlocked.CompareExchange: CAS操作による排他制御</item>
/// <item>ConcurrentBag: スレッドセーフな結果収集</item>
/// <item>非再帰版FindRoot: スタックオーバーフロー防止</item>
/// </list>
/// </remarks>
/// <remarks>
/// SimulationEngineを初期化。
/// </remarks>
/// <exception cref="ArgumentNullException">fileListがnullの場合。</exception>
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

        PerformanceDebugLogger.WriteLine("=== RunParallelSimulationDetailed Start ===");
        PerformanceDebugLogger.WriteLine($"Parallel simulation: {total} thresholds, {_parallelDegree} threads");
        PerformanceDebugLogger.WriteLine($"Range: {rangeMin:F2} - {rangeMax:F2}, Step: {step:F2}");

        int cachedCount = _audioCache.Count;
        PerformanceDebugLogger.WriteLine($"Cached audio files: {cachedCount}/{_fileList.Count}");

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
                PerformanceDebugLogger.WriteLine($"ERROR: Simulation failed at threshold={threshold:F2}: {ex.Message}");
                PerformanceDebugLogger.WriteLine($"  StackTrace: {ex.StackTrace}");
            }
        });

        PerformanceDebugLogger.WriteLine("=== RunParallelSimulationDetailed Complete ===");
        PerformanceDebugLogger.WriteLine($"Completed {results.Count} simulations in {timer.Lap("RunParallelSimulationDetailed")} ms");

        return [.. results.OrderByDescending(r => r.Threshold)];
    }

    /// <summary>
    /// 並列シミュレーション実行。
    /// </summary>
    /// <param name="rangeMin">しきい値の最小値。</param>
    /// <param name="rangeMax">しきい値の最大値。</param>
    /// <param name="step">しきい値のステップ幅。</param>
    /// <param name="progress">進捗報告用のIProgress。</param>
    /// <returns>シミュレーション結果のリスト（しきい値降順）。</returns>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>しきい値リストを生成（0.05～0.99を0.01刻み）</item>
    /// <item>各しきい値でシミュレーション（順次実行、しきい値降順）</item>
    /// <item>結果をリストに収集</item>
    /// <item>Base36/Base62の両方の条件を同時監視</item>
    /// <item>Base36条件を満たしたら早期終了</item>
    /// </list>
    ///
    /// <para>【早期終了戦略】</para>
    /// Base36制限（1295件）とBase62制限（3844件）を同時に監視し、
    /// それぞれの条件を満たした最初のしきい値を記録します。
    /// Base36条件を満たしたら（より厳しい条件）、シミュレーションを終了します。
    ///
    /// <para>【音声キャッシュの検証】</para>
    /// キャッシュが0件の場合、すべてのシミュレーションで削減率0%となるため、
    /// 開始時にキャッシュ状態をログ出力して問題を早期発見します。
    ///
    /// <para>【進捗報告】</para>
    /// 10回ごとまたは完了時に進捗を報告（0-70%の範囲）。
    /// 残り30%はデータ平滑化とエルボーポイント検出に割り当てられます。
    /// </remarks>
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

        PerformanceDebugLogger.WriteLine("=== RunParallelSimulation Start (with early termination) ===");
        PerformanceDebugLogger.WriteLine($"Sequential simulation: {total} thresholds max");
        PerformanceDebugLogger.WriteLine($"Range: {rangeMin:F2} - {rangeMax:F2}, Step: {step:F2}");
        PerformanceDebugLogger.WriteLine($"File range: {_startPoint} - {_endPoint}");
        PerformanceDebugLogger.WriteLine($"Base36 limit: {Base36Limit} files");
        PerformanceDebugLogger.WriteLine($"Base62 limit: {Base62Limit} files");

        // 音声キャッシュの確認
        int cachedCount = _audioCache.Count;
        PerformanceDebugLogger.WriteLine($"Cached audio files: {cachedCount}/{_fileList.Count}");

        if (cachedCount == 0)
        {
            PerformanceDebugLogger.WriteLine("CRITICAL ERROR: No cached audio data! All simulations will return original count.");
        }

        var timer = PerformanceDebugLogger.StartTimer();

        // グループ分けを事前に1回だけ計算
        var timerGroup = PerformanceDebugLogger.StartTimer();
        var groups = AudioFileGroupingStrategy.GroupFiles(_audioCache, _fileList, _startPoint, _endPoint, null);
        PerformanceDebugLogger.WriteLine($"[RunParallelSimulation] AudioFileGroupingStrategy.GroupFiles: {timerGroup.Lap("AudioFileGroupingStrategy.GroupFiles")} ms");

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
                    PerformanceDebugLogger.WriteLine($"=== Base62 condition met at threshold={threshold:F2} ===");
                    PerformanceDebugLogger.WriteLine($"File count: {fileCount} <= {Base62Limit}");
                }

                // Base36条件チェック
                if (!base36Found && fileCount <= Base36Limit)
                {
                    base36Found = true;
                    base36Threshold = threshold;
                    PerformanceDebugLogger.WriteLine($"=== Base36 condition met at threshold={threshold:F2} ===");
                    PerformanceDebugLogger.WriteLine($"File count: {fileCount} <= {Base36Limit}");
                    PerformanceDebugLogger.WriteLine($"Skipping remaining {total - completed} simulations");
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
                PerformanceDebugLogger.WriteLine($"ERROR: Simulation failed at threshold={threshold:F2}: {ex.Message}");
                PerformanceDebugLogger.WriteLine($"  StackTrace: {ex.StackTrace}");
            }
        }

        PerformanceDebugLogger.WriteLine("=== RunParallelSimulation Complete ===");
        PerformanceDebugLogger.WriteLine($"Completed {results.Count}/{total} simulations in {timer.Lap("RunParallelSimulation")} ms");
        PerformanceDebugLogger.WriteLine($"Saved {total - completed} simulations due to early termination");

        // Base36/Base62の結果を報告
        if (base62Found)
        {
            PerformanceDebugLogger.WriteLine($"Base62 threshold: {base62Threshold:F2}");
        }
        else
        {
            PerformanceDebugLogger.WriteLine("Base62 condition not met in simulation range");
        }

        if (base36Found)
        {
            PerformanceDebugLogger.WriteLine($"Base36 threshold: {base36Threshold:F2}");
        }
        else
        {
            PerformanceDebugLogger.WriteLine("Base36 condition not met in simulation range");
        }

        // 結果の統計
        if (results.Count > 0)
        {
            var minFiles = results.Min(static r => r.FileCount);
            var maxFiles = results.Max(static r => r.FileCount);
            PerformanceDebugLogger.WriteLine($"File count range: {minFiles} - {maxFiles}");

            if (minFiles == maxFiles)
            {
                PerformanceDebugLogger.WriteLine("WARNING: All simulations returned the same file count - no reduction detected!");
            }
        }

        // 進捗を70%に設定（完了）
        progress?.Report(70);

        return results;
    }

    /// <summary>
    /// 単一しきい値でのシミュレーション。
    /// </summary>
    /// <param name="threshold">相関係数しきい値。</param>
    /// <returns>ユニークファイル数。</returns>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>ファイルをグループ化（<see cref="AudioFileGroupingStrategy"/>）</item>
    /// <item>各グループを並列処理（Parallel.ForEach）</item>
    /// <item>Union-Findで置換テーブルを構築</item>
    /// <item>ルートが自分自身のファイルをカウント（ユニーク数）</item>
    /// </list>
    ///
    /// <para>【Union-Findによるカウント】</para>
    /// 置換テーブルで代表値（ルート）を辿り、自分自身がルートのファイルのみをカウント。
    /// これにより、推移的な統合を考慮した正確なユニーク数を取得できます。
    /// </remarks>
    private int SimulateThreshold(float threshold, IReadOnlyList<IReadOnlyList<int>> groups)
    {
        var uf = new UnionFind(AppConstants.Definition.ReplaceTableSize); // BMSの最大定義番号
        int[] replaceTable = uf.GetRawTable();

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
            PerformanceDebugLogger.WriteLine($"=== Simulation Threshold {threshold:F2} Detail ===");
            PerformanceDebugLogger.WriteLine($"  Total in range: {totalInRange}");
            PerformanceDebugLogger.WriteLine($"  Unique (self-ref): {uniqueCount}");
            PerformanceDebugLogger.WriteLine($"  Not processed (==0): {notProcessed}");
            PerformanceDebugLogger.WriteLine($"  Total comparisons: {totalComparisons}");
            PerformanceDebugLogger.WriteLine($"  Total matches: {totalMatches}");
        }

        // 最初の数回のシミュレーションで詳細ログ
        if (threshold >= 0.98f || threshold <= 0.07f || Math.Abs(threshold - 0.50f) < 0.01f)
        {
            PerformanceDebugLogger.WriteLine($"  Threshold {threshold:F2}: Groups={groups.Count}, Comparisons={totalComparisons}, Matches={totalMatches}, Unique={uniqueCount}");
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

        int[] replaceTable = uf.GetRawTable();

        // 単一ファイルのグループでも自分自身を登録
        if (group.Count == 1)
        {
            int idx = group[0];
            int fileNum = _fileList[idx].NumInteger;

            if (fileNum >= _startPoint && fileNum <= _endPoint)
            {
                System.Threading.Interlocked.CompareExchange(ref replaceTable[fileNum], fileNum, 0);
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
            if (System.Threading.Interlocked.CompareExchange(ref replaceTable[iVal], iVal, 0) != 0)
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
                if (replaceTable[jVal] != 0) continue;

                // Fast path checking (Name & Fingerprint)
                if (TryFastPathMatch(iIdx, jIdx, replaceTable, iVal, jVal, ref matches))
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
        int[] replaceTable,
        int iVal,
        int jVal,
        ref int matches)
    {
        // Fast path: exact name match
        if (_fileList[iIdx].Name.Equals(_fileList[jIdx].Name))
        {
            if (System.Threading.Interlocked.CompareExchange(ref replaceTable[jVal], iVal, 0) == 0)
            {
                System.Threading.Interlocked.Increment(ref matches);
            }
            return true;
        }

        // Fast path: fingerprint match
        if (!string.IsNullOrEmpty(_fileList[iIdx].AudioFingerprint) &&
            _fileList[iIdx].AudioFingerprint.Equals(_fileList[jIdx].AudioFingerprint))
        {
            if (System.Threading.Interlocked.CompareExchange(ref replaceTable[jVal], iVal, 0) == 0)
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
            PerformanceDebugLogger.WriteLine($"ERROR: Audio comparison failed [{iIdx}] vs [{jIdx}]: {ex.Message}");
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
