using System.Diagnostics;
using BmsAtelierKyokufu.BmsPartTuner.Core;
using static BmsAtelierKyokufu.BmsPartTuner.Models.FileList;

namespace BmsAtelierKyokufu.BmsPartTuner.Audio;

/// <summary>
/// オーディオファイルのキャッシュ管理。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="bullet">
/// <item>全オーディオデータのメモリプリロード</item>
/// <item>バッチ処理による並列ロード</item>
/// <item>メモリ使用量の監視と統計情報出力</item>
/// </list>
/// 
/// <para>【最適化の根拠】</para>
/// <list type="bullet">
/// <item>Before: 2,408,310回の比較 × 2ファイル = 約480万回のディスクI/O</item>
/// <item>After: 2,196ファイルの一括読み込み = 2,196回のディスクI/O</item>
/// <item>結果: 約2,200倍のI/O削減</item>
/// </list>
/// 
/// <para>【メモリ使用量】</para>
/// 2,196ファイル × 200KB ≈ 440MB（現代のPCでは許容範囲）
/// 
/// <para>【バッチ処理戦略】</para>
/// <list type="bullet">
/// <item>CPUコア数に合わせてバッチサイズを動的決定</item>
/// <item>バッチごとに並列処理でスループット最大化</item>
/// <item>進捗レポートはバッチ単位（オーバーヘッド削減）</item>
/// </list>
/// 
/// <para>【Why バッチ並列・内部順次】</para>
/// バッチ間は並列処理でCPUコアを活用し、バッチ内は順次処理でディスク負荷を制御します。
/// ディスクI/Oはランダムアクセスが遅いため、順次アクセスの方が効率的です。
/// </remarks>
internal static class AudioCacheManager
{
    /// <summary>
    /// 全オーディオデータをメモリにプリロード。
    /// </summary>
    /// <param name="fileList">ファイルリスト。</param>
    /// <param name="progress">進捗報告用のIProgress。</param>
    /// <param name="normalizationMode">正規化モード（デフォルト: None）。</param>
    /// <returns>読み込みに失敗したファイルパスのリスト。</returns>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>バッチサイズの動的計算（CPUコア数 × 4）</item>
    /// <item>ファイルリストをバッチに分割</item>
    /// <item>バッチごとに並列処理（Parallel.ForEach）</item>
    /// <item>バッチ内は順次ロード（ディスク負荷制御）</item>
    /// <item>5バッチごとまたは完了時に進捗報告</item>
    /// <item>統計情報をデバッグログに出力</item>
    /// </list>
    /// 
    /// <para>【統計情報】</para>
    /// <list type="bullet">
    /// <item>ロード成功率</item>
    /// <item>総メモリ使用量</item>
    /// <item>スループット（files/sec）</item>
    /// </list>
    /// 
    /// <para>【失敗ファイルの扱い】</para>
    /// 破損ファイルや読み込みに失敗したファイルは無視して処理を続行しますが、
    /// そのファイルパスをリストで返却します。呼び出し元でユーザーに警告を表示できます。
    /// </remarks>
    public static List<string> PreloadAudioData(
        IReadOnlyList<WavFiles> fileList,
        IProgress<int>? progress,
        Models.NormalizationMode normalizationMode = Models.NormalizationMode.None)
    {
        Debug.WriteLine($"=== PreloadAudioData Start ===");
        Debug.WriteLine($"Total files to preload: {fileList.Count}");
        Debug.WriteLine($"Normalization mode: {normalizationMode}");

        int loaded = 0;
        int totalFiles = fileList.Count;
        int successCount = 0;
        int failCount = 0;
        var failedFiles = new System.Collections.Concurrent.ConcurrentBag<string>();

        if (totalFiles == 0)
        {
            Debug.WriteLine("WARNING: No files to preload");
            progress?.Report(AppConstants.Progress.PreloadComplete);
            return new List<string>();
        }

        int batchSize = CalculateOptimalBatchSize(totalFiles);
        var batches = CreateBatches(fileList, batchSize);

        Debug.WriteLine($"Preloading {totalFiles} files in {batches.Count} batches (batch size: ~{batchSize})");

        var sw = Stopwatch.StartNew();

        int completedBatches = 0;

        Parallel.ForEach(batches, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, batch =>
        {
            int batchSuccess = 0;
            int batchFail = 0;

            LoadBatch(batch, ref loaded, ref batchSuccess, ref batchFail, normalizationMode, failedFiles);

            System.Threading.Interlocked.Add(ref successCount, batchSuccess);
            System.Threading.Interlocked.Add(ref failCount, batchFail);

            int currentBatch = System.Threading.Interlocked.Increment(ref completedBatches);

            if (currentBatch % 5 == 0 || currentBatch == batches.Count)
            {
                Debug.WriteLine($"Batch progress: {currentBatch}/{batches.Count} (Success: {successCount}, Fail: {failCount})");
            }

            int percentage = (int)((float)currentBatch / batches.Count * AppConstants.Progress.PreloadComplete);
            progress?.Report(percentage);
        });

        sw.Stop();

        LogCacheStatistics(fileList, loaded, totalFiles, successCount, failCount, sw.ElapsedMilliseconds);

        return failedFiles.ToList();
    }

