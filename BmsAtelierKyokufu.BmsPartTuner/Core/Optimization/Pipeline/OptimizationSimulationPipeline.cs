namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization.Pipeline;

/// <summary>
/// 非同期最適化シミュレーションパイプライン。
/// 登録された複数の非同期ステップを順次実行します。
/// </summary>
[ADRAnchor("ARCH-01", nameof(OptimizationSimulationPipeline))]
internal sealed class OptimizationSimulationPipeline
{
    private readonly List<IAsyncOptimizationStep> _steps = new();

    public OptimizationSimulationPipeline AddStep(IAsyncOptimizationStep step)
    {
        _steps.Add(step);
        return this;
    }

    public async Task<Models.OptimizationResult?> ExecuteAsync(OptimizationSimulationContext context)
    {
        PerformanceDebugLogger.WriteDebug(nameof(OptimizationSimulationPipeline), "=== Async Pipeline Starting ===");
        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timerStep = PerformanceDebugLogger.StartTimer();

        try
        {
            foreach (var step in _steps)
            {
                PerformanceDebugLogger.WriteDebug(nameof(OptimizationSimulationPipeline), $"--- Step: {step.Name} ---");
                timerStep.Lap(step.Name);
                await step.ExecuteAsync(context);
                PerformanceDebugLogger.WriteDebug(nameof(OptimizationSimulationPipeline), $"{step.Name} completed in {timerStep.Lap(step.Name)} ms");
            }
        }
        catch (Exception ex)
        {
            PerformanceDebugLogger.WriteDebug(nameof(OptimizationSimulationPipeline), $"ERROR in pipeline execution: {ex.Message}");
            PerformanceDebugLogger.WriteDebug(nameof(OptimizationSimulationPipeline), $"StackTrace: {ex.StackTrace}");
            return null;
        }
        finally
        {
            // 音声キャッシュなどのクリーンアップは別ステップで行うか、ここで行うことも可能
            // 今回はクリーンアップステップをパイプラインの最後に登録する想定
            long totalElapsed = timerTotal.Lap("Total");
            PerformanceDebugLogger.WriteDebug(nameof(OptimizationSimulationPipeline), $"=== Async Pipeline Complete ({totalElapsed}ms) ===");
        }

        return context.Result;
    }
}
