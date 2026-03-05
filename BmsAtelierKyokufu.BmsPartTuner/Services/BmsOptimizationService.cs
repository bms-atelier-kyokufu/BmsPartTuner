using System.Collections.ObjectModel;
using System.Diagnostics;
using BmsAtelierKyokufu.BmsPartTuner.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Core.Validation;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// BMS最適化サービス。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>BMS定義の削減処理</item>
/// <item>入力検証（定義範囲、相関係数しきい値）← IInputValidationService</item>
/// <item>しきい値最適化シミュレーション（100回シミュレーション）</item>
/// </list>
/// 
/// <para>【設計パターン】</para>
/// <list type="bullet">
/// <item>Strategy Pattern: Validator（<see cref="DefinitionRangeValidator"/>, <see cref="R2ThresholdValidator"/>）</item>
/// </list>
/// </remarks>
public class BmsOptimizationService : IBmsOptimizationService
{
    private readonly DefinitionRangeValidator _definitionRangeValidator;
    private readonly R2ThresholdValidator _r2ThresholdValidator;

    /// <summary>
    /// BmsOptimizationServiceを初期化します。
    /// </summary>
    public BmsOptimizationService()
    {
        _definitionRangeValidator = new DefinitionRangeValidator();
        _r2ThresholdValidator = new R2ThresholdValidator();
    }

    #region パブリックメソッド

