namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms.Pipeline;

/// <summary>
/// BMS定義削減パイプライン。
/// 登録された複数の処理ステップを順次実行し、時間計測や進捗報告を自動化します。
/// </summary>
[ADRAnchor("ARCH-01", nameof(DefinitionReductionPipeline))]
internal sealed class DefinitionReductionPipeline
{
    private static readonly Logger<DefinitionReductionPipeline> s_logger = new();
    private readonly List<IDefinitionReductionStep> _steps = [];

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
        var timerTotal = s_logger.StartTimer();
        var timerStep = s_logger.StartTimer();

        var opContext = context.Options.OperationContext;
        opContext?.ReportProgress(0);

        foreach (var step in _steps)
        {
            context.Options.OperationContext?.ThrowIfCancellationRequested();
            timerStep.Lap(step.Name);

            step.Execute(context);

            s_logger.WriteDebug($"{step.Name}: {timerStep.Lap(step.Name)} ms");
        }

        opContext?.ReportProgress(AppConstants.Progress.Complete);

        long totalElapsed = timerTotal.Lap("Total");
        s_logger.WriteDebug($"=== DefinitionReductionPipeline completed in {totalElapsed} ms ({totalElapsed / 1000.0:F2}s) ===");
    }
}


