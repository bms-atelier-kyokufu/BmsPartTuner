using ValidationResult = BmsAtelierKyokufu.BmsPartTuner.Core.Validation.ValidationResult;
namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;

/// <summary>
/// BMS定義の最適化、しきい値のシミュレーション、および関連する入力の検証を担当するサービス。
/// </summary>
public class BmsOptimizationService : IBmsOptimizationService
{
    private readonly DefinitionRangeValidator _definitionRangeValidator;
    private readonly R2ThresholdValidator _r2ThresholdValidator;
    private static readonly Logger<BmsOptimizationService> s_logger = new();

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
    /// <returns>最適化のシミュレーション結果。エラー時はnullを返します。</returns>
    public async Task<OptimizationResult?> FindOptimalThresholdsAsync(
        List<string> files,
        int startDefinition,
        int endDefinition,
        IOperationContext? opContext = null)
    {
        if (files == null || files.Count == 0)
            throw new ArgumentException("ファイルリストが空です", nameof(files));

        long memoryBefore = GC.GetTotalMemory(false);
        var timerTotal = s_logger.StartTimer();

        var context = new Pipeline.OptimizationSimulationContext(
            files,
            startDefinition,
            endDefinition,
            opContext);

        var pipeline = new Pipeline.OptimizationSimulationPipeline()
            .AddStep(new Pipeline.LoadValidFilesStep())
            .AddStep(new Pipeline.PreloadAudioCacheStep())
            .AddStep(new Pipeline.RunParallelSimulationStep())
            .AddStep(new Pipeline.FindOptimalThresholdsStep());

        var result = await pipeline.ExecuteAsync(context);

        if (result != null)
        {
            // メモリ計測と完了
            long totalElapsed = timerTotal.Lap("Total");
            long currentMemory = GC.GetTotalMemory(false);
            long memoryUsed = Math.Max(0, currentMemory - memoryBefore);

            result.ExecutionTime = TimeSpan.FromMilliseconds(totalElapsed);
            result.MemoryUsedBytes = memoryUsed;

            s_logger.WriteDebug($"Base36 optimal: Threshold={result.Base36Result.Threshold:F2}, Count={result.Base36Result.Count}");
            s_logger.WriteDebug($"Base62 optimal: Threshold={result.Base62Result.Threshold:F2}, Count={result.Base62Result.Count}");
            s_logger.WriteDebug($"Memory used: {memoryUsed / 1024.0 / 1024.0:F2} MB");
        }

        context.OperationContext?.ReportProgress(100);

        s_logger.WriteDebug("=== Clearing audio cache ===");
        if (context.AudioCache != null)
        {
            CleanupAudioCache(context.FileListItems, context.AudioCache);
        }
        context.FileListItems.Clear();

        return result;
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

        /// <summary>メモリ使用量（バイト）。</summary>
        public long MemoryUsedBytes { get; set; }
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

        var timerTotal = s_logger.StartTimer();
        var timer = s_logger.StartTimer();
        long memoryBefore = GC.GetTotalMemory(false);

        // 音声データの事前ロード（キャッシュ構築）
        // スレッド管理（Top-Level Offloading）はViewModel層で行うため、ここではTask.Runを使用しない
        var (FailedFiles, Cache) = AudioCacheManager.PreloadAudioData(fileList, options.OperationContext);
        var audioCache = Cache;
        s_logger.WriteDebug($"AudioCacheManager.PreloadAudioData: {timer.Lap("AudioCacheManager.PreloadAudioData")} ms");

        // DefinitionReuse expects an ObservableCollection, so we need to convert
        ObservableCollection<BmsAudioFile> observableCollection = new(fileList);
        DefinitionReuse dr = new(observableCollection, audioCache, options.InputBmsContent);
        s_logger.WriteDebug($"DefinitionReuse constructor: {timer.Lap("DefinitionReuse constructor")} ms");

        var originalCount = fileList.Count;
        var optimizedCount = originalCount;
        var deletedFilesCount = 0;
        ReductionResult errorResult(string message)
        {
            s_logger.WriteDebug($"ERROR in ExecuteDefinitionReductionAsync: {message}");
            return new ReductionResult
            {
                OriginalCount = originalCount,
                OptimizedCount = optimizedCount,
                ReductionRate = 0,
                ProcessingTime = TimeSpan.FromMilliseconds(timerTotal.Lap("Total")),
                Threshold = options.R2Threshold,
                IsSuccess = false,
                ErrorMessage = message,
                MemoryUsedBytes = 0
            };
        }

        try
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
                    SelectedKeywords = options.SelectedKeywords,
                    OperationContext = options.OperationContext
                });

            // 物理削除処理
            if (options.IsPhysicalDeletionEnabled)
            {
                var timerDelete = s_logger.StartTimer();
                List<string> unusedFiles = dr.GetUnusedFilePaths();
                deletedFilesCount = DeleteUnusedFiles(unusedFiles);
                s_logger.WriteDebug($"DeleteUnusedFiles: {timerDelete.Lap("DeleteUnusedFiles")} ms");
            }
            s_logger.WriteDebug($"dr.ReductDefinition total: {timer.Lap("dr.ReductDefinition total")} ms");

            var totalElapsed = timerTotal.Lap("Total");
            long memoryUsed = Math.Max(0, GC.GetTotalMemory(false) - memoryBefore);
            s_logger.WriteDebug($"=== ExecuteDefinitionReductionAsync: Complete ({totalElapsed}ms) ===");

            optimizedCount = dr.GetUniqueFileCount();
            var reductionRate = CalculateReductionRate(originalCount, optimizedCount);

            CleanupAudioCache(fileList, audioCache);
            s_logger.WriteDebug($"CleanupAudioCache: {timer.Lap("CleanupAudioCache")} ms");

            return new ReductionResult
            {
                OriginalCount = originalCount,
                OptimizedCount = optimizedCount,
                ReductionRate = reductionRate,
                ProcessingTime = TimeSpan.FromMilliseconds(totalElapsed),
                Threshold = options.R2Threshold,
                IsSuccess = true,
                DeletedFilesCount = deletedFilesCount,
                MemoryUsedBytes = memoryUsed
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
        catch (AggregateException ae) when (ae.InnerExceptions.Any(e => e is OperationCanceledException))
        {
            throw new OperationCanceledException("Operation was canceled.", ae);
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
    private static void CleanupAudioCache(IEnumerable<BmsAudioFile>? files, ConcurrentDictionary<string, ICachedSoundData> audioCache)
    {
        if (files == null || audioCache == null) return;

        int clearedCount = 0;
        foreach (BmsAudioFile file in files)
        {
            if (audioCache.TryGetValue(file.Name, out var cachedData))
            {
                // PointerSoundDataはセッション中に自動でライフサイクル管理されるためDisposeしない
                // AudioRegistry.Instanceに登録済みのデータはキャッシュとして保持するためDisposeしない
                if (cachedData is not Models.PointerSoundData &&
                    !AudioRegistry.Instance.TryGet(file.Name, out _))
                {
                    cachedData.Dispose();
                }
                clearedCount++;
            }
        }
        s_logger.WriteDebug($"Cleared {clearedCount} cached audio files");
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
                    s_logger.WriteDebug($"Deleted unused file: {file}");
                }
                else
                {
                    s_logger.WriteDebug($"File to delete not found: {file}");
                }
            }
            catch (Exception ex)
            {
                s_logger.WriteDebug($"Failed to delete unused file: {file}. Error: {ex.Message}");
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

