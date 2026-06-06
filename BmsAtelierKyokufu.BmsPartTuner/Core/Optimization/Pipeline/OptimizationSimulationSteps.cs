namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization.Pipeline;

/// <summary>
/// 実際のファイル数を数え、有効なファイルのリストを作成するステップ。
/// </summary>
internal sealed class LoadValidFilesStep : IAsyncOptimizationStep
{
    private static readonly Logger<LoadValidFilesStep> s_logger = new();
    public string Name => PipelineStepHelper.GetStepName(nameof(LoadValidFilesStep));
    public Task ExecuteAsync(OptimizationSimulationContext context)
    {
        int actualEndDefinition = context.EndDefinition;
        if (actualEndDefinition == 0)
        {
            actualEndDefinition = context.StartDefinition + context.FilePaths.Count - 1;
            s_logger.WriteDebug($"Auto-detected end definition: {actualEndDefinition}");
        }

        int fileNum = context.StartDefinition;
        int radix = actualEndDefinition > AppConstants.Definition.MaxNumberBase36
            ? AppConstants.Definition.RadixBase62
            : AppConstants.Definition.RadixBase36;

        int limit = (radix * radix) - 1;

        foreach (string filePath in context.FilePaths)
        {
            if (File.Exists(filePath))
            {
                string numStr = fileNum <= limit ? RadixConvert.IntToZZ(fileNum, radix) : "XX";
                BmsAudioFile wavFile = new()
                {
                    Num = numStr,
                    NumInteger = fileNum,
                    Name = filePath,
                    FileSize = new FileInfo(filePath).Length
                };
                context.FileListItems.Add(wavFile);
                fileNum++;

                if (fileNum > actualEndDefinition)
                    break;
            }
        }

        int originalCount = context.FileListItems.Count;
        if (originalCount == 0)
        {
            // 有効なファイルが見つからない場合はパイプラインを中断する
            // (BmsOptimizationService 側で事前チェック済みのため通常ここには到達しない)
            s_logger.WriteDebug("LoadValidFilesStep: No valid files found. Skipping pipeline.");
            return Task.CompletedTask;
        }

        context.EndDefinition = context.StartDefinition + originalCount - 1;
        s_logger.WriteDebug($"Valid files loaded: {originalCount}, Actual range: {context.StartDefinition}-{context.EndDefinition}");

        return Task.CompletedTask;
    }
}

/// <summary>
/// 音声データをプリロードしてキャッシュを構築するステップ。
/// </summary>
internal sealed class PreloadAudioCacheStep : IAsyncOptimizationStep
{
    private static readonly Logger<PreloadAudioCacheStep> s_logger = new();
    public string Name => PipelineStepHelper.GetStepName(nameof(PreloadAudioCacheStep));
    public Task ExecuteAsync(OptimizationSimulationContext context)
    {
        context.OperationContext?.ReportProgress(5);

        var (failedFiles, audioCache) = AudioCacheManager.PreloadAudioData(
            context.FileListItems,
            context.OperationContext, // Not mapping partial progress directly, rely on ThrottledProgress in ViewModel or modify OperationContext wrapper later if needed
            NormalizationMode.None,
            extractFeatures: false);

        context.FailedFiles = failedFiles;
        context.AudioCache = audioCache;

        if (failedFiles.Count > 0)
        {
            s_logger.WriteDebug($"WARNING: {failedFiles.Count} files failed to load");
            foreach (string? file in failedFiles.Take(5))
            {
                s_logger.WriteDebug($"  - {Path.GetFileName(file)}");
            }
            if (failedFiles.Count > 5)
            {
                s_logger.WriteDebug($"  ... and {failedFiles.Count - 5} more");
            }
        }

        context.OperationContext?.ReportProgress(10);
        return Task.CompletedTask;
    }
}

