namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio
{
    /// <summary>
    /// 音声ファイルの読み込み、チャンネル分離、正規化、各種特徴量（RMS、有音区間、LSH）の抽出を行うサービス。
    /// 各ドメイン処理（ファイルI/O、正規化、特徴量抽出）をオーケストレーションし、
    /// PreNormalizedSoundData の生成を担うファサードとして機能します。
    /// </summary>
    [ADRAnchor("OPT-05", nameof(AudioProcessingService))]
    internal static class AudioProcessingService
    {
        public static PreNormalizedSoundData LoadAndProcess(string path, NormalizationMode normalizationMode, bool extractFeatures = true)
        {
            var context = new AudioProcessingContext(path, normalizationMode);
            var pipeline = new AudioProcessingPipeline()
                .AddStep(new LoadAndDeinterleaveStep())
                .AddStep(new ApplyNormalizationStep())
                .AddStep(new ExtractMetricsStep());

            if (extractFeatures)
            {
                pipeline.AddStep(new ExtractFeaturesStep());
            }

            pipeline.AddStep(new BuildResultStep());

            return pipeline.Execute(context);
        }
    }
}
