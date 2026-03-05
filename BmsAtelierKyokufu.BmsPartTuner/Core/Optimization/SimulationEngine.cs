using System.Collections.Concurrent;
using System.Diagnostics;
using BmsAtelierKyokufu.BmsPartTuner.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

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
internal class SimulationEngine
{
    private readonly IReadOnlyList<WavFiles> _fileList;
    private readonly int _startPoint;
    private readonly int _endPoint;
    private readonly int _parallelDegree;

    /// <summary>
    /// SimulationEngineを初期化。
    /// </summary>
    /// <param name="fileList">ファイルリスト。</param>
    /// <param name="startPoint">開始位置。</param>
    /// <param name="endPoint">終了位置。</param>
    /// <exception cref="ArgumentNullException">fileListがnullの場合。</exception>
    public SimulationEngine(
        IReadOnlyList<WavFiles> fileList,
        int startPoint,
        int endPoint)
    {
        _fileList = fileList ?? throw new ArgumentNullException(nameof(fileList));

        _startPoint = startPoint;
        _endPoint = endPoint;
        _parallelDegree = Math.Max(1, Environment.ProcessorCount - 1);
    }

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

        Debug.WriteLine($"=== RunParallelSimulationDetailed Start ===");
        Debug.WriteLine($"Parallel simulation: {total} thresholds, {_parallelDegree} threads");
        Debug.WriteLine($"Range: {rangeMin:F2} - {rangeMax:F2}, Step: {step:F2}");

