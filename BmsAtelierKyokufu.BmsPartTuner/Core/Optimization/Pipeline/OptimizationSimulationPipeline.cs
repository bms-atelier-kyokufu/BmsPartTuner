namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization.Pipeline;

/// <summary>
/// 非同期最適化シミュレーションパイプライン。
/// 登録された複数の非同期ステップを順次実行します。
/// </summary>
[ADRAnchor("ARCH-01", nameof(OptimizationSimulationPipeline))]
internal sealed class OptimizationSimulationPipeline
{
    private static readonly Logger<OptimizationSimulationPipeline> s_logger = new();
    private readonly List<IAsyncOptimizationStep> _steps = [];

    public OptimizationSimulationPipeline AddStep(IAsyncOptimizationStep step)
    {
        _steps.Add(step);
        return this;
    }

    public async Task<OptimizationResult?> ExecuteAsync(OptimizationSimulationContext context)
    {
        s_logger.WriteDebug("=== Async Pipeline Starting ===");
        var timerTotal = s_logger.StartTimer();
        var timerStep = s_logger.StartTimer();

        try
        {
            foreach (var step in _steps)
            {
                context.OperationContext?.ThrowIfCancellationRequested();
                s_logger.WriteDebug($"--- Step: {step.Name} ---");
                timerStep.Lap(step.Name);
                await step.ExecuteAsync(context);
                s_logger.WriteDebug($"{step.Name} completed in {timerStep.Lap(step.Name)} ms");
            }
        }
        finally
        {
            // 音声キャッシュなどのクリーンアップは別ステップで行うか、ここで行うことも可能
            // 今回はクリーンアップステップをパイプラインの最後に登録する想定
            long totalElapsed = timerTotal.Lap("Total");
            s_logger.WriteDebug($"=== Async Pipeline Complete ({totalElapsed}ms) ===");
        }

        return context.Result;
    }
}


