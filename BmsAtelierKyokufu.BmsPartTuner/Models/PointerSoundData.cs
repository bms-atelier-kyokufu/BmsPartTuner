using MathNet.Numerics;
namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// 元の大きなWAVファイル（ベース）の特定の範囲を指し示すポインタモデルです。
/// 自身のメモリを確保せず、ベースWAVの配列を <see cref="Span{T}"/> として返すFlyweightパターンを採用しており、
/// 多数生成してもメモリを消費しません。
/// </summary>
/// <summary>
/// 音声ファイルのオフセットと長さを保持する、軽量なサウンドデータ参照クラス (bmson用)。
/// 実際の波形データはキャッシュされた大元の <see cref="CachedAudioSource"/> からオンデマンドで切り出します。
/// </summary>
[ADRAnchor("OPT-05", nameof(PointerSoundData))]
public class PointerSoundData(
    string filePath,
    BaseAudioOptimizationData baseData,
    int startSample,
    int lengthSamples) : ICachedSoundData, IAudioStatisticalData
{
    /// <inheritdoc />
    public string FilePath { get; } = filePath;

    /// <inheritdoc />
    public int SampleRate => AppConstants.Audio.StandardSampleRate;

    /// <inheritdoc />
    public int Channels => 2;

    /// <inheritdoc />
    public int BitsPerSample => 16;

    /// <inheritdoc />
    public int TotalSamples { get; } = lengthSamples * 2;

    /// <inheritdoc />
    public long FileSize => TotalSamples * 2;

    /// <inheritdoc />
    public float TotalRms { get; } = CalculateTotalRms(baseData.SamplesPerChannel, startSample, lengthSamples);

    /// <inheritdoc />
    public int StartSilenceSamples { get; } = DetectStartSilence(baseData.SamplesPerChannel, startSample, lengthSamples);

    /// <inheritdoc />
    public int EffectiveLength => TotalSamples > StartSilenceSamples * Channels
        ? TotalSamples - (StartSilenceSamples * Channels)
        : 0;

    /// <inheritdoc />
    public double EstimatedMemoryMB => 0.0;

    /// <inheritdoc />
    public bool IsPreNormalized => false;

    /// <inheritdoc />
    public Complex32[][]? FftSpectrum => null;

    /// <inheritdoc />
    public float[]? SpectralFeatures => null;

    /// <inheritdoc />
    public ulong[]? SimHash256 => null;

    private BaseAudioOptimizationData? _baseData = baseData;
    private readonly int _startSample = startSample;
    private readonly int _lengthSamples = lengthSamples;

    private BaseAudioOptimizationData BaseData => _baseData ?? throw new ObjectDisposedException(nameof(PointerSoundData));

    private IReadOnlyList<ActiveRegion>[]? _regions;

    /// <inheritdoc />
    public IReadOnlyList<ActiveRegion>[] GetActiveRegions()
    {
        _regions ??= ExtractPointerRegions(BaseData.SamplesPerChannel, _startSample, _lengthSamples);
        return _regions;
    }

    /// <inheritdoc />
    public double GetChannelSum(int channel)
    {
        return GetRangeSum(channel, 0, _lengthSamples);
    }

    /// <inheritdoc />
    public double GetChannelSumSq(int channel)
    {
        return GetRangeSumSq(channel, 0, _lengthSamples);
    }

    /// <inheritdoc />
    public ReadOnlySpan<float> GetRawSpan(int channel, int offset, int length)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        return new ReadOnlySpan<float>(BaseData.SamplesPerChannel[channel], _startSample + offset, length);
    }

    /// <inheritdoc />
    public double GetRangeSum(int channel, int offset, int length)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        int globalStart = _startSample + offset;
        return BaseData.PrefixSum[channel][globalStart + length] - BaseData.PrefixSum[channel][globalStart];
    }

    /// <inheritdoc />
    public double GetRangeSumSq(int channel, int offset, int length)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        int globalStart = _startSample + offset;
        return BaseData.PrefixSumSq[channel][globalStart + length] - BaseData.PrefixSumSq[channel][globalStart];
    }

    /// <inheritdoc />
    public ReadOnlySpan<ulong> GetLsh(int channel)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        int globalStartLshIdx = _startSample / 64;
        int lshCount = (_lengthSamples + 63) / 64;
        return new ReadOnlySpan<ulong>(BaseData.SignLsh[channel], globalStartLshIdx, lshCount);
    }

    /// <inheritdoc />
    public ReadOnlySpan<ulong> GetLshMask(int channel)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        int globalStartLshIdx = _startSample / 64;
        int lshCount = (_lengthSamples + 63) / 64;
        return new ReadOnlySpan<ulong>(BaseData.SignLshMask[channel], globalStartLshIdx, lshCount);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _baseData = null;
        _regions = null;
        GC.SuppressFinalize(this);
    }

    private static float CalculateTotalRms(float[][] samplesPerChannel, int startSample, int lengthSamples)
    {
        if (lengthSamples == 0) return 0f;

        double sumSquares = 0;
        int totalAnalyzedSamples = lengthSamples * 2;

        for (int ch = 0; ch < 2; ch++)
        {
            var span = new ReadOnlySpan<float>(samplesPerChannel[ch], startSample, lengthSamples);
            for (int i = 0; i < lengthSamples; i++)
            {
                float val = span[i];
                sumSquares += val * val;
            }
        }
        return (float)Math.Sqrt(sumSquares / totalAnalyzedSamples);
    }

    private static int DetectStartSilence(float[][] samplesPerChannel, int startSample, int lengthSamples)
    {
        const float threshold = 0.001f;
        var spanL = new ReadOnlySpan<float>(samplesPerChannel[0], startSample, lengthSamples);
        var spanR = new ReadOnlySpan<float>(samplesPerChannel[1], startSample, lengthSamples);

        for (int i = 0; i < lengthSamples; i++)
        {
            if (Math.Abs(spanL[i]) > threshold || Math.Abs(spanR[i]) > threshold)
            {
                return i;
            }
        }
        return lengthSamples;
    }

    private static IReadOnlyList<ActiveRegion>[] ExtractPointerRegions(float[][] samplesPerChannel, int startSample, int lengthSamples)
    {
        int channels = samplesPerChannel.Length;
        var regionsPerChannel = new List<ActiveRegion>[channels];

        const double dbThreshold = -45.0;
        const int windowFrames = 1024;
        double eThreshold = windowFrames * Math.Pow(10, dbThreshold / 10.0);
        const int maxSilenceFrames = AppConstants.Audio.StandardSampleRate / 4;

        for (int ch = 0; ch < channels; ch++)
        {
            var samplesSpan = new ReadOnlySpan<float>(samplesPerChannel[ch], startSample, lengthSamples);
            regionsPerChannel[ch] = ExtractChannelPointerRegions(samplesSpan, windowFrames, eThreshold, maxSilenceFrames);
        }

        return regionsPerChannel;
    }

    private static List<ActiveRegion> ExtractChannelPointerRegions(ReadOnlySpan<float> samplesSpan, int windowFrames, double eThreshold, int maxSilenceFrames)
    {
        var channelRegions = new List<ActiveRegion>();
        int lengthSamples = samplesSpan.Length;
        int startIdx = -1;
        int currentSilenceFrames = 0;
        double currentEnergy = 0;

        for (int i = 0; i < lengthSamples; i++)
        {
            double sample = samplesSpan[i];
            currentEnergy += sample * sample;

            if (i >= windowFrames)
            {
                double outSample = samplesSpan[i - windowFrames];
                currentEnergy -= outSample * outSample;
                currentEnergy = Math.Max(0, currentEnergy);
            }

            if (i >= windowFrames - 1)
            {
                if (currentEnergy >= eThreshold)
                {
                    if (startIdx == -1)
                    {
                        startIdx = i - windowFrames + 1;
                    }
                    currentSilenceFrames = 0;
                }
                else if (startIdx != -1)
                {
                    currentSilenceFrames++;
                    if (currentSilenceFrames >= maxSilenceFrames)
                    {
                        int endIdx = i - currentSilenceFrames + 1;
                        int length = endIdx - startIdx;
                        if (length > 0)
                        {
                            channelRegions.Add(new ActiveRegion(startIdx, length, null!));
                        }
                        startIdx = -1;
                        currentSilenceFrames = 0;
                    }
                }
            }
        }

        if (startIdx != -1)
        {
            int length = lengthSamples - startIdx;
            if (length > 0)
            {
                channelRegions.Add(new ActiveRegion(startIdx, length, null!));
            }
        }

        return channelRegions;
    }
}
