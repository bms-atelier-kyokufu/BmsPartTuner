using System;
using System.Collections.Generic;

namespace BmsAtelierKyokufu.BmsPartTuner.Models;

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
    float[][] baseSamplesPerChannel,
    int startSample,
    int lengthSamples) : ICachedSoundData
{
    public string FilePath { get; } = filePath;
    public int SampleRate => AppConstants.Audio.StandardSampleRate;
    public int Channels => 2;
    public int BitsPerSample => 16;
    public int TotalSamples { get; } = lengthSamples * 2;
    public long FileSize => TotalSamples * 2;

    // RMSは先頭から最後までを対象に都度計算するか、初期化時に計算してキャッシュ

    public float TotalRms { get; } = CalculateTotalRms(baseSamplesPerChannel, startSample, lengthSamples);

    public int StartSilenceSamples { get; } = DetectStartSilence(baseSamplesPerChannel, startSample, lengthSamples);


    public int EffectiveLength => TotalSamples > StartSilenceSamples * Channels
        ? TotalSamples - (StartSilenceSamples * Channels)
        : 0;

    public double EstimatedMemoryMB => 0.0; // ポインタなのでメモリ消費はゼロ

    public bool IsPreNormalized => false; // 1-pass SIMDアルゴリズムを使用するフラグ

    private readonly float[][] _baseSamplesPerChannel = baseSamplesPerChannel;
    private readonly int _startSample = startSample;
    private readonly (IReadOnlyList<ActiveRegion>[] Regions, double[] SumX, double[] SumX2) _extractedData = ExtractPointerRegionsAndStats(baseSamplesPerChannel, startSample, lengthSamples);

    public IReadOnlyList<ActiveRegion>[] GetActiveRegions()
    {
        return _extractedData.Regions;
    }

    public double GetChannelSum(int channel)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        return _extractedData.SumX[channel];
    }

    public double GetChannelSumSq(int channel)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        return _extractedData.SumX2[channel];
    }

    public ReadOnlySpan<float> GetRawSpan(int channel, int offset, int length)
    {
        if (channel < 0 || channel >= 2) throw new ArgumentOutOfRangeException(nameof(channel));
        return new ReadOnlySpan<float>(_baseSamplesPerChannel[channel], _startSample + offset, length);
    }

    public void Dispose()
    {
        // 参照を保持するのみで解放すべきリソースはない
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

    private static (IReadOnlyList<ActiveRegion>[], double[], double[]) ExtractPointerRegionsAndStats(float[][] samplesPerChannel, int startSample, int lengthSamples)
    {
        var regionsPerChannel = new List<ActiveRegion>[2] { [], [] };
        var sumX = new double[2];
        var sumX2 = new double[2];

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

            double chSum = 0;
            double chSumX2 = 0;

            for (int i = 0; i < lengthSamples; i++)
            {
                double sample = samplesSpan[i];
                
                chSum += sample;
                chSumX2 += sample * sample;

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
            
            sumX[ch] = chSum;
            sumX2[ch] = chSumX2;
        }

        return (regionsPerChannel, sumX, sumX2);
    }
}