/// <summary>
/// シミュレーションエンジンを用いて並列シミュレーションを実行するステップ。
/// </summary>
internal sealed class RunParallelSimulationStep : IAsyncOptimizationStep
{
    private static readonly Logger<RunParallelSimulationStep> s_logger = new();
    public string Name => PipelineStepHelper.GetStepName(nameof(RunParallelSimulationStep));
    public async Task ExecuteAsync(OptimizationSimulationContext context)
    {
        if (context.AudioCache == null)
            throw new InvalidOperationException("AudioCache is not initialized.");

        SimulationEngine simulationEngine = new(
            context.FileListItems,
            context.AudioCache,
            context.StartDefinition,
            context.EndDefinition);

        var simulationResults = simulationEngine.RunParallelSimulation(
            0.00f,      // 最小しきい値
            1.00f,      // 最大しきい値
            0.01f,      // ステップ
            context.OperationContext);

        context.SimulationResults = simulationResults;

        context.SimulationData = [.. simulationResults.Select(r => ((double)r.Threshold, r.FileCount))];

        s_logger.WriteDebug($"Simulation results: {context.SimulationData.Count} data points");
        if (context.SimulationData.Count > 0)
        {
            int minCount = context.SimulationData.Min(d => d.FileCount);
            int maxCount = context.SimulationData.Max(d => d.FileCount);
            s_logger.WriteDebug($"File count range in results: {minCount} - {maxCount}");
        }

        context.OperationContext?.ReportProgress(70);
    }
}

/// <summary>
/// シミュレーション結果から最適なエルボーポイント（しきい値）を算出するステップ。
/// </summary>
internal sealed class FindOptimalThresholdsStep : IAsyncOptimizationStep
{
    private static readonly Logger<FindOptimalThresholdsStep> s_logger = new();
    public string Name => PipelineStepHelper.GetStepName(nameof(FindOptimalThresholdsStep));
    public Task ExecuteAsync(OptimizationSimulationContext context)
    {
        context.OperationContext?.ReportProgress(75);

        // Base36とBase62の最適値を探索
        (float base36Threshold, int base36Count) = FindOptimalThreshold(context.SimulationData, AppConstants.Definition.MaxNumberBase36);
        (float base62Threshold, int base62Count) = FindOptimalThreshold(context.SimulationData, AppConstants.Definition.MaxNumberBase62);

        // Base62が見つからない場合はBase36と同じ値を使用（フォールバック）
        if (base62Count == 0 || base62Threshold < base36Threshold)
        {
            s_logger.WriteDebug("Base62: Using Base36 threshold as fallback (no better option found)");
            base62Threshold = base36Threshold;
            base62Count = base36Count;
        }

        s_logger.WriteDebug($"Base36: Threshold={base36Threshold:F2}, Count={base36Count}");
        s_logger.WriteDebug($"Base62: Threshold={base62Threshold:F2}, Count={base62Count}");

        context.Result = new OptimizationResult
        {
            Base36Result = (base36Threshold, base36Count),
            Base62Result = (base62Threshold, base62Count),
            SimulationData = context.SimulationData
        };

        if (context.FailedFiles.Count > 0)
        {
            string warningMessage = context.FailedFiles.Count == 1
                ? $"1 件の音声ファイルが読み込めなかったため、最適化から除外されました:\n{Path.GetFileName(context.FailedFiles[0])}"
                : $"{context.FailedFiles.Count} 件の音声ファイルが読み込めなかったため、最適化から除外されました。";
            context.Result.Warnings.Add(warningMessage);
            s_logger.WriteDebug($"Added warning to result: {warningMessage}");
        }

        context.OperationContext?.ReportProgress(85);
        return Task.CompletedTask;
    }

    private static (float Threshold, int Count) FindOptimalThreshold(
        List<(double Threshold, int Count)> simulationData,
        int fileLimit)
    {
        if (simulationData == null || simulationData.Count == 0)
        {
            return (0.60f, 0);
        }

        var validEntries = simulationData.Where(d => d.Count > 0 && d.Count <= fileLimit).ToList();

        if (validEntries.Count == 0)
        {
            var nonZeroEntries = simulationData.Where(d => d.Count > 0).ToList();
            if (nonZeroEntries.Count == 0)
            {
                return (0.60f, 0);
            }
            var (Threshold, Count) = nonZeroEntries.OrderBy(d => d.Count).First();
            return ((float)Threshold, Count);
        }

        var optimalEntry = validEntries.OrderByDescending(d => d.Threshold).First();
        return ((float)optimalEntry.Threshold, optimalEntry.Count);
    }
}

