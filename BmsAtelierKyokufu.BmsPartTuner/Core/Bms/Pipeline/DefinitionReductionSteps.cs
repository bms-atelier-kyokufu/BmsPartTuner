namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms.Pipeline;

/// <summary>
/// 処理範囲を決定し、統計用クラスを初期化するステップ。
/// </summary>
internal sealed class DetermineProcessingRangeStep : IDefinitionReductionStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(DetermineProcessingRangeStep));
    public void Execute(DefinitionReductionContext context)
    {
        context.RangeManager.DetermineProcessingRange(context.Options.StartDefinition, context.Options.EndDefinition);

        // 範囲確定後に統計クラスを再初期化
        context.Statistics = new DefinitionStatistics(
            context.FileList,
            context.Replaces,
            context.RangeManager.StartPoint,
            context.RangeManager.EndPoint);
    }
}

/// <summary>
/// 音声データをプリロードしてキャッシュを構築するステップ。
/// </summary>
internal sealed class PreloadAudioDataStep : IDefinitionReductionStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(PreloadAudioDataStep));
    public void Execute(DefinitionReductionContext context)
    {
        var opContext = context.Options.OperationContext;

        var (_, loadedCache) = AudioCacheManager.PreloadAudioData(
            context.FileList,
            opContext,
            context.NormalizationMode);

        context.AudioCache = loadedCache;
        opContext?.ReportProgress(AppConstants.Progress.PreloadComplete);
    }
}

/// <summary>
/// キャッシュされた音声データを比較し、重複を検知して置換テーブルを構築するステップ。
/// </summary>
internal sealed class CreateReplaceTableStep : IDefinitionReductionStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(CreateReplaceTableStep));
    public void Execute(DefinitionReductionContext context)
    {
        var opContext = context.Options.OperationContext;

        // ファイルをグループ化して比較対象を絞り込む
        var groups = AudioFileGroupingStrategy.GroupFiles(
            context.AudioCache,
            context.FileList,
            context.RangeManager.StartPoint,
            context.RangeManager.EndPoint,
            context.Options.SelectedKeywords);

        // 並列比較エンジンを実行
        var parameters = new AudioComparisonParameters(
            context.FileList,
            context.AudioCache,
            context.Replaces,
            context.RangeManager.StartPoint,
            context.RangeManager.EndPoint);

        var comparisonEngine = new ParallelAudioComparisonEngine(parameters);
        comparisonEngine.CompareGroups(groups, context.Options.R2Threshold, opContext);

        opContext?.ReportProgress(AppConstants.Progress.ComparisonComplete);
    }
}

/// <summary>
/// 置換テーブルを元にBMSファイルを実際に書き換えるステップ。
/// </summary>
internal sealed class RewriteBmsFileStep : IDefinitionReductionStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(RewriteBmsFileStep));
    public void Execute(DefinitionReductionContext context)
    {
        var opContext = context.Options.OperationContext;

        context.Rewriter = new BmsFileRewriter(
            context.FileList,
            context.Replaces,
            context.RangeManager.StartPoint,
            context.RangeManager.EndPoint,
            context.InputBmsContent);

        context.RewriteData = context.Rewriter.ReplaceAndAlignBmsFile(context.InputBmsFileName);

        opContext?.ReportProgress(AppConstants.Progress.RewriteComplete);
    }
}

/// <summary>
/// 書き換えられたBMSファイルデータをディスクに保存し、メモリスライスをフラッシュするステップ。
/// </summary>
internal sealed class WriteAndFlushToDiskStep : IDefinitionReductionStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(WriteAndFlushToDiskStep));
    public void Execute(DefinitionReductionContext context)
    {
        if (context.RewriteData == null || context.Rewriter == null) return;

        // BMSファイルの書き込み
        BmsFileWriter.WriteBmsFile(context.OutputSaveFileName, context.RewriteData);

        // メモリ上のスライスを物理ディスクに書き出す
        string outDir = Path.GetDirectoryName(context.OutputSaveFileName) ?? string.Empty;
        if (string.IsNullOrEmpty(outDir)) return;

        foreach (var file in context.Rewriter.KeptFiles)
        {
            var fileName = Path.GetFileName(file.Name);
            if (VirtualAudioRegistry.TryGetStream(fileName, out var stream))
            {
                using (stream)
                {
                    var targetPath = Path.Combine(outDir, fileName);
                    using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: false);
                    stream.CopyTo(fs);
                }
            }
            else if (VirtualAudioRegistry.TryGetFile(fileName, out var data))
            {
                var targetPath = Path.Combine(outDir, fileName);
                File.WriteAllBytes(targetPath, data);
            }
        }
    }
}

/// <summary>
/// 不要になった音源ファイルを物理的に削除するステップ。
/// </summary>
internal sealed class PhysicalDeletionStep : IDefinitionReductionStep
{
    private static readonly Logger<PhysicalDeletionStep> s_logger = new();
    public string Name => PipelineStepHelper.GetStepName(nameof(PhysicalDeletionStep));
    public void Execute(DefinitionReductionContext context)
    {
        if (!context.Options.IsPhysicalDeletionEnabled || context.Rewriter == null) return;

        var unusedFiles = context.FileList.Except(context.Rewriter.KeptFiles).ToList();
        s_logger.WriteDebug($"=== Physical Deletion: {unusedFiles.Count} files to delete ===");

        int deletedCount = 0;
        foreach (var file in unusedFiles)
        {
            try
            {
                if (File.Exists(file.Name))
                {
                    File.Delete(file.Name);
                    deletedCount++;
                    s_logger.WriteDebug($"Deleted: {file.Name}");
                }
            }
            catch (IOException ex)
            {
                s_logger.WriteDebug($"Failed to delete {file.Name} (IO Error): {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                s_logger.WriteDebug($"Failed to delete {file.Name} (Access Denied): {ex.Message}");
            }
        }
        s_logger.WriteDebug($"=== Physical Deletion Complete: {deletedCount}/{unusedFiles.Count} files deleted ===");
    }
}

/// <summary>
/// 定義削減の統計情報をログに出力するステップ。
/// </summary>
internal sealed class LogStatisticsStep : IDefinitionReductionStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(LogStatisticsStep));
    public void Execute(DefinitionReductionContext context)
    {
        context.Statistics?.LogStatistics();
    }
}

