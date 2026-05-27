using System;
using System.Collections.Generic;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// テスト用のICachedSoundData実装。
    /// 波形データから動的に特徴量を生成します。
    /// </summary>
    public class MockCachedSoundData : ICachedSoundData
    {
        public string FilePath { get; }
        public int SampleRate { get; }
        public int Channels { get; }
        public int BitsPerSample { get; }

        public static float[]? Samples => null;
        public float[][]? SamplesPerChannel { get; private set; }

        public List<ActiveRegion>[]? NormalizedRegions { get; }

        public int TotalSamples { get; }
        public float TotalRms { get; }
        public long FileSize { get; }
        public int StartSilenceSamples { get; }

        public int EffectiveLength => TotalSamples > StartSilenceSamples * Channels
            ? TotalSamples - (StartSilenceSamples * Channels)
            : 0;

        public double EstimatedMemoryMB => 0;
        public bool IsPreNormalized => true;


        public MathNet.Numerics.Complex32[][]? FftSpectrum { get; }
        public ulong ShiftInvariantLsh { get; }

        private readonly ulong[][] _signLsh;
        private readonly ulong[][] _signLshMask;

        public MockCachedSoundData(float[][] samplesPerChannel, int sampleRate, int bitsPerSample, string filePath = "test.wav")
        {
            if (samplesPerChannel == null || samplesPerChannel.Length == 0 || samplesPerChannel[0].Length == 0)
            {
                throw new ArgumentException("Samples cannot be empty.");
            }

            FilePath = filePath;
            SampleRate = sampleRate;
            BitsPerSample = bitsPerSample;
            SamplesPerChannel = samplesPerChannel;
            Channels = samplesPerChannel.Length;
            int samplesPerChannelLen = samplesPerChannel[0].Length;
            TotalSamples = samplesPerChannelLen * Channels;
            FileSize = 1024;

            NormalizedRegions = ExtractActiveRegions(samplesPerChannel, Channels);
            StartSilenceSamples = DetectStartSilence(samplesPerChannel, samplesPerChannelLen, Channels);
            TotalRms = CalculateTotalRms(samplesPerChannel, samplesPerChannelLen, Channels);

            var lshResult = GenerateLsh(samplesPerChannel, samplesPerChannelLen, Channels);
            _signLsh = lshResult.signLsh;
            _signLshMask = lshResult.signLshMask;

            FftSpectrum = GenerateFftSpectrum(samplesPerChannel, Channels);
            ShiftInvariantLsh = GenerateShiftInvariantLsh(FftSpectrum);
        }

        public IReadOnlyList<ActiveRegion>[] GetActiveRegions()
        {
            return NormalizedRegions ?? [[], []];
        }

        public ReadOnlySpan<float> GetRawSpan(int channel, int offset, int length)
        {
            if (channel < 0 || channel >= Channels) throw new ArgumentOutOfRangeException(nameof(channel));

            float[] buffer = new float[length];
            var regions = NormalizedRegions?[channel];
            if (regions == null) return buffer;

            int endOffset = offset + length;
            foreach (var region in regions)
            {
                int rStart = region.Offset;
                int rEnd = region.Offset + region.Length;

                if (rEnd <= offset || rStart >= endOffset) continue;

                int overlapStart = Math.Max(rStart, offset);
                int overlapEnd = Math.Min(rEnd, endOffset);

                int srcOffset = overlapStart - rStart;
                int destOffset = overlapStart - offset;
                int copyLength = overlapEnd - overlapStart;

                if (copyLength > 0 && region.Data != null)
                {
                    Array.Copy(region.Data, srcOffset, buffer, destOffset, copyLength);
                }
            }

            return buffer;
        }

        public double GetChannelSum(int channel) => throw new NotSupportedException();
        public double GetChannelSumSq(int channel) => throw new NotSupportedException();
        public double GetRangeSum(int channel, int offset, int length) => throw new NotSupportedException();
        public double GetRangeSumSq(int channel, int offset, int length) => throw new NotSupportedException();

        public ReadOnlySpan<ulong> GetLsh(int channel) => _signLsh[channel];
        public ReadOnlySpan<ulong> GetLshMask(int channel) => _signLshMask[channel];

        public void Dispose()
        {
            for (int i = 0; i < Channels; i++)
            {
                _signLsh[i] = [];
                _signLshMask[i] = [];
            }
            SamplesPerChannel = null;
            GC.SuppressFinalize(this);
        }

        // --- 以下はテスト用にプロダクションコード(AudioProcessingService)のロジックを簡略移植したもの ---

        private static List<ActiveRegion>[] ExtractActiveRegions(float[][] samplesPerChannel, int channels)
        {
            var regionsPerChannel = new List<ActiveRegion>[channels];
            for (int ch = 0; ch < channels; ch++)
            {
                regionsPerChannel[ch] = new List<ActiveRegion>();
                var samples = samplesPerChannel[ch];
                if (samples.Length > 0)
                {
                    // テスト用：単純に全体を1つのRegionとして扱う（必要に応じてより厳密に移植）
                    float[] normData = new float[samples.Length];
                    double sum = 0;
                    for (int i = 0; i < samples.Length; i++) sum += samples[i];
                    double mean = sum / samples.Length;
                    double varSum = 0;
                    for (int i = 0; i < samples.Length; i++) varSum += Math.Pow(samples[i] - mean, 2);
                    double stdDev = Math.Sqrt(varSum);

                    if (stdDev < 1e-10)
                    {
                        for (int i = 0; i < samples.Length; i++) normData[i] = 0;
                    }
                    else
                    {
                        for (int i = 0; i < samples.Length; i++) normData[i] = (float)((samples[i] - mean) / stdDev);
                    }

                    regionsPerChannel[ch].Add(new ActiveRegion(0, samples.Length, normData));
                }
            }
            return regionsPerChannel;
        }

        private static float CalculateTotalRms(float[][] samplesPerChannel, int length, int channels)
        {
            double sumSq = 0;
            long totalCount = (long)length * channels;
            if (totalCount == 0) return 0f;

            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < length; i++)
                {
                    sumSq += samplesPerChannel[ch][i] * (double)samplesPerChannel[ch][i];
                }
            }

            return (float)Math.Sqrt(sumSq / totalCount);
        }

        private static int DetectStartSilence(float[][] samplesPerChannel, int length, int channels)
        {
            return 0; // テスト用ダミー
        }

        private static (ulong[][] signLsh, ulong[][] signLshMask) GenerateLsh(float[][] samplesPerChannel, int lengthSamples, int channels)
        {
            int extractLen = Math.Min(lengthSamples, 2048);
            const int fftLen = 4096;
            const int lshLength = 2048 / 64;

            var signLsh = new ulong[channels][];
            var signLshMask = new ulong[channels][];

            for (int ch = 0; ch < channels; ch++)
            {
                signLsh[ch] = new ulong[lshLength];
                signLshMask[ch] = new ulong[lshLength];
            }

            if (extractLen <= 0) return (signLsh, signLshMask);

            double[] hannWindow = MathNet.Numerics.Window.Hann(extractLen);

            for (int ch = 0; ch < channels; ch++)
            {
                var complexData = new Complex32[fftLen];
                var span = new ReadOnlySpan<float>(samplesPerChannel[ch], 0, extractLen);

                for (int i = 0; i < extractLen; i++)
                {
                    complexData[i] = new Complex32((float)(span[i] * hannWindow[i]), 0);
                }

                Fourier.Forward(complexData, FourierOptions.Default);

                float[] magnitudes = new float[2048];
                for (int i = 0; i < 2048; i++)
                {
                    magnitudes[i] = complexData[i].Magnitude;
                }

                for (int i = 0; i < 2048 - 1; i++)
                {
                    int lshIdx = i / 64;
                    int bitShift = i % 64;

                    if (magnitudes[i] >= magnitudes[i + 1])
                    {
                        signLsh[ch][lshIdx] |= 1UL << bitShift;
                    }
                    if (magnitudes[i] > 1e-4f)
                    {
                        signLshMask[ch][lshIdx] |= 1UL << bitShift;
                    }
                }
            }

            return (signLsh, signLshMask);
        }

        private static Complex32[][]? GenerateFftSpectrum(float[][] samplesPerChannel, int channels)
        {
            const int fftLen = 4096;
            const int extractLen = 2048;

            var fftSpectrum = new Complex32[channels][];
            double[] hannWindow = MathNet.Numerics.Window.Hann(extractLen);

            for (int ch = 0; ch < channels; ch++)
            {
                var complexData = new Complex32[fftLen];
                var channelSamples = samplesPerChannel[ch];
                int copyLength = Math.Min(extractLen, channelSamples.Length);

                if (copyLength > 0)
                {
                    var span = new ReadOnlySpan<float>(channelSamples, 0, copyLength);
                    for (int i = 0; i < copyLength; i++)
                    {
                        complexData[i] = new Complex32((float)(span[i] * hannWindow[i]), 0);
                    }
                }

                Fourier.Forward(complexData, FourierOptions.Default);
                fftSpectrum[ch] = complexData;
            }

            return fftSpectrum;
        }

        private static ulong GenerateShiftInvariantLsh(Complex32[][]? fftSpectrum)
        {
            if (fftSpectrum == null || fftSpectrum.Length == 0 || fftSpectrum[0] == null) return 0;

            var spectrum = fftSpectrum[0];
            ulong hash = 0;

            // 擬似乱数で64個のランダムベクトルを生成し、内積を取る
            var random = new Random(42);
            const int features = 256;

            for (int bit = 0; bit < 64; bit++)
            {
                double dotProduct = 0;
                for (int i = 0; i < features; i++)
                {
                    double val = spectrum[i].Magnitude;
                    double weight = (random.NextDouble() * 2.0) - 1.0;
                    dotProduct += val * weight;
                }
                if (dotProduct > 0)
                {
                    hash |= (1UL << bit);
                }
            }
            return hash;
        }
    }
}
