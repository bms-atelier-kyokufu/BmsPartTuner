namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Processing.Pipeline;

/// <summary>
/// 音声処理パイプラインの実行コンテキスト。
/// </summary>
internal class AudioProcessingContext(string path, NormalizationMode normalizationMode)
{
    public string Path { get; } = path;
    public NormalizationMode NormalizationMode { get; } = normalizationMode;

    public float[][]? SamplesPerChannel { get; set; }
    public AudioFileInfo? FileInfo { get; set; }

    public int SamplesPerChannelLen => FileInfo != null ? FileInfo.TotalSamples / FileInfo.Channels : 0;
    public int Channels => FileInfo?.Channels ?? 0;

    public AudioMetrics? Metrics { get; set; }
    public AudioFeatures? Features { get; set; }

    public PreNormalizedSoundData? Result { get; set; }
}
