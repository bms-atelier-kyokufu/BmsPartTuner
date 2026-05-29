namespace BmsAtelierKyokufu.BmsPartTuner.Core.Optimization.Pipeline;

/// <summary>
/// 非同期最適化シミュレーションパイプライン。
/// 登録された複数の非同期ステップを順次実行します。
/// </summary>
[ADRAnchor("ARCH-01", nameof(OptimizationSimulationPipeline))]
internal sealed class OptimizationSimulationPipeline
{
    private readonly List<IAsyncOptimizationStep> _steps = [];

    public OptimizationSimulationPipeline AddStep(IAsyncOptimizationStep step)
    {
        _steps.Add(step);
        return this;
    }

    public async Task<OptimizationResult?> ExecuteAsync(OptimizationSimulationContext context)
    {
        PerformanceDebugLogger<OptimizationSimulationPipeline>.WriteDebug("=== Async Pipeline Starting ===");
        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timerStep = PerformanceDebugLogger.StartTimer();

        try
        {
            foreach (var step in _steps)
            {
                PerformanceDebugLogger<OptimizationSimulationPipeline>.WriteDebug($"--- Step: {step.Name} ---");
                timerStep.Lap(step.Name);
                await step.ExecuteAsync(context);
                PerformanceDebugLogger<OptimizationSimulationPipeline>.WriteDebug($"{step.Name} completed in {timerStep.Lap(step.Name)} ms");
            }
        }
        catch (Exception ex)
        {
            PerformanceDebugLogger<OptimizationSimulationPipeline>.WriteDebug($"ERROR in pipeline execution: {ex.Message}");
            PerformanceDebugLogger<OptimizationSimulationPipeline>.WriteDebug($"StackTrace: {ex.StackTrace}");
            return null;
        }
        finally
        {
            // 音声キャッシュなどのクリーンアップは別ステップで行うか、ここで行うことも可能
            // 今回はクリーンアップステップをパイプラインの最後に登録する想定
            long totalElapsed = timerTotal.Lap("Total");
            PerformanceDebugLogger<OptimizationSimulationPipeline>.WriteDebug($"=== Async Pipeline Complete ({totalElapsed}ms) ===");
        }

        return context.Result;
    }
}
