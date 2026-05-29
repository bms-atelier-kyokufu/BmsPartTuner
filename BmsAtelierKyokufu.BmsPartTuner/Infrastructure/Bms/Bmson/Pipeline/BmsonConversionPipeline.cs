namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson.Pipeline;

/// <summary>
/// BMSON変換パイプライン。
/// 登録された複数の変換ステップを順次実行し、時間・メモリなどの計測を自動化します。
/// </summary>
public sealed class BmsonConversionPipeline
{
    private readonly List<IBmsonConversionStep> _steps = new();

    /// <summary>
    /// パイプラインに処理ステップを追加します。
    /// </summary>
    /// <param name="step">追加するステップ</param>
    /// <returns>自分自身のインスタンス（メソッドチェーン用）</returns>
    public BmsonConversionPipeline AddStep(IBmsonConversionStep step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// 指定されたコンテキストでパイプラインを実行します。
    /// </summary>
    /// <param name="context">実行コンテキスト</param>
    /// <returns>生成されたBMSテキスト</returns>
    public string Execute(BmsonConversionContext context)
    {
        PerformanceDebugLogger.Clear();
        PerformanceDebugLogger.WriteDebug(nameof(BmsonConversionPipeline), "=== Downconvert started ===");
        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timerStep = PerformanceDebugLogger.StartTimer();

        foreach (var step in _steps)
        {
            // 各ステップ実行前にタイマーをリセットし、計測を開始
            timerStep.Lap(step.Name);
            
            step.Execute(context);
            
            PerformanceDebugLogger.WriteDebug(nameof(BmsonConversionPipeline), $"{step.Name}: {timerStep.Lap(step.Name)} ms");
        }

        PerformanceDebugLogger.WriteDebug(nameof(BmsonConversionPipeline), $"=== Downconvert finished. Total: {timerTotal.Lap("Total")} ms ===");

        return context.ResultBmsText ?? throw new InvalidOperationException("Pipeline completed without producing a result.");
    }
}
