namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Processing;

/// <summary>
/// オーディオファイルのキャッシュ管理を行います。
/// 全オーディオデータをメモリにプリロードし、CPUコア数に応じたバッチ処理による並列ロードを行うことで、
/// 効率的なメモリ管理とディスクI/Oの大幅な削減を実現します。
/// </summary>
internal sealed class AudioCacheManager
{
    private AudioCacheManager() { }

    private static readonly Logger<AudioCacheManager> s_logger = new();

    /// <summary>
    /// 全オーディオデータをメモリにプリロードします。
    /// バッチごとに並列処理しつつ、バッチ内では順次ロードすることでディスク負荷を制御し、
    /// 読み込みに失敗したファイルは無視して処理を続行しますが、そのパスのリストを返却します。
    /// </summary>
    /// <param name="fileList">ファイルリスト。</param>
    /// <param name="normalizationMode">正規化モード（デフォルト: None）。</param>
    /// <param name="extractFeatures">特徴量抽出を行うかどうか。</param>
    /// <returns>読み込みに失敗したファイルパスのリストと、オーディオキャッシュのタプル。</returns>
    public static (List<string> FailedFiles, ConcurrentDictionary<string, ICachedSoundData> Cache) PreloadAudioData(
        IReadOnlyList<BmsAudioFile> fileList,
        IOperationContext? opContext = null,
        NormalizationMode normalizationMode = Models.NormalizationMode.None,
        bool extractFeatures = true)
    {
        s_logger.WriteDebug("=== PreloadAudioData Start ===");
        s_logger.WriteDebug($"Total files to preload: {fileList.Count}");
        s_logger.WriteDebug($"Normalization mode: {normalizationMode}");

        int loaded = 0;
        int totalFiles = fileList.Count;
        int successCount = 0;
        int failCount = 0;
        var failedFiles = new ConcurrentBag<string>();
        var audioCache = new ConcurrentDictionary<string, ICachedSoundData>();

        if (totalFiles == 0)
        {
            s_logger.WriteDebug("WARNING: No files to preload");
            opContext?.ReportProgress(AppConstants.Progress.PreloadComplete);
            return (new List<string>(), audioCache);
        }

        bool isSsd = false;
        if (fileList.Count > 0)
        {
            isSsd = Helpers.StorageTypeDetector.IsSolidStateDrive(fileList[0].Name);
        }

        s_logger.WriteDebug($"Storage type detected as: {(isSsd ? "SSD (Full Parallel Mode)" : "HDD (Batch Mode)")}");


        var timer = s_logger.StartTimer();

        if (isSsd)
        {
            int processedCount = 0;
            _ = Parallel.ForEach(fileList, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = opContext?.CancellationToken ?? CancellationToken.None
            }, file =>
            {
                opContext?.ThrowIfCancellationRequested();
                var singleBatch = new[] { file };
                var (batchSuccess, batchFail) = LoadBatch(singleBatch, normalizationMode, failedFiles, audioCache, extractFeatures, opContext);

                Interlocked.Add(ref successCount, batchSuccess);
                Interlocked.Add(ref failCount, batchFail);

                int currentCount = Interlocked.Increment(ref processedCount);

                if (currentCount % 100 == 0 || currentCount == totalFiles)
                {
                    s_logger.WriteDebug($"Load progress: {currentCount}/{totalFiles} (Success: {successCount}, Fail: {failCount})");
                }

                int percentage = (int)((float)currentCount / totalFiles * AppConstants.Progress.PreloadComplete);
                opContext?.ReportProgress(percentage);


            });
        }
        else
        {
            int batchSize = CalculateOptimalBatchSize(totalFiles);
            var batches = CreateBatches(fileList, batchSize);

            s_logger.WriteDebug($"Preloading {totalFiles} files in {batches.Count} batches (batch size: ~{batchSize})");

            int completedBatches = 0;

            _ = Parallel.ForEach(batches, new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount),
                CancellationToken = opContext?.CancellationToken ?? CancellationToken.None
            }, batch =>
            {
                opContext?.ThrowIfCancellationRequested();
                var (batchSuccess, batchFail) = LoadBatch(batch, normalizationMode, failedFiles, audioCache, extractFeatures, opContext);

                Interlocked.Add(ref successCount, batchSuccess);
                Interlocked.Add(ref failCount, batchFail);

                int currentBatch = Interlocked.Increment(ref completedBatches);

                if (currentBatch % 5 == 0 || currentBatch == batches.Count)
                {
                    s_logger.WriteDebug($"Batch progress: {currentBatch}/{batches.Count} (Success: {successCount}, Fail: {failCount})");
                }

                int percentage = (int)((float)currentBatch / batches.Count * AppConstants.Progress.PreloadComplete);
                opContext?.ReportProgress(percentage);

                // ループのたびに5秒経過していないかチェックし、経過していれば停止・レポート

            });
        }

        loaded = successCount + failCount;
        LogCacheStatistics(fileList, audioCache, loaded, totalFiles, successCount, failCount, timer.Lap("AudioCacheManager.PreloadAudioData"));

        return (failedFiles.ToList(), audioCache);
    }

    /// <summary>
    /// 総ファイル数とCPUコア数に基づいて最適なバッチサイズを計算します。
    /// </summary>
    /// <returns>バッチサイズ。</returns>
    private static int CalculateOptimalBatchSize(int totalFiles)
    {
        int coreCount = Environment.ProcessorCount;

        int targetBatchCount = coreCount * AppConstants.Cache.BatchSizeDivisor;
        int batchSize = Math.Max(AppConstants.Cache.MinBatchSize, totalFiles / targetBatchCount);

        return batchSize;
    }

    /// <summary>
    /// ファイルリストをバッチに分割。
    /// </summary>
    private static List<IReadOnlyList<BmsAudioFile>> CreateBatches(
        IReadOnlyList<BmsAudioFile> fileList,
        int batchSize)
    {
        var batches = new List<IReadOnlyList<BmsAudioFile>>();

        for (int i = 0; i < fileList.Count; i += batchSize)
        {
            int remaining = Math.Min(batchSize, fileList.Count - i);
            var batch = new List<BmsAudioFile>();

            for (int j = 0; j < remaining; j++)
            {
                batch.Add(fileList[i + j]);
            }

            batches.Add(batch);
        }

        return batches;
    }

    /// <summary>
    /// バッチ内のファイルをロードします。バッチ間は並列、バッチ内は順次でディスク負荷を制御します。
    /// </summary>
    private static (int SuccessCount, int FailCount) LoadBatch(
        IReadOnlyList<BmsAudioFile> batch,
        NormalizationMode normalizationMode,
        ConcurrentBag<string> failedFiles,
        ConcurrentDictionary<string, ICachedSoundData> audioCache,
        bool extractFeatures,
        IOperationContext? opContext)
    {
        int success = 0;
        int fail = 0;
        foreach (var file in batch)
        {
            opContext?.ThrowIfCancellationRequested();
            try
            {
                if (AudioRegistry.Instance.TryGet(file.Name, out var cachedData))
                {
                    // キャッシュヒット（PointerSoundData または PreNormalizedSoundData）
                    audioCache[file.Name] = cachedData!;
                    success++;
                }
                else
                {
                    var newCachedData = AudioProcessingService.LoadAndProcess(file.Name, normalizationMode, extractFeatures);
                    audioCache[file.Name] = newCachedData;
                    AudioRegistry.Instance.Register(file.Name, newCachedData);
                    success++;
                }
            }
            catch (Exception ex)
            {
                s_logger.WriteDebug($"[AudioCacheManager] Exception loading {Path.GetFileName(file.Name)}: {ex.Message}");
                fail++;
                failedFiles.Add(file.Name);
            }
        }
        return (success, fail);
    }

    /// <summary>
    /// 処理したファイル数、成功率、総メモリ使用量、スループット等のキャッシュ統計をログに出力します。
    /// </summary>
    private static void LogCacheStatistics(
        IReadOnlyList<BmsAudioFile> fileList,
        ConcurrentDictionary<string, ICachedSoundData> audioCache,
        int loaded,
        int totalFiles,
        int successCount,
        int failCount,
        long elapsedMs)
    {
        double totalMemoryMB = 0;
        int cachedCount = 0;

        for (int i = 0; i < fileList.Count; i++)
        {
            audioCache.TryGetValue(fileList[i].Name, out var cached);
            if (cached != null)
            {
                totalMemoryMB += cached.EstimatedMemoryMB;
                cachedCount++;
            }
        }

        s_logger.WriteDebug("=== PreloadAudioData Complete ===");
        s_logger.WriteDebug($"Preload completed: {loaded}/{totalFiles} files processed");
        s_logger.WriteDebug($"Success: {successCount}, Failed: {failCount}");
        s_logger.WriteDebug($"Actual cached count: {cachedCount}");
        s_logger.WriteDebug($"Cache success rate: {(totalFiles > 0 ? (double)cachedCount / totalFiles * 100 : 0):F1}%");
        s_logger.WriteDebug($"Total cached memory: {totalMemoryMB:F2} MB");
        s_logger.WriteDebug($"Load time: {elapsedMs} ms");
        s_logger.WriteDebug($"Throughput: {(elapsedMs > 0 ? (double)loaded / elapsedMs * 1000 : 0):F1} files/sec");

        if (cachedCount == 0)
        {
            s_logger.WriteDebug("CRITICAL ERROR: No audio data cached! This will cause 0% reduction rate.");
        }
        else if (cachedCount < totalFiles * 0.9)
        {
            s_logger.WriteDebug($"WARNING: Only {(double)cachedCount / totalFiles * 100:F1}% of files cached successfully");
        }
    }
}

