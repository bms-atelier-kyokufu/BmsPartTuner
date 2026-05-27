namespace BmsAtelierKyokufu.BmsPartTuner.Models;

using MathNet.Numerics;

/// <summary>
/// 元の大きなWAVファイル（ベース）の特定の範囲を指し示すポインタ。
/// 音声のfloat配列を自身のメモリに抱えず、ベースWAVの配列をSpanとして返すため、
/// 何千個生成してもメモリを一切消費しません。
/// </summary>
/// <remarks>
/// PointerSoundData のコンストラクタ。
/// </remarks>
public class PointerSoundData(
    string filePath,
    BaseAudioOptimizationData baseData,
    int startSample,
    int lengthSamples) : ICachedSoundData
{
    public string FilePath { get; } = filePath;
    public int SampleRate => AppConstants.Audio.StandardSampleRate;
    public int Channels => 2;
    public int BitsPerSample => 16;
    public int TotalSamples { get; } = lengthSamples * 2;
    public long FileSize => TotalSamples * 2;

    public float TotalRms { get; } = CalculateTotalRms(baseData.SamplesPerChannel, startSample, lengthSamples);

    public int StartSilenceSamples { get; } = DetectStartSilence(baseData.SamplesPerChannel, startSample, lengthSamples);

    public int EffectiveLength => TotalSamples > StartSilenceSamples * Channels
        ? TotalSamples - (StartSilenceSamples * Channels)
        : 0;

    public double EstimatedMemoryMB => 0.0;

    public bool IsPreNormalized => false;

    public Complex32[][]? FftSpectrum => null;

    public ulong ShiftInvariantLsh => 0; // ポインタ方式ではPhase 2のグループ化には関与しない（または別途実装）

    private BaseAudioOptimizationData? _baseData = baseData;
    private readonly int _startSample = startSample;
    private readonly int _lengthSamples = lengthSamples;

    private BaseAudioOptimizationData BaseData => _baseData ?? throw new ObjectDisposedException(nameof(PointerSoundData));

    // Lazy initialized ActiveRegions
    private IReadOnlyList<ActiveRegion>[]? _regions;

    public IReadOnlyList<ActiveRegion>[] GetActiveRegions()
    {
        _regions ??= ExtractPointerRegions(BaseData.SamplesPerChannel, _startSample, _lengthSamples);
        return _regions;
    }

    public double GetChannelSum(int channel)
    {
        return GetRangeSum(channel, 0, _lengthSamples);
    }

    public double GetChannelSumSq(int channel)
    {
        return GetRangeSumSq(channel, 0, _lengthSamples);
    }

    public ReadOnlySpan<float> GetRawSpan(int channel, int offset, int length)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        return new ReadOnlySpan<float>(BaseData.SamplesPerChannel[channel], _startSample + offset, length);
    }

    public double GetRangeSum(int channel, int offset, int length)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        int globalStart = _startSample + offset;
        return BaseData.PrefixSum[channel][globalStart + length] - BaseData.PrefixSum[channel][globalStart];
    }

    public double GetRangeSumSq(int channel, int offset, int length)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        int globalStart = _startSample + offset;
        return BaseData.PrefixSumSq[channel][globalStart + length] - BaseData.PrefixSumSq[channel][globalStart];
    }

    public ReadOnlySpan<ulong> GetLsh(int channel)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        int globalStartLshIdx = _startSample / 64;
        int lshCount = (_lengthSamples + 63) / 64;
        return new ReadOnlySpan<ulong>(BaseData.SignLsh[channel], globalStartLshIdx, lshCount);
    }

    public ReadOnlySpan<ulong> GetLshMask(int channel)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        int globalStartLshIdx = _startSample / 64;
        int lshCount = (_lengthSamples + 63) / 64;
        return new ReadOnlySpan<ulong>(BaseData.SignLshMask[channel], globalStartLshIdx, lshCount);
    }

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
        int totalAnalyzedSamples = lengthSamples * 2; // 2ch

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
        var regionsPerChannel = new List<ActiveRegion>[2] { [], [] };

        const double dbThreshold = -45.0;
        const int windowFrames = 1024; // 約23ms (44.1kHz時)
        double eThreshold = windowFrames * Math.Pow(10, dbThreshold / 10.0);
        const int maxSilenceFrames = AppConstants.Audio.StandardSampleRate / 4; // 250ms

        for (int ch = 0; ch < 2; ch++)
        {
            var samplesSpan = new ReadOnlySpan<float>(samplesPerChannel[ch], startSample, lengthSamples);

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
                                regionsPerChannel[ch].Add(new ActiveRegion(startIdx, length, null!));
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
                    regionsPerChannel[ch].Add(new ActiveRegion(startIdx, length, null!));
                }
            }
        }

        return regionsPerChannel;
    }
}
