using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Core.Validation;
using ValidationResult = BmsAtelierKyokufu.BmsPartTuner.Core.Validation.ValidationResult;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms;

/// <summary>
/// BMS定義の最適化、しきい値のシミュレーション、および関連する入力の検証を担当するサービス。
/// </summary>
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
    /// 最適なしきい値を見つけるため、指定された範囲でシミュレーションを実行します。
    /// 実行時間とメモリ使用量を計測し、結果を返します。
    /// </summary>
    /// <param name="files">処理対象のファイルリスト。</param>
    /// <param name="startDefinition">最適化を開始する定義のインデックス。</param>
    /// <param name="endDefinition">最適化を終了する定義のインデックス。</param>
    /// <param name="progress">進捗を報告するためのオブジェクト。</param>
    /// <returns>最適化のシミュレーション結果。エラー時はnullを返します。</returns>
    public async Task<Models.OptimizationResult?> FindOptimalThresholdsAsync(
        List<string> files,
        int startDefinition,
        int endDefinition,
        IProgress<int>? progress = null)
    {
        if (files == null || files.Count == 0)
            throw new ArgumentException("ファイルリストが空です", nameof(files));

        PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), "=== FindOptimalThresholdsAsync: Starting ===");
        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timer = PerformanceDebugLogger.StartTimer();
        long memoryBefore = GC.GetTotalMemory(false);
        long peakMemory = memoryBefore;

        // ファイルリストからObservableCollectionを作成
        ObservableCollection<BmsAudioFile> fileListItems = [];
        System.Collections.Concurrent.ConcurrentDictionary<string, Models.ICachedSoundData>? audioCache = null;
        try
        {
            // endDefinitionが0の場合、自動検出（ファイル数から計算）
            int actualEndDefinition = endDefinition;
            if (actualEndDefinition == 0)
            {
                actualEndDefinition = startDefinition + files.Count - 1;
                PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Auto-detected end definition: {actualEndDefinition}");
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
                    BmsAudioFile wavFile = new()
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
                PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), "ERROR: No valid files found");
                return null;
            }

            // 実際の終了定義番号を更新（ロードしたファイル数に基づく）
            int actualEnd = startDefinition + originalCount - 1;
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Valid files loaded: {originalCount}, Actual range: {startDefinition}-{actualEnd}");

            // 音声キャッシュをプリロード
            progress?.Report(5);
            List<string> failedFiles = [];
            try
            {
                var (FailedFiles, Cache) = AudioCacheManager.PreloadAudioData(
                    fileListItems,
                    new Progress<int>(p => progress?.Report(5 + (p / 20))), // 5-10%
                    Models.NormalizationMode.None);
                failedFiles = FailedFiles;
                audioCache = Cache;
                PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"AudioCacheManager.PreloadAudioData: {timer.Lap("AudioCacheManager.PreloadAudioData")} ms");

                if (failedFiles.Count > 0)
                {
                    PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"WARNING: {failedFiles.Count} files failed to load");
                    foreach (string? file in failedFiles.Take(5))
                    {
                        PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"  - {Path.GetFileName(file)}");
                    }
                    if (failedFiles.Count > 5)
                    {
                        PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"  ... and {failedFiles.Count - 5} more");
                    }
                }
            }
            catch (Exception ex)
            {
                PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"ERROR: Failed to preload audio data: {ex.Message}");
                // 音声ファイルの読み込みに失敗した場合は処理を中断
                return null;
            }

            progress?.Report(10);

            // SimulationEngineでシミュレーション実行
            // endPointは実際にロードしたファイルの最大定義番号を使用
            SimulationEngine simulationEngine = new(
                fileListItems,
                audioCache!,
                startDefinition,
                actualEnd);

            Progress<int> simulationProgress = new(p => progress?.Report(10 + (int)(p * 0.6))); // 10-70%

            IReadOnlyList<SimulationPoint> simulationResults = await Task.Run(() =>
                simulationEngine.RunParallelSimulation(
                    0.00f,      // 最小しきい値
                    1.00f,      // 最大しきい値
                    0.01f,      // ステップ
                    simulationProgress));
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"SimulationEngine.RunParallelSimulation: {timer.Lap("SimulationEngine.RunParallelSimulation")} ms");

            progress?.Report(70);

            // シミュレーション結果をOptimizationResult形式に変換
            List<(double, int FileCount)> simulationData = [.. simulationResults.Select(r => ((double)r.Threshold, r.FileCount))];

            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Simulation results: {simulationData.Count} data points");
            if (simulationData.Count > 0)
            {
                int minCount = simulationData.Min(d => d.FileCount);
                int maxCount = simulationData.Max(d => d.FileCount);
                PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"File count range in results: {minCount} - {maxCount}");
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
                PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), "Base62: Using Base36 threshold as fallback (no better option found)");
                base62Threshold = base36Threshold;
                base62Count = base36Count;
            }

            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Base36: Threshold={base36Threshold:F2}, Count={base36Count}");
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Base62: Threshold={base62Threshold:F2}, Count={base62Count}");

            progress?.Report(85);

            // 90-100%: メモリ計測と完了
            long totalElapsed = timerTotal.Lap("Total");
            long currentMemory = GC.GetTotalMemory(false);
            long memoryUsed = Math.Max(0, currentMemory - memoryBefore);

            OptimizationResult result = new()
            {
                Base36Result = (base36Threshold, base36Count),
                Base62Result = (base62Threshold, base62Count),
                SimulationData = simulationData,
                ExecutionTime = TimeSpan.FromMilliseconds(totalElapsed),
                MemoryUsedBytes = memoryUsed
            };

            // 警告メッセージを追加
            if (failedFiles.Count > 0)
            {
                string warningMessage = failedFiles.Count == 1
                    ? $"1 件の音声ファイルが読み込めなかったため、最適化から除外されました:\n{Path.GetFileName(failedFiles[0])}"
                    : $"{failedFiles.Count} 件の音声ファイルが読み込めなかったため、最適化から除外されました。";
                result.Warnings.Add(warningMessage);

                PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Added warning to result: {warningMessage}");
            }

            progress?.Report(100);

            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"=== FindOptimalThresholdsAsync: Complete ({totalElapsed}ms) ===");
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Base36 optimal: Threshold={base36Threshold:F2}, Count={base36Count}");
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Base62 optimal: Threshold={base62Threshold:F2}, Count={base62Count}");
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Memory used: {memoryUsed / 1024.0 / 1024.0:F2} MB");
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Simulation data points: {simulationData.Count}");

            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), "=== Clearing audio cache ===");
            if (audioCache != null)
            {
                CleanupAudioCache(fileListItems, audioCache);
            }
            fileListItems.Clear();

            return result;
        }
        catch (Exception ex)
        {
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"ERROR in FindOptimalThresholdsAsync: {ex.Message}");
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"StackTrace: {ex.StackTrace}");

            if (fileListItems != null && audioCache != null)
            {
                CleanupAudioCache(fileListItems, audioCache);
            }
            fileListItems?.Clear();

            return null;
        }
    }

    /// <summary>
    /// 指定されたファイル数制限を超えない範囲で、最も高いしきい値（品質が最高となる値）を探索します。
    /// </summary>
    /// <param name="simulationData">シミュレーションデータのリスト。</param>
    /// <param name="fileLimit">許容される最大ファイル数。</param>
    /// <returns>最適なしきい値とそのときのファイル数のタプル。</returns>
    private static (float Threshold, int Count) FindOptimalThreshold(
        List<(double Threshold, int Count)> simulationData,
        int fileLimit)
    {
        if (simulationData == null || simulationData.Count == 0)
        {
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), "FindOptimalThreshold: No simulation data, returning default");
            return (0.60f, 0);
        }

        // ファイル数がfileLimit以下のエントリを抽出
        List<(double Threshold, int Count)> validEntries = [.. simulationData.Where(d => d.Count > 0 && d.Count <= fileLimit)];

        PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"FindOptimalThreshold: {validEntries.Count} valid entries for limit {fileLimit}");

        if (validEntries.Count == 0)
        {
            // 全てのエントリがfileLimit超えまたは0件の場合
            // ファイル数が最も少ない（0以外の）ものを選択
            List<(double Threshold, int Count)> nonZeroEntries = [.. simulationData.Where(d => d.Count > 0)];

            if (nonZeroEntries.Count == 0)
            {
                PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), "FindOptimalThreshold: All entries have 0 count, returning default");
                return (0.60f, 0);
            }

            (double Threshold, int Count) = nonZeroEntries.OrderBy(d => d.Count).First();
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"FindOptimalThreshold: Using min count entry: Threshold={Threshold:F2}, Count={Count}");
            return ((float)Threshold, Count);
        }

        // 制限を満たす中で、しきい値が最も高い（=品質が最大）ものを選択
        (double Threshold, int Count) optimalEntry = validEntries
            .OrderByDescending(d => d.Threshold)
            .First();

        PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"FindOptimalThreshold: Optimal entry: Threshold={optimalEntry.Threshold:F2}, Count={optimalEntry.Count}");
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
    /// 入力されたBMSファイルに対して定義削減処理を非同期で実行します。
    /// </summary>
    /// <param name="fileList">対象となるBMS音声ファイルのリスト。</param>
    /// <param name="inputPath">入力BMSファイルのパス。</param>
    /// <param name="outputPath">出力BMSファイルのパス。</param>
    /// <param name="options">定義削減のオプション設定。</param>
    /// <returns>定義削減処理の結果。</returns>
    public async Task<ReductionResult> ExecuteDefinitionReductionAsync(
        IReadOnlyList<BmsAudioFile> fileList,
        string inputPath,
        string outputPath,
        DefinitionReductionOptions options)
    {
        // Manage static registries automatically using a scoped using session
        using var registrySession = new AudioRegistrySession();

        ArgumentNullException.ThrowIfNull(fileList);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new ArgumentException("入力パスが指定されていません", nameof(inputPath));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("出力パスが指定されていません", nameof(outputPath));

        if (options.InputBmsContent == null && !File.Exists(inputPath))
        {
            return new ReductionResult
            {
                OriginalCount = fileList.Count,
                OptimizedCount = fileList.Count,
                ReductionRate = 0,
                ProcessingTime = TimeSpan.Zero,
                Threshold = options.R2Threshold,
                IsSuccess = false,
                ErrorMessage = $"ファイルが見つかりません: {inputPath}"
            };
        }

        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timer = PerformanceDebugLogger.StartTimer();

        // 音声データの事前ロード（キャッシュ構築）
        var (FailedFiles, Cache) = AudioCacheManager.PreloadAudioData(fileList, options.Progress);
        var audioCache = Cache;
        PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"AudioCacheManager.PreloadAudioData: {timer.Lap("AudioCacheManager.PreloadAudioData")} ms");

        // DefinitionReuse expects an ObservableCollection, so we need to convert
        ObservableCollection<BmsAudioFile> observableCollection = new(fileList);
        DefinitionReuse dr = new(observableCollection, audioCache, options.InputBmsContent);
        PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"DefinitionReuse constructor: {timer.Lap("DefinitionReuse constructor")} ms");

        var originalCount = fileList.Count;
        var optimizedCount = originalCount;
        var deletedFilesCount = 0;
        ReductionResult errorResult(string message)
        {
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"ERROR in ExecuteDefinitionReductionAsync: {message}");
            return new ReductionResult
            {
                OriginalCount = originalCount,
                OptimizedCount = optimizedCount,
                ReductionRate = 0,
                ProcessingTime = TimeSpan.FromMilliseconds(timerTotal.Lap("Total")),
                Threshold = options.R2Threshold,
                IsSuccess = false,
                ErrorMessage = message
            };
        }

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
                    outputPath,
                    new DefinitionReductionOptions
                    {
                        R2Threshold = options.R2Threshold,
                        StartDefinition = options.StartDefinition,
                        EndDefinition = options.EndDefinition,
                        IsPhysicalDeletionEnabled = false,
                        InputBmsContent = options.InputBmsContent,
                        Progress = options.Progress ?? new Progress<int>(),
                        SelectedKeywords = options.SelectedKeywords
                    });

                // 物理削除処理
                if (options.IsPhysicalDeletionEnabled)
                {
                    var timerDelete = PerformanceDebugLogger.StartTimer();
                    List<string> unusedFiles = dr.GetUnusedFilePaths();
                    deletedFilesCount = DeleteUnusedFiles(unusedFiles);
                    PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"DeleteUnusedFiles: {timerDelete.Lap("DeleteUnusedFiles")} ms");
                }
            });
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"dr.ReductDefinition Task.Run total: {timer.Lap("dr.ReductDefinition Task.Run total")} ms");

            var totalElapsed = timerTotal.Lap("Total");
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"=== ExecuteDefinitionReductionAsync: Complete ({totalElapsed}ms) ===");

            optimizedCount = dr.GetUniqueFileCount();
            var reductionRate = CalculateReductionRate(originalCount, optimizedCount);

            CleanupAudioCache(fileList, audioCache);
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"CleanupAudioCache: {timer.Lap("CleanupAudioCache")} ms");

            return new ReductionResult
            {
                OriginalCount = originalCount,
                OptimizedCount = optimizedCount,
                ReductionRate = reductionRate,
                ProcessingTime = TimeSpan.FromMilliseconds(totalElapsed),
                Threshold = options.R2Threshold,
                IsSuccess = true,
                DeletedFilesCount = deletedFilesCount
            };
        }
        catch (FileNotFoundException ex)
        {
            return errorResult($"ファイルが見つかりません: {ex.FileName}");
        }
        catch (IOException ex)
        {
            // ファイル削除失敗やアクセス拒否を処理
            CleanupAudioCache(fileList, audioCache);

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            return errorResult($"ファイル操作エラー: {ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            return errorResult("ファイルへのアクセスが拒否されました");
        }
        catch (Exception ex)
        {
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"ERROR in ExecuteDefinitionReductionAsync: {ex.Message}");
            PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"StackTrace: {ex.StackTrace}");
            Trace.TraceError($"Unexpected error: {ex}");
            return errorResult($"予期しないエラーが発生しました: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 指定された開始および終了の定義値が正しい範囲にあるかを検証します。
    /// </summary>
    /// <param name="startVal">開始定義の値の文字列表現。</param>
    /// <param name="endVal">終了定義の値の文字列表現。</param>
    /// <returns>検証結果。</returns>
    public ValidationResult ValidateDefinitionRange(string startVal, string endVal)
    {
        DefinitionRange range = new(startVal, endVal);
        return _definitionRangeValidator.Validate(range);
    }

    /// <summary>
    /// 相関係数のしきい値文字列を検証し、有効なfloat値を取得します。
    /// </summary>
    /// <param name="r2Text">検証対象のしきい値文字列。</param>
    /// <returns>検証結果とパースされたしきい値。</returns>
    public ValidationResult<float> ValidateR2Threshold(string r2Text)
    {
        return R2ThresholdValidator.ValidateWithValue(r2Text);
    }

    #endregion

    #region プライベートメソッド（Extract Method）

    /// <summary>
    /// 音声キャッシュをクリアします。
    /// </summary>
    /// <param name="files">クリア対象のファイルリスト。</param>
    /// <param name="audioCache">音声キャッシュディクショナリ。</param>
    private static void CleanupAudioCache(IEnumerable<BmsAudioFile>? files, System.Collections.Concurrent.ConcurrentDictionary<string, ICachedSoundData> audioCache)
    {
        if (files == null || audioCache == null) return;

        int clearedCount = 0;
        foreach (BmsAudioFile file in files)
        {
            if (audioCache.TryGetValue(file.Name, out var cachedData))
            {
                cachedData.Dispose();
                clearedCount++;
            }
        }
        PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Cleared {clearedCount} cached audio files");
    }

    /// <summary>
    /// 削減処理によって未使用となった音源ファイルを物理的に削除します。
    /// </summary>
    /// <param name="unusedFiles">削除対象となる未使用ファイルのパスのリスト。</param>
    /// <returns>正常に削除されたファイルの数。</returns>
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
                    PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Deleted unused file: {file}");
                }
                else
                {
                    PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"File to delete not found: {file}");
                }
            }
            catch (Exception ex)
            {
                PerformanceDebugLogger.WriteDebug(nameof(BmsOptimizationService), $"Failed to delete unused file: {file}. Error: {ex.Message}");
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