        int cachedCount = _fileList.Count(f => f.CachedData != null);
        Debug.WriteLine($"Cached audio files: {cachedCount}/{_fileList.Count}");

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _parallelDegree };
        var sw = Stopwatch.StartNew();

        Parallel.ForEach(thresholds, parallelOptions, threshold =>
        {
            try
            {
                int fileCount = SimulateThreshold(threshold);
                results.Add(new SimulationPoint(threshold, fileCount));

                int current = System.Threading.Interlocked.Increment(ref completed);

                // 進捗を0.0～1.0の範囲で報告（0.0～0.7は計算、0.7～1.0は統計用）
                double percentage = (double)current / total * 0.7;
                progress?.Report(percentage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR: Simulation failed at threshold={threshold:F2}: {ex.Message}");
                Debug.WriteLine($"  StackTrace: {ex.StackTrace}");
            }
        });

        sw.Stop();
        Debug.WriteLine($"=== RunParallelSimulationDetailed Complete ===");
        Debug.WriteLine($"Completed {results.Count} simulations in {sw.ElapsedMilliseconds} ms");

        return results.OrderByDescending(r => r.Threshold).ToList();
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
        const int Base36Limit = 1295;
        const int Base62Limit = 3844;
        bool base36Found = false;
        bool base62Found = false;
        float base36Threshold = 0f;
        float base62Threshold = 0f;

        Debug.WriteLine($"=== RunParallelSimulation Start (with early termination) ===");
        Debug.WriteLine($"Sequential simulation: {total} thresholds max");
        Debug.WriteLine($"Range: {rangeMin:F2} - {rangeMax:F2}, Step: {step:F2}");
        Debug.WriteLine($"File range: {_startPoint} - {_endPoint}");
        Debug.WriteLine($"Base36 limit: {Base36Limit} files");
        Debug.WriteLine($"Base62 limit: {Base62Limit} files");

        // 音声キャッシュの確認
        int cachedCount = _fileList.Count(f => f.CachedData != null);
        Debug.WriteLine($"Cached audio files: {cachedCount}/{_fileList.Count}");

        if (cachedCount == 0)
        {
            Debug.WriteLine("CRITICAL ERROR: No cached audio data! All simulations will return original count.");
        }

        var sw = Stopwatch.StartNew();

        // 順次実行（しきい値降順）
        foreach (var threshold in thresholds)
        {
            try
            {
                int fileCount = SimulateThreshold(threshold);
                results.Add(new SimulationPoint(threshold, fileCount));

                completed++;

                // Base62条件チェック
                if (!base62Found && fileCount <= Base62Limit)
                {
                    base62Found = true;
                    base62Threshold = threshold;
                    Debug.WriteLine($"=== Base62 condition met at threshold={threshold:F2} ===");
                    Debug.WriteLine($"File count: {fileCount} <= {Base62Limit}");
                }

                // Base36条件チェック
                if (!base36Found && fileCount <= Base36Limit)
                {
                    base36Found = true;
                    base36Threshold = threshold;
                    Debug.WriteLine($"=== Base36 condition met at threshold={threshold:F2} ===");
                    Debug.WriteLine($"File count: {fileCount} <= {Base36Limit}");
                    Debug.WriteLine($"Skipping remaining {total - completed} simulations");
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
                Debug.WriteLine($"ERROR: Simulation failed at threshold={threshold:F2}: {ex.Message}");
                Debug.WriteLine($"  StackTrace: {ex.StackTrace}");
            }
        }

        sw.Stop();
        Debug.WriteLine($"=== RunParallelSimulation Complete ===");
        Debug.WriteLine($"Completed {results.Count}/{total} simulations in {sw.ElapsedMilliseconds} ms");
        Debug.WriteLine($"Saved {total - completed} simulations due to early termination");

        // Base36/Base62の結果を報告
        if (base62Found)
        {
            Debug.WriteLine($"Base62 threshold: {base62Threshold:F2}");
        }
        else
        {
            Debug.WriteLine("Base62 condition not met in simulation range");
        }

        if (base36Found)
        {
            Debug.WriteLine($"Base36 threshold: {base36Threshold:F2}");
        }
        else
        {
            Debug.WriteLine("Base36 condition not met in simulation range");
        }

        // 結果の統計
        if (results.Count > 0)
        {
            var minFiles = results.Min(r => r.FileCount);
            var maxFiles = results.Max(r => r.FileCount);
            Debug.WriteLine($"File count range: {minFiles} - {maxFiles}");

            if (minFiles == maxFiles)
            {
                Debug.WriteLine("WARNING: All simulations returned the same file count - no reduction detected!");
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
    private int SimulateThreshold(float threshold)
    {
        var groupingStrategy = new AudioFileGroupingStrategy();
        IReadOnlyList<IReadOnlyList<int>> groups = groupingStrategy.GroupFiles(_fileList, _startPoint, _endPoint);

        var replaceTable = new int[3844]; // BMSの最大定義番号

        int totalComparisons = 0;
        int totalMatches = 0;

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _parallelDegree };

        Parallel.ForEach(groups, parallelOptions, group =>
        {
            int groupComparisons = 0;
            int groupMatches = 0;
            ProcessGroup(group, replaceTable, threshold, ref groupComparisons, ref groupMatches);

            System.Threading.Interlocked.Add(ref totalComparisons, groupComparisons);
            System.Threading.Interlocked.Add(ref totalMatches, groupMatches);
        });

        // Union-Findで代表値を辿ってユニークファイル数をカウント
        int uniqueCount = 0;
        int totalInRange = 0;
        int notProcessed = 0;

        foreach (WavFiles file in _fileList)
        {
            int fileNum = file.NumInteger;
            if (fileNum >= _startPoint && fileNum <= _endPoint)
            {
                totalInRange++;

                // 代表値（ルート）を見つける
                int root = FindRoot(replaceTable, fileNum);

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
            Debug.WriteLine($"=== Simulation Threshold {threshold:F2} Detail ===");
            Debug.WriteLine($"  Total in range: {totalInRange}");
            Debug.WriteLine($"  Unique (self-ref): {uniqueCount}");
            Debug.WriteLine($"  Not processed (==0): {notProcessed}");
            Debug.WriteLine($"  Total comparisons: {totalComparisons}");
            Debug.WriteLine($"  Total matches: {totalMatches}");
        }

        // 最初の数回のシミュレーションで詳細ログ
        if (threshold >= 0.98f || threshold <= 0.07f || Math.Abs(threshold - 0.50f) < 0.01f)
        {
            Debug.WriteLine($"  Threshold {threshold:F2}: Groups={groups.Count}, Comparisons={totalComparisons}, Matches={totalMatches}, Unique={uniqueCount}");
        }

        return uniqueCount;
    }

    /// <summary>
    /// グループ比較（Union-Find方式・スレッドセーフ）。
    /// </summary>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>単一ファイルグループは自分自身を登録</item>
    /// <item>RMS値でソート</item>
    /// <item>各ファイルと後続の近傍ファイルを比較（Sort &amp; Sweep）</item>
    /// <item>一致した場合、Union-Findで統合</item>
    /// </list>
    /// 
    /// <para>【高速化パス】</para>
    /// <list type="bullet">
    /// <item>Fast path 1: ファイル名完全一致</item>
    /// <item>Fast path 2: フィンガープリント一致</item>
    /// <item>Slow path: 波形比較（<see cref="FastWaveCompare"/>）</item>
    /// </list>
    /// </remarks>
    private void ProcessGroup(
        IReadOnlyList<int> group,
        int[] replaceTable,
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
            (float min, float max) thresholds = CalculateRmsRange(rms1);

            for (int j = i + 1; j < n; j++)
            {
                float rms2 = entries[j].Rms;

                // Early break: sorted by RMS
                if (rms2 > thresholds.max) break;

                int jIdx = entries[j].OriginalIndex;
                int jVal = _fileList[jIdx].NumInteger;

                if (jVal < _startPoint || jVal > _endPoint) continue;
                if (replaceTable[jVal] != 0) continue;

                // Fast path: exact name match
                if (_fileList[iIdx].Name.Equals(_fileList[jIdx].Name))
                {
                    if (System.Threading.Interlocked.CompareExchange(ref replaceTable[jVal], iVal, 0) == 0)
                    {
                        System.Threading.Interlocked.Increment(ref matches);
                    }
                    continue;
                }

                // Fast path: fingerprint match
                if (!string.IsNullOrEmpty(_fileList[iIdx].AudioFingerprint) &&
                    _fileList[iIdx].AudioFingerprint.Equals(_fileList[jIdx].AudioFingerprint))
                {
                    if (System.Threading.Interlocked.CompareExchange(ref replaceTable[jVal], iVal, 0) == 0)
                    {
                        System.Threading.Interlocked.Increment(ref matches);
                    }
                    continue;
                }

                // RMS range check
                if (rms2 < thresholds.min || rms2 > thresholds.max)
                    continue;

                // Actual audio comparison
                try
                {
                    CachedSoundData? cachedData1 = _fileList[iIdx].CachedData;
                    CachedSoundData? cachedData2 = _fileList[jIdx].CachedData;

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
                            UpdateReplaceTable(replaceTable, iVal, jVal);
                            System.Threading.Interlocked.Increment(ref matches);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ERROR: Audio comparison failed [{iIdx}] vs [{jIdx}]: {ex.Message}");
                }
            }
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
            CachedSoundData? cachedData = _fileList[idx].CachedData;
            if (cachedData != null)
            {
                entries.Add((idx, cachedData.TotalRms));
            }
        }

        entries.Sort((a, b) => a.Rms.CompareTo(b.Rms));
        return entries;
    }

    /// <summary>
    /// Union-Findアルゴリズム: 代表値（ルート）を見つける（経路圧縮付き）
    /// </summary>
    /// <param name="replaceTable">置換テーブル。</param>
    /// <param name="fileNum">検索するファイル番号。</param>
    /// <returns>代表値（ルート）。</returns>
    /// <remarks>
    /// <para>【アルゴリズム】</para>
    /// 親を辿って代表値に到達するまで繰り返し、経路圧縮を行います。
    /// 
    /// <para>【ターミネーション条件】</para>
    /// - parent == 0: 未設定（自分自身がルート）
    /// - parent == current: 自分自身を指している（ルート）
    /// 
    /// <para>【非再帰版】</para>
    /// スタックオーバーフロー防止のため、whileループで実装。
    /// 深いツリーでも安全です。
    /// 
    /// <para>【経路圧縮（Path Compression）】</para>
    /// 探索経路上の全ノードが直接ルートを指すように更新し、
    /// 後続の探索を高速化します。
    /// </remarks>
    internal static int FindRoot(int[] replaceTable, int fileNum)
    {
        int current = fileNum;
        var pathNodes = new List<int>();

        // ルートまで辿り、経路を記録
        while (true)
        {
            int parent = replaceTable[current];

            // 自分自身が代表値、または未設定の場合
            if (parent == 0 || parent == current)
                break;

            pathNodes.Add(current);
            current = parent;
        }

        // 経路圧縮：経路上の全ノードが直接ルートを指すように更新
        int root = current;
        foreach (var node in pathNodes)
        {
            replaceTable[node] = root;
        }

        return root;
    }

    /// <summary>
    /// Union-Findアルゴリズム: 2つの集合を統合
    /// </summary>
    /// <param name="replaceTable">置換テーブル。</param>
    /// <param name="iVal">ファイル番号1。</param>
    /// <param name="jVal">ファイル番号2。</param>
    /// <remarks>
    /// <para>【アルゴリズム】</para>
    /// <list type="number">
    /// <item>両方の代表値（ルート）を取得</item>
    /// <item>既に同じグループなら何もしない</item>
    /// <item>小さい方を親、大きい方を子にする</item>
    /// <item>CAS操作（Compare-And-Swap）で排他制御</item>
    /// </list>
    /// 
    /// <para>【スレッドセーフ性】</para>
    /// Interlocked.CompareExchangeを使用することで、
    /// ロックレスに統合を実現します。
    /// </remarks>
    internal static void UpdateReplaceTable(int[] replaceTable, int iVal, int jVal)
    {
        // 両方の代表値を見つける
        int rootI = FindRoot(replaceTable, iVal);
        int rootJ = FindRoot(replaceTable, jVal);

        // 既に同じグループなら何もしない
        if (rootI == rootJ)
            return;

        // より小さい値を代表値にする
        int minRoot = Math.Min(rootI, rootJ);
        int maxRoot = Math.Max(rootI, rootJ);

        // CASで統合
        System.Threading.Interlocked.CompareExchange(ref replaceTable[maxRoot], minRoot, 0);
        System.Threading.Interlocked.CompareExchange(ref replaceTable[maxRoot], minRoot, maxRoot);
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
    private readonly struct AudioEntry
    {
        public readonly int OriginalIndex;
        public readonly float Rms;

        public AudioEntry(int index, float rms)
        {
            OriginalIndex = index;
            Rms = rms;
        }
    }
}
