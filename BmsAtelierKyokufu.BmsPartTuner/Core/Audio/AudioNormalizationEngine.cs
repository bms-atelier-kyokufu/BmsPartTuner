using System;
using System.Collections.Generic;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio
{
    /// <summary>
    /// 音声データの正規化と有音区間の抽出を担当する純粋なドメインサービス。
    /// I/Oを持たず、メモリ上の配列操作のみを行います。
    /// </summary>
    public record AudioMetrics(List<ActiveRegion>[] Regions, int StartSilenceSamples, float TotalRms);

    [ADRAnchor("OPT-05", nameof(AudioNormalizationEngine))]
    internal static class AudioNormalizationEngine
    {
        public static AudioMetrics ExtractMetrics(float[][] samplesPerChannel, int lengthSamples, int channels)
        {
            var regions = ExtractActiveRegions(samplesPerChannel, channels);
            var startSilence = DetectStartSilence(samplesPerChannel, lengthSamples, channels);
            var totalRms = CalculateTotalRms(samplesPerChannel, lengthSamples, channels);
            
            return new AudioMetrics(regions, startSilence, totalRms);
        }

        public static void ApplyNormalization(float[][] samplesPerChannel, int channels, NormalizationMode mode)
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

        public static List<ActiveRegion>[] ExtractActiveRegions(float[][] samplesPerChannel, int channels)
        {
            var regionsPerChannel = new List<ActiveRegion>[channels];
            const double dbThreshold = -90.0;
            const int windowFrames = 256;
            double eThreshold = windowFrames * Math.Pow(10, dbThreshold / 10.0);
            const int maxSilenceFrames = AppConstants.Audio.StandardSampleRate / 4;

            for (int ch = 0; ch < channels; ch++)
            {
                regionsPerChannel[ch] = ExtractChannelActiveRegions(samplesPerChannel[ch], windowFrames, eThreshold, maxSilenceFrames);
            }

            return regionsPerChannel;
        }

        private static List<ActiveRegion> ExtractChannelActiveRegions(float[] samples, int windowFrames, double eThreshold, int maxSilenceFrames)
        {
            var regions = new List<ActiveRegion>();
            int totalFrames = samples.Length;

            if (totalFrames == 0) return regions;

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
                sumSq += (nextSample * nextSample) - (prevSample * prevSample);

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
                else if (inActiveRegion)
                {
                    silenceFramesCount++;
                    if (silenceFramesCount > maxSilenceFrames)
                    {
                        int regionEnd = lastActiveFrame + windowFrames;
                        int regionLength = regionEnd - currentRegionStart;
                        AddNormalizedRegion(regions, samples, currentRegionStart, regionLength);
                        inActiveRegion = false;
                    }
                }
            }

            if (inActiveRegion)
            {
                int regionEnd = Math.Min(lastActiveFrame + windowFrames, totalFrames);
                int regionLength = regionEnd - currentRegionStart;
                AddNormalizedRegion(regions, samples, currentRegionStart, regionLength);
            }

            return regions;
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

        public static float CalculateTotalRms(float[][] samplesPerChannel, int length, int channels)
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

        public static int DetectStartSilence(float[][] samplesPerChannel, int length, int channels)
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
    }
}
