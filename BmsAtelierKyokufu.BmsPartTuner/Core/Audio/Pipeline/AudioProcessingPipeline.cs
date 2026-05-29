namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Pipeline;

/// <summary>
/// 音声処理パイプライン。
/// 登録された複数の処理ステップを順次実行し、時間計測や進捗報告を自動化します。
/// </summary>
[ADRAnchor("ARCH-01", nameof(AudioProcessingPipeline))]
internal sealed class AudioProcessingPipeline
{
    private static readonly IPerformanceLogger s_logger = new TypedLogger(typeof(AudioProcessingPipeline));
    private readonly List<IAudioProcessingStep> _steps = [];

    /// <summary>
    /// パイプラインに処理ステップを追加します。
    /// </summary>
    public AudioProcessingPipeline AddStep(IAudioProcessingStep step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// 指定されたコンテキストでパイプラインを実行します。
    /// </summary>
    /// <param name="context">実行コンテキスト</param>
    public PreNormalizedSoundData Execute(AudioProcessingContext context)
    {
        var timerTotal = PerformanceDebugLogger.StartTimer();
        var timerStep = PerformanceDebugLogger.StartTimer();

        foreach (var step in _steps)
        {
            timerStep.Lap(step.Name);
            
            step.Execute(context);
            
            s_logger.WriteDebug($"{step.Name}: {timerStep.Lap(step.Name)} ms");
        }

        long totalElapsed = timerTotal.Lap("Total");
        s_logger.WriteDebug($"=== AudioProcessingPipeline completed in {totalElapsed} ms ===");

        return context.Result ?? throw new InvalidOperationException("Pipeline completed without producing a result.");
    }
}
