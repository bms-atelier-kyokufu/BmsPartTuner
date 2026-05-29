namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Pipeline;

/// <summary>
/// 音声処理パイプラインの各ステップが実装するインターフェース。
/// </summary>
internal interface IAudioProcessingStep
{
    /// <summary>
    /// ステップの表示名（パフォーマンス計測・ログ用）。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// ステップの処理を実行します。
    /// </summary>
    /// <param name="context">パイプラインの実行コンテキスト</param>
    void Execute(AudioProcessingContext context);
}
