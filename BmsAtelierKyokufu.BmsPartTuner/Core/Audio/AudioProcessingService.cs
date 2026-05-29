using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio;

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
        public static PreNormalizedSoundData LoadAndProcess(string path, NormalizationMode normalizationMode)
        {
            // 1. ファイルI/O と デインターリーブ (NAudio依存の隔離)
            var (samplesPerChannel, fileInfo) = AudioFileReaderService.LoadAndDeinterleave(path);

            int samplesPerChannelLen = fileInfo.TotalSamples / fileInfo.Channels;
            int channels = fileInfo.Channels;

            // 2. 正規化処理 (純粋な配列計算)
            if (normalizationMode != NormalizationMode.None)
            {
                AudioNormalizationEngine.ApplyNormalization(samplesPerChannel, channels, normalizationMode);
            }

            // 3. 有音区間とRMS・無音時間の抽出
            var metrics = AudioNormalizationEngine.ExtractMetrics(samplesPerChannel, samplesPerChannelLen, channels);

            // 4. 特徴量（FFT, LSH, SimHash等）の抽出
            var features = AudioFeatureExtractor.ExtractAllFeatures(samplesPerChannel, samplesPerChannelLen, metrics.Regions, channels);

            // 結果の生成
            var p = new PreNormalizedSoundDataParameters(
                fileInfo,
                metrics,
                features
            );
            return new PreNormalizedSoundData(p);
        }
    }
}
