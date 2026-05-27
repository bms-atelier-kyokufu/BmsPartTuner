using System;
using System.Collections.Generic;
using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio
{
    /// <summary>
    /// 音声ファイルの読み込み、チャンネル分離、正規化、各種特徴量（RMS、有音区間、LSH）の抽出を行うサービス。
    /// PreNormalizedSoundData の生成を担います。
    /// </summary>
    internal static class AudioProcessingService
    {
        public static PreNormalizedSoundData LoadAndProcess(string path, NormalizationMode normalizationMode)
        {
            var fileName = Path.GetFileName(path);
            Stream? memoryStreamToDispose = null;
            WaveStream stream;
            ISampleProvider sampleProvider;
            long fileSize = 0;

            if (VirtualAudioRegistry.TryGetStream(fileName, out var vStream))
            {
                VirtualAudioRegistry.TryGetFileSize(fileName, out var size);
                fileSize = size;
                memoryStreamToDispose = vStream;
                var waveReader = new WaveFileReader(memoryStreamToDispose);
                stream = waveReader;
                sampleProvider = waveReader.ToSampleProvider();
            }
            else
            {
                var fi = new FileInfo(path);
                if (!fi.Exists)
                {
                    throw new FileNotFoundException($"File not found: {path}");
                }
                fileSize = fi.Length;
                var audioReader = new AudioFileReader(path);
                stream = audioReader;
                sampleProvider = audioReader;
            }

            using (memoryStreamToDispose)
            using (stream)
            {
                int sampleRate = stream.WaveFormat.SampleRate;
                int channels = stream.WaveFormat.Channels;
                int bitsPerSample = stream.WaveFormat.BitsPerSample;

                long totalSamples;
                if (stream is AudioFileReader)
                {
                    totalSamples = stream.Length / sizeof(float);
                }
                else
                {
                    totalSamples = stream.Length / (stream.WaveFormat.BitsPerSample / 8);
                }

                if (totalSamples == 0)
                {
                    throw new InvalidOperationException($"File has zero samples: {path}");
                }

                float[] samplesArray = new float[totalSamples];
                int totalRead = 0;
                int bufferSize = Math.Min(sampleRate * channels, (int)totalSamples);

                while (totalRead < totalSamples)
                {
                    int toRead = (int)Math.Min(bufferSize, totalSamples - totalRead);
                    int read = sampleProvider.Read(samplesArray, totalRead, toRead);

                    if (read == 0)
                    {
                        PerformanceDebugLogger.WriteLine($"[CachedSoundData] WARNING: Read returned 0 at {totalRead}/{totalSamples} for {Path.GetFileName(path)}");
                        break;
                    }

                    totalRead += read;
                }

                if (totalRead == 0)
                {
                    throw new InvalidOperationException($"Failed to read any samples from file: {path}");
                }

                if (totalRead < totalSamples)
                {
                    Array.Resize(ref samplesArray, totalRead);
                }

                int samplesPerChannelLen = samplesArray.Length / channels;
                float[][] samplesPerChannel = DeinterleaveChannels(samplesArray, channels, samplesPerChannelLen);

                if (normalizationMode != NormalizationMode.None)
                {
                    ApplyNormalization(samplesPerChannel, channels, normalizationMode);
                }

                var normalizedRegions = ExtractActiveRegions(samplesPerChannel, channels);
                int startSilenceSamples = DetectStartSilence(samplesPerChannel, samplesPerChannelLen, channels);
                float totalRms = CalculateTotalRms(samplesPerChannel, samplesPerChannelLen, channels);
                var (signLsh, signLshMask) = GenerateLsh(samplesPerChannel, samplesPerChannelLen, channels);

                return new PreNormalizedSoundData(
                    path,
                    sampleRate,
                    channels,
                    bitsPerSample,
                    samplesPerChannelLen * channels,
                    fileSize,
                    normalizedRegions,
                    totalRms,
                    startSilenceSamples,
                    signLsh,
                    signLshMask
                );
            }
        }

        private static float[][] DeinterleaveChannels(float[] interleavedData, int channels, int samplesPerChannel)
        {
            var result = new float[channels][];
            for (int ch = 0; ch < channels; ch++)
            {
                result[ch] = new float[samplesPerChannel];
                int srcIdx = ch;
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    result[ch][i] = interleavedData[srcIdx];
                    srcIdx += channels;
                }
            }
            return result;
        }

        private static void ApplyNormalization(float[][] samplesPerChannel, int channels, NormalizationMode mode)
        {
            switch (mode)
            {
                case NormalizationMode.PeakNormalize:
                    NormalizePeak(samplesPerChannel, channels);
                    break;
                case NormalizationMode.RmsNormalize:
                    NormalizeRms(samplesPerChannel, channels);
                    break;
            }
        }

        private static void NormalizePeak(float[][] samplesPerChannel, int channels)
        {
            float maxAbsValue = 0.0f;
            for (int ch = 0; ch < channels; ch++)
            {
                foreach (float sample in samplesPerChannel[ch])
                {
                    float absValue = Math.Abs(sample);
                    if (absValue > maxAbsValue)
                        maxAbsValue = absValue;
                }
            }

            if (maxAbsValue < 1e-10f) return;

            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < samplesPerChannel[ch].Length; i++)
                {
                    samplesPerChannel[ch][i] /= maxAbsValue;
                }
            }
        }

        private static void NormalizeRms(float[][] samplesPerChannel, int channels, float targetRms = 0.5f)
        {
            float currentRms = CalculateTotalRms(samplesPerChannel, samplesPerChannel[0].Length, channels);
            if (currentRms < 1e-10f) return;

            float scaleFactor = targetRms / currentRms;
            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < samplesPerChannel[ch].Length; i++)
                {
                    samplesPerChannel[ch][i] *= scaleFactor;
                }
            }
        }

        private static List<ActiveRegion>[] ExtractActiveRegions(float[][] samplesPerChannel, int channels)
        {
            var regionsPerChannel = new List<ActiveRegion>[channels];
            const double dbThreshold = -90.0;
            const int windowFrames = 256;
            double eThreshold = windowFrames * Math.Pow(10, dbThreshold / 10.0);
            const int maxSilenceFrames = AppConstants.Audio.StandardSampleRate / 4;

            for (int ch = 0; ch < channels; ch++)
            {
                var samples = samplesPerChannel[ch];
                regionsPerChannel[ch] = [];
                int totalFrames = samples.Length;

                if (totalFrames == 0) continue;

                double sumSq = 0;
                int currentWindowFrames = Math.Min(windowFrames, totalFrames);
                for (int i = 0; i < currentWindowFrames; i++)
                {
                    sumSq += samples[i] * (double)samples[i];
                }

                int silenceFramesCount = 0;
                bool inActiveRegion = false;
                int currentRegionStart = -1;
                int lastActiveFrame = -1;

                if (sumSq > eThreshold)
                {
                    inActiveRegion = true;
                    currentRegionStart = 0;
                    lastActiveFrame = 0;
                }

                for (int i = 1; i <= totalFrames - windowFrames; i++)
                {
                    double prevSample = samples[i - 1];
                    double nextSample = samples[i + windowFrames - 1];
                    sumSq += nextSample * nextSample - prevSample * prevSample;

                    if (sumSq < 0) sumSq = 0;

                    if (sumSq > eThreshold)
                    {
                        if (!inActiveRegion)
                        {
                            inActiveRegion = true;
                            currentRegionStart = i;
                        }
                        lastActiveFrame = i;
                        silenceFramesCount = 0;
                    }
                    else
                    {
                        if (inActiveRegion)
                        {
                            silenceFramesCount++;
                            if (silenceFramesCount > maxSilenceFrames)
                            {
                                int regionEnd = lastActiveFrame + windowFrames;
                                int regionLength = regionEnd - currentRegionStart;
                                AddNormalizedRegion(regionsPerChannel[ch], samples, currentRegionStart, regionLength);
                                inActiveRegion = false;
                            }
                        }
                    }
                }

                if (inActiveRegion)
                {
                    int regionEnd = Math.Min(lastActiveFrame + windowFrames, totalFrames);
                    int regionLength = regionEnd - currentRegionStart;
                    AddNormalizedRegion(regionsPerChannel[ch], samples, currentRegionStart, regionLength);
                }
            }

            return regionsPerChannel;
        }

        private static void AddNormalizedRegion(List<ActiveRegion> regions, float[] originalSamples, int offset, int length)
        {
            if (length <= 0) return;

            float[] normData = new float[length];
            double sum = 0;
            for (int i = 0; i < length; i++)
            {
                sum += originalSamples[offset + i];
            }
            double mean = sum / length;

            double varianceSum = 0;
            for (int i = 0; i < length; i++)
            {
                double diff = originalSamples[offset + i] - mean;
                varianceSum += diff * diff;
            }

            double stdDev = Math.Sqrt(varianceSum);
            if (stdDev < 1e-10)
            {
                for (int i = 0; i < length; i++) normData[i] = 0;
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    normData[i] = (float)((originalSamples[offset + i] - mean) / stdDev);
                }
            }

            regions.Add(new ActiveRegion(offset, length, normData));
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
            const float silenceThreshold = 0.001f;
            int silenceSamples = 0;
            for (int i = 0; i < length; i++)
            {
                bool isSilent = true;
                for (int ch = 0; ch < channels; ch++)
                {
                    if (Math.Abs(samplesPerChannel[ch][i]) > silenceThreshold)
                    {
                        isSilent = false;
                        break;
                    }
                }
                if (!isSilent) break;
                silenceSamples++;
            }
            return silenceSamples;
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
    }
}