    /// <summary>
    /// 最適なバッチサイズを計算。
    /// </summary>
    /// <returns>バッチサイズ。</returns>
    /// <remarks>
    /// <para>【計算式】</para>
    /// バッチサイズ = max(10, 総ファイル数 / (CPUコア数 × 4))
    /// 
    /// <para>【例】</para>
    /// 8コアなら32バッチ、2196ファイルなら約69ファイル/バッチ
    /// </remarks>
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
    private static IReadOnlyList<IReadOnlyList<WavFiles>> CreateBatches(
        IReadOnlyList<WavFiles> fileList,
        int batchSize)
    {
        var batches = new List<IReadOnlyList<WavFiles>>();

        for (int i = 0; i < fileList.Count; i += batchSize)
        {
            int remaining = Math.Min(batchSize, fileList.Count - i);
            var batch = new List<WavFiles>();

            for (int j = 0; j < remaining; j++)
            {
                batch.Add(fileList[i + j]);
            }

            batches.Add(batch);
        }

        return batches;
    }

    /// <summary>
    /// バッチ内のファイルをロード。
    /// </summary>
    /// <remarks>
    /// バッチ間は並列、バッチ内は順次でディスク負荷を制御します。
    /// </remarks>
    private static void LoadBatch(
        IReadOnlyList<WavFiles> batch,
        ref int loaded,
        ref int successCount,
        ref int failCount,
        Models.NormalizationMode normalizationMode,
        System.Collections.Concurrent.ConcurrentBag<string> failedFiles)
    {
        foreach (var file in batch)
        {
            try
            {
                file.PreloadCache(normalizationMode);
                System.Threading.Interlocked.Increment(ref loaded);

                if (file.CachedData != null)
                {
                    System.Threading.Interlocked.Increment(ref successCount);
                }
                else
                {
                    Debug.WriteLine($"[AudioCacheManager] Preload failed: {Path.GetFileName(file.Name)}");
                    System.Threading.Interlocked.Increment(ref failCount);
                    failedFiles.Add(file.Name);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioCacheManager] Exception loading {Path.GetFileName(file.Name)}: {ex.Message}");
                System.Threading.Interlocked.Increment(ref failCount);
                failedFiles.Add(file.Name);
            }
        }
    }

    /// <summary>
    /// キャッシュ統計のログ出力。
    /// </summary>
    /// <remarks>
    /// <para>【出力項目】</para>
    /// <list type="bullet">
    /// <item>処理したファイル数</item>
    /// <item>成功・失敗数</item>
    /// <item>キャッシュ成功率</item>
    /// <item>総メモリ使用量（MB）</item>
    /// <item>ロード時間（ms）</item>
    /// <item>スループット（files/sec）</item>
    /// </list>
    /// </remarks>
    private static void LogCacheStatistics(
        IReadOnlyList<WavFiles> fileList,
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
            var cached = fileList[i].CachedData;
            if (cached != null)
            {
                totalMemoryMB += cached.EstimatedMemoryMB;
                cachedCount++;
            }
        }

        Debug.WriteLine($"=== PreloadAudioData Complete ===");
        Debug.WriteLine($"Preload completed: {loaded}/{totalFiles} files processed");
        Debug.WriteLine($"Success: {successCount}, Failed: {failCount}");
        Debug.WriteLine($"Actual cached count: {cachedCount}");
        Debug.WriteLine($"Cache success rate: {(totalFiles > 0 ? (double)cachedCount / totalFiles * 100 : 0):F1}%");
        Debug.WriteLine($"Total cached memory: {totalMemoryMB:F2} MB");
        Debug.WriteLine($"Load time: {elapsedMs} ms");
        Debug.WriteLine($"Throughput: {(elapsedMs > 0 ? (double)loaded / elapsedMs * 1000 : 0):F1} files/sec");

        if (cachedCount == 0)
        {
            Debug.WriteLine("CRITICAL ERROR: No audio data cached! This will cause 0% reduction rate.");
        }
        else if (cachedCount < totalFiles * 0.9)
        {
            Debug.WriteLine($"WARNING: Only {(double)cachedCount / totalFiles * 100:F1}% of files cached successfully");
        }
    }
}
