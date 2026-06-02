namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Processing.Pipeline;

/// <summary>
/// 音声処理パイプライン。
/// 登録された複数の処理ステップを順次実行し、時間計測や進捗報告を自動化します。
/// </summary>
[ADRAnchor("ARCH-01", nameof(AudioProcessingPipeline))]
internal sealed class AudioProcessingPipeline
{
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
        foreach (var step in _steps)
        {
            step.Execute(context);
        }

        return context.Result ?? throw new InvalidOperationException("Pipeline completed without producing a result.");
    }
}


