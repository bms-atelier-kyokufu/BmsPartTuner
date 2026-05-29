namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms.Pipeline;

/// <summary>
/// BMS定義削減パイプライン。
/// 登録された複数の処理ステップを順次実行し、時間計測や進捗報告を自動化します。
/// </summary>
[ADRAnchor("ARCH-01", nameof(DefinitionReductionPipeline))]
internal sealed class DefinitionReductionPipeline
{
    private readonly List<IDefinitionReductionStep> _steps = new();

    /// <summary>
    /// パイプラインに処理ステップを追加します。
    /// </summary>
    public DefinitionReductionPipeline AddStep(IDefinitionReductionStep step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// 指定されたコンテキストでパイプラインを実行します。
    /// </summary>
    /// <param name="context">実行コンテキスト</param>
    public void Execute(DefinitionReductionContext context)
    {
        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timerStep = PerformanceDebugLogger.StartTimer();

        var progress = context.Options.Progress ?? new Progress<int>();
        progress.Report(0);

        foreach (var step in _steps)
        {
            timerStep.Lap(step.Name);

            step.Execute(context);

            PerformanceDebugLogger.WriteDebug(nameof(DefinitionReductionPipeline), $"{step.Name}: {timerStep.Lap(step.Name)} ms");
        }

        progress.Report(AppConstants.Progress.Complete);

        long totalElapsed = timerTotal.Lap("Total");
        PerformanceDebugLogger.WriteDebug(nameof(DefinitionReductionPipeline), $"=== DefinitionReductionPipeline completed in {totalElapsed} ms ({totalElapsed / 1000.0:F2}s) ===");
    }
}