    /// <summary>
    /// 最適なしきい値を見つけるため、100回のシミュレーションを実行します。
    /// </summary>
    /// <param name="files">ファイルリスト。</param>
    /// <param name="startDefinition">開始定義。</param>
    /// <param name="endDefinition">終了定義。</param>
    /// <param name="progress">進捗報告（0.0～1.0）。</param>
    /// <returns>最適化結果。</returns>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>Stopwatchで実行時間を計測開始</item>
    /// <item>メモリ使用量の初期値を取得</item>
    /// <item>SimulationEngineで0.00～1.00の範囲でしきい値を変化させつつシミュレーション実行</item>
    /// <item>Base36（1295）とBase62（3843）制限に基づいて最適値を探索</item>
    /// <item>実行時間とメモリ使用量を計測</item>
    /// <item>結果をOptimizationResultに詰めて返す</item>
    /// </list>
    /// 
    /// <para>【最適値探索ロジック】</para>
    /// <list type="bullet">
    /// <item>Base36: ファイル数がBase36Limit（1295）以下で、かつしきい値が最大のもを選択</item>
    /// <item>Base62: ファイル数がBase62Limit（3843）以下で、かつしきい値が最大のものを選択</item>
    /// <item>制限を超える場合は、制限以下で最も高いしきい値を選択</item>
    /// </list>
    /// 
    /// <para>【メモリ計測】</para>
    /// GC.GetTotalMemory(false)で推定メモリ使用量を計測します。
    /// </remarks>
    public async Task<Models.OptimizationResult?> FindOptimalThresholdsAsync(
        List<string> files,
        int startDefinition,
        int endDefinition,
        IProgress<int>? progress = null)
    {
        if (files == null || files.Count == 0)
            throw new ArgumentException("ファイルリストが空です", nameof(files));

        Stopwatch sw = Stopwatch.StartNew();
        long memoryBefore = GC.GetTotalMemory(false);
        long peakMemory = memoryBefore;

        // ファイルリストからObservableCollectionを作成
        ObservableCollection<WavFiles> fileListItems = new ObservableCollection<FileList.WavFiles>();
        try
        {
            Debug.WriteLine("=== FindOptimalThresholdsAsync: Starting ===");
            Debug.WriteLine($"Files: {files.Count}, Definition range: {startDefinition}-{endDefinition}");

            // endDefinitionが0の場合、自動検出（ファイル数から計算）
            int actualEndDefinition = endDefinition;
            if (actualEndDefinition == 0)
            {
                actualEndDefinition = startDefinition + files.Count - 1;
                Debug.WriteLine($"Auto-detected end definition: {actualEndDefinition}");
            }


            int fileNum = startDefinition;
            // 終了定義番号に基づいて適切な基数を選択
            int radix = actualEndDefinition > AppConstants.Definition.MaxNumberBase36
                ? AppConstants.Definition.RadixBase62
                : AppConstants.Definition.RadixBase36;

            foreach (string filePath in files)
            {
                if (File.Exists(filePath))
                {
                    WavFiles wavFile = new FileList.WavFiles
                    {
                        Num = Core.Helpers.RadixConvert.IntToZZ(fileNum, radix),
                        NumInteger = fileNum,
                        Name = filePath,
                        FileSize = new FileInfo(filePath).Length
                    };
                    fileListItems.Add(wavFile);
                    fileNum++;

                    if (fileNum > actualEndDefinition)
                        break;
                }
            }

            int originalCount = fileListItems.Count;

            if (originalCount == 0)
            {
                Debug.WriteLine("ERROR: No valid files found");
                return null;
            }

            // 実際の終了定義番号を更新（ロードしたファイル数に基づく）
            int actualEnd = startDefinition + originalCount - 1;
            Debug.WriteLine($"Valid files loaded: {originalCount}, Actual range: {startDefinition}-{actualEnd}");

            // 音声キャッシュをプリロード
            progress?.Report(5);
            List<string> failedFiles = new List<string>();
            try
            {
                failedFiles = AudioCacheManager.PreloadAudioData(
                    fileListItems,
                    new Progress<int>(p => progress?.Report(5 + p / 20)), // 5-10%
                    Models.NormalizationMode.None);

                if (failedFiles.Count > 0)
                {
                    Debug.WriteLine($"WARNING: {failedFiles.Count} files failed to load");
                    foreach (string? file in failedFiles.Take(5))
                    {
                        Debug.WriteLine($"  - {Path.GetFileName(file)}");
                    }
                    if (failedFiles.Count > 5)
                    {
                        Debug.WriteLine($"  ... and {failedFiles.Count - 5} more");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR: Failed to preload audio data: {ex.Message}");
                // 音声ファイルの読み込みに失敗した場合は処理を中断
                return null;
            }

            progress?.Report(10);

            // SimulationEngineでシミュレーション実行
            // endPointは実際にロードしたファイルの最大定義番号を使用
            SimulationEngine simulationEngine = new SimulationEngine(
                fileListItems,
                startDefinition,
                actualEnd);

            Progress<int> simulationProgress = new Progress<int>(p => progress?.Report(10 + (int)(p * 0.6))); // 10-70%

            IReadOnlyList<SimulationPoint> simulationResults = await Task.Run(() =>
                simulationEngine.RunParallelSimulation(
                    0.00f,      // 最小しきい値
                    1.00f,      // 最大しきい値
                    0.01f,      // ステップ
                    simulationProgress));

            progress?.Report(70);

            // シミュレーション結果をOptimizationResult形式に変換
            List<(double, int FileCount)> simulationData = simulationResults
                .Select(r => ((double)r.Threshold, r.FileCount))
                .ToList();

            Debug.WriteLine($"Simulation results: {simulationData.Count} data points");
            if (simulationData.Count > 0)
            {
                int minCount = simulationData.Min(d => d.Item2);
                int maxCount = simulationData.Max(d => d.Item2);
                Debug.WriteLine($"File count range in results: {minCount} - {maxCount}");
            }

            // 70-90%: データソートと最適値探索
            progress?.Report(75);

            // Base36とBase62の最適値を探索
            (float base36Threshold, int base36Count) = FindOptimalThreshold(simulationData, Core.AppConstants.Definition.MaxNumberBase36);

            // Base62: シミュレーションデータから検索
            // Base36で見つかったしきい値以上のデータから、Base62制限を満たす最高しきい値を探す
            (float base62Threshold, int base62Count) = FindOptimalThreshold(simulationData, Core.AppConstants.Definition.MaxNumberBase62);

            // Base62が見つからない場合はBase36と同じ値を使用（フォールバック）
            if (base62Count == 0 || base62Threshold < base36Threshold)
            {
                Debug.WriteLine($"Base62: Using Base36 threshold as fallback (no better option found)");
                base62Threshold = base36Threshold;
                base62Count = base36Count;
            }

            Debug.WriteLine($"Base36: Threshold={base36Threshold:F2}, Count={base36Count}");
            Debug.WriteLine($"Base62: Threshold={base62Threshold:F2}, Count={base62Count}");

            progress?.Report(85);

            // 90-100%: メモリ計測と完了
            sw.Stop();
            long currentMemory = GC.GetTotalMemory(false);
            long memoryUsed = Math.Max(0, currentMemory - memoryBefore);

            OptimizationResult result = new Models.OptimizationResult
            {
                Base36Result = (base36Threshold, base36Count),
                Base62Result = (base62Threshold, base62Count),
                SimulationData = simulationData,
                ExecutionTime = sw.Elapsed,
                MemoryUsedBytes = memoryUsed
            };

            // 警告メッセージを追加
            if (failedFiles.Count > 0)
            {
                string warningMessage = failedFiles.Count == 1
                    ? $"1 件の音声ファイルが読み込めなかったため、最適化から除外されました:\n{Path.GetFileName(failedFiles[0])}"
                    : $"{failedFiles.Count} 件の音声ファイルが読み込めなかったため、最適化から除外されました。";
                result.Warnings.Add(warningMessage);

                Debug.WriteLine($"Added warning to result: {warningMessage}");
            }

            progress?.Report(100);

            Debug.WriteLine($"=== FindOptimalThresholdsAsync: Complete ({sw.ElapsedMilliseconds}ms) ===");
            Debug.WriteLine($"Base36 optimal: Threshold={base36Threshold:F2}, Count={base36Count}");
            Debug.WriteLine($"Base62 optimal: Threshold={base62Threshold:F2}, Count={base62Count}");
            Debug.WriteLine($"Memory used: {memoryUsed / 1024.0 / 1024.0:F2} MB");
            Debug.WriteLine($"Simulation data points: {simulationData.Count}");

            Debug.WriteLine("=== Clearing audio cache ===");
            CleanupAudioCache(fileListItems);
            fileListItems.Clear();

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ERROR in FindOptimalThresholdsAsync: {ex.Message}");
            Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            CleanupAudioCache(fileListItems);
            fileListItems?.Clear();

            return null;
        }
    }

    /// <summary>
    /// ファイル数制限に基づいて最適なしきい値を探索します。
    /// </summary>
    /// <param name="simulationData">シミュレーションデータ。</param>
    /// <param name="fileLimit">ファイル数制限。</param>
    /// <returns>最適なしきい値とそのときのファイル数のタプル。</returns>
    /// <remarks>
    /// <para>【探索アルゴリズム】</para>
    /// ファイル数がfileLimitを超えない範囲で、最も高いしきい値を選択します。
    /// しきい値が高いほど品質が保たれるため、制限を満たす最大しきい値が最適です。
    /// </remarks>
    private (float Threshold, int Count) FindOptimalThreshold(
        List<(double Threshold, int Count)> simulationData,
        int fileLimit)
    {
        if (simulationData == null || simulationData.Count == 0)
        {
            Debug.WriteLine($"FindOptimalThreshold: No simulation data, returning default");
            return (0.60f, 0);
        }

        // ファイル数がfileLimit以下のエントリを抽出
        List<(double Threshold, int Count)> validEntries = simulationData
            .Where(d => d.Count > 0 && d.Count <= fileLimit)
            .ToList();

        Debug.WriteLine($"FindOptimalThreshold: {validEntries.Count} valid entries for limit {fileLimit}");

        if (validEntries.Count == 0)
        {
            // 全てのエントリがfileLimit超えまたは0件の場合
            // ファイル数が最も少ない（0以外の）ものを選択
            List<(double Threshold, int Count)> nonZeroEntries = simulationData.Where(d => d.Count > 0).ToList();

            if (nonZeroEntries.Count == 0)
            {
                Debug.WriteLine($"FindOptimalThreshold: All entries have 0 count, returning default");
                return (0.60f, 0);
            }

            (double Threshold, int Count) minEntry = nonZeroEntries.OrderBy(d => d.Count).First();
            Debug.WriteLine($"FindOptimalThreshold: Using min count entry: Threshold={minEntry.Threshold:F2}, Count={minEntry.Count}");
            return ((float)minEntry.Threshold, minEntry.Count);
        }

        // 制限を満たす中で、しきい値が最も高い（=品質が最大）ものを選択
        (double Threshold, int Count) optimalEntry = validEntries
            .OrderByDescending(d => d.Threshold)
            .First();

        Debug.WriteLine($"FindOptimalThreshold: Optimal entry: Threshold={optimalEntry.Threshold:F2}, Count={optimalEntry.Count}");
        return ((float)optimalEntry.Threshold, optimalEntry.Count);
    }

    /// <summary>
    /// 定義削減処理結果。
    /// </summary>
    public class ReductionResult
    {
        /// <summary>元のファイル数。</summary>
        public int OriginalCount { get; set; }

        /// <summary>最適化後のファイル数。</summary>
        public int OptimizedCount { get; set; }

        /// <summary>削減率（0.0～1.0）。</summary>
        public double ReductionRate { get; set; }

        /// <summary>処理時間。</summary>
        public TimeSpan ProcessingTime { get; set; }

        /// <summary>使用した相関係数しきい値。</summary>
        public float Threshold { get; set; }

        /// <summary>成功フラグ。</summary>
        public bool IsSuccess { get; set; }

        /// <summary>エラーメッセージ（失敗時）。</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>物理削除されたファイル数。</summary>
        public int DeletedFilesCount { get; set; }
    }

    /// <summary>
    /// 定義削減処理を実行。
    /// </summary>
    /// <param name="fileList">ファイルリスト。</param>
    /// <param name="inputPath">入力BMSファイルパス。</param>
    /// <param name="outputPath">出力BMSファイルパス。</param>
    /// <param name="r2Threshold">相関係数しきい値。</param>
    /// <param name="startDefinition">開始定義。</param>
    /// <param name="endDefinition">終了定義。</param>
    /// <param name="progress">進捗報告。</param>
    /// <param name="selectedKeywords">選択されたキーワード（nullまたは空の場合は全て処理）。</param>
    /// <returns>最適化結果。</returns>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item><see cref="DefinitionReuse"/>で削減処理を実行</item>
    /// <item>削減後のユニークファイル数を取得</item>
    /// <item>削減率を計算</item>
    /// <item>結果を返す</item>
    /// </list>
    /// 
    /// <para>【Why Task.Run】</para>
    /// 削減処理は長時間かかるため、UIスレッドをブロックしないよう
    /// バックグラウンドスレッドで実行します。
    /// </remarks>
    public async Task<ReductionResult> ExecuteDefinitionReductionAsync(
        IReadOnlyList<WavFiles> fileList,
        string inputPath,
        string outputPath,
        float r2Threshold,
        int startDefinition,
        int endDefinition,
        bool isPhysicalDeletionEnabled,
        IProgress<int>? progress = null,
        IEnumerable<string>? selectedKeywords = null)
    {
        if (fileList == null)
            throw new ArgumentNullException(nameof(fileList));
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new ArgumentException("入力パスが指定されていません", nameof(inputPath));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("出力パスが指定されていません", nameof(outputPath));

        Stopwatch sw = Stopwatch.StartNew();

        // DefinitionReuse expects an ObservableCollection, so we need to convert
        ObservableCollection<WavFiles> observableCollection = new ObservableCollection<WavFiles>(fileList);
        DefinitionReuse dr = new DefinitionReuse(observableCollection);

        int originalCount = fileList.Count;
        int optimizedCount = originalCount;
        int deletedFilesCount = 0;
        Func<string, ReductionResult> errorResult = (string message) =>
            {
                Debug.WriteLine($"ERROR in ExecuteDefinitionReductionAsync: {message}");
                sw.Stop();
                return new ReductionResult
                {
                    OriginalCount = originalCount,
                    OptimizedCount = optimizedCount,
                    ReductionRate = 0,
                    ProcessingTime = sw.Elapsed,
                    Threshold = r2Threshold,
                    IsSuccess = false,
                    ErrorMessage = message
                };
            };

        try
        {
            await Task.Run(() =>
            {
                // isPhysicalDeletionEnabledは常にfalseを渡す
                // Why: ここでtrueを渡すとDefinitionReuse内でファイルが削除されてしまい、
                //      直後のサービス側の削除ループで「ファイルなし」と判定され、削除数がカウントできないため。
                //      物理削除はサービス側で一元管理する。
                dr.ReductDefinition(
                    inputPath,
                    progress ?? new Progress<int>(),
                    r2Threshold,
                    outputPath,
                    startDefinition,
                    endDefinition,
                    isPhysicalDeletionEnabled: false,
                    selectedKeywords: selectedKeywords);

                // 物理削除処理
                if (isPhysicalDeletionEnabled)
                {
                    List<string> unusedFiles = dr.GetUnusedFilePaths();
                    deletedFilesCount = DeleteUnusedFiles(unusedFiles);
                }
            });

            sw.Stop();

            optimizedCount = dr.GetUniqueFileCount();
            double reductionRate = CalculateReductionRate(originalCount, optimizedCount);

            Debug.WriteLine("=== Clearing audio cache after reduction ===");
            CleanupAudioCache(fileList);

            return new ReductionResult
            {
                OriginalCount = originalCount,
                OptimizedCount = optimizedCount,
                ReductionRate = reductionRate,
                ProcessingTime = sw.Elapsed,
                Threshold = r2Threshold,
                IsSuccess = true,
                DeletedFilesCount = deletedFilesCount
            };
        }
        catch (FileNotFoundException ex)
        {
            CleanupAudioCache(fileList);
            return errorResult($"ファイルが見つかりません: {ex.FileName}");
        }
        catch (IOException ex)
        {
            // ファイル削除失敗やアクセス拒否を処理
            foreach (WavFiles file in fileList)
            {
                if (file.CachedData != null)
                {
                    file.CachedData.Dispose();
                    file.ClearCache();
                }
            }

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            return errorResult($"ファイル操作エラー: {ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            CleanupAudioCache(fileList);
            return errorResult("ファイルへのアクセスが拒否されました");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ERROR in ExecuteDefinitionReductionAsync: {ex.Message}");
            Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            CleanupAudioCache(fileList);
            Trace.TraceError($"Unexpected error: {ex}");
            return errorResult($"予期しないエラーが発生しました: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 定義範囲の検証。
    /// </summary>
    /// <param name="startVal">開始定義値。</param>
    /// <param name="endVal">終了定義値。</param>
    /// <returns>検証結果。</returns>
    /// <remarks>
    /// <para>【Strategy Pattern使用】</para>
    /// <see cref="DefinitionRangeValidator"/>に検証ロジックを委譲します。
    /// </remarks>
    public ValidationResult ValidateDefinitionRange(string startVal, string endVal)
    {
        DefinitionRange range = new DefinitionRange(startVal, endVal);
        return _definitionRangeValidator.Validate(range);
    }

    /// <summary>
    /// 相関係数しきい値の検証。
    /// </summary>
    /// <param name="r2Text">しきい値文字列。</param>
    /// <returns>検証結果（値付き）。</returns>
    /// <remarks>
    /// <para>【Strategy Pattern使用】</para>
    /// <see cref="R2ThresholdValidator"/>に検証ロジックを委譲します。
    /// </remarks>
    public ValidationResult<float> ValidateR2Threshold(string r2Text)
    {
        return _r2ThresholdValidator.ValidateWithValue(r2Text);
    }

    #endregion

    #region プライベートメソッド（Extract Method）

    /// <summary>
    /// 音声キャッシュをクリアします。
    /// </summary>
    /// <param name="files">クリア対象のファイルリスト。</param>
    /// <remarks>
    /// <para>【IDisposableパターン】</para>
    /// CachedSoundDataはIDisposableを実装しているため、
    /// Disposeを呼び出すことでアンマネージドリソースを解放します。
    /// GCは.NETランタイムが最適なタイミングで自動的に実行します。
    /// </remarks>
    private static void CleanupAudioCache(IEnumerable<WavFiles>? files)
    {
        if (files == null) return;

        int clearedCount = 0;
        foreach (WavFiles file in files)
        {
            if (file.CachedData != null)
            {
                file.CachedData.Dispose();
                file.ClearCache();
                clearedCount++;
            }
        }
        Debug.WriteLine($"Cleared {clearedCount} cached audio files");
    }

    /// <summary>
    /// 未使用ファイルを物理削除します。
    /// </summary>
    /// <param name="unusedFiles">削除対象のファイルパス一覧。</param>
    /// <returns>削除に成功したファイル数。</returns>
    /// <remarks>
    /// <para>【Why 個別try-catch】</para>
    /// 一部のファイルが削除できなくても処理を継続するため、
    /// 各ファイルの削除を個別にtry-catchで囲みます。
    /// </remarks>
    private static int DeleteUnusedFiles(IEnumerable<string> unusedFiles)
    {
        int deletedCount = 0;
        foreach (string file in unusedFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                    deletedCount++;
                    Debug.WriteLine($"Deleted unused file: {file}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete file {file}: {ex.Message}");
            }
        }
        return deletedCount;
    }

    /// <summary>
    /// 削減結果の統計を計算します。
    /// </summary>
    /// <param name="originalCount">元のファイル数。</param>
    /// <param name="optimizedCount">最適化後のファイル数。</param>
    /// <returns>削減率（0.0～1.0）。</returns>
    private static double CalculateReductionRate(int originalCount, int optimizedCount)
    {
        return originalCount > 0
            ? (double)(originalCount - optimizedCount) / originalCount
            : 0;
    }

    #endregion

}
