using System;
using System.Collections.Generic;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using MathNet.Numerics;

namespace BmsAtelierKyokufu.BmsPartTuner.Models
{
    /// <summary>
    /// 有音区間のメタデータと波形データを保持する構造体
    /// </summary>
    public readonly struct ActiveRegion(int offset, int length, float[] data)
    {
        public readonly int Offset = offset;
        public readonly int Length = length;
        public readonly float[] Data = data;
    }

    /// <summary>
    /// 波形正規化モード
    /// </summary>
    public enum NormalizationMode
    {
        None,
        PeakNormalize,
        RmsNormalize
    }

    /// <summary>
    /// <para>オンメモリでキャッシュされた音声データ（SIMD最適化版）</para>
    /// <para>
    /// 【メモリ最適化戦略】
    /// 処理されたデータのみを保持し、I/Oや計算ロジックを持たない純粋なデータモデルです。
    /// 生成は AudioProcessingService に委譲されています。
    /// </para>
    /// </summary>
    public class PreNormalizedSoundData : ICachedSoundData, IDisposable
    {
        public string FilePath { get; }
        public int SampleRate { get; }
        public int Channels { get; }
        public int BitsPerSample { get; }

        public float[]? Samples => null;
        public float[][]? SamplesPerChannel { get; private set; }

        public List<ActiveRegion>[]? NormalizedRegions { get; private set; }

        public int TotalSamples { get; }
        public float TotalRms { get; }
        public long FileSize { get; }
        public int StartSilenceSamples { get; }

        public int EffectiveLength => TotalSamples > StartSilenceSamples * Channels
            ? TotalSamples - (StartSilenceSamples * Channels)
            : 0;

        private readonly ulong[][]? _signLsh;
        private readonly ulong[][]? _signLshMask;

        public Complex32[][]? FftSpectrum { get; }
        public ulong ShiftInvariantLsh { get; }

        public double EstimatedMemoryMB
        {
            get
            {
                long totalBytes = 0;
                if (NormalizedRegions != null)
                {
                    foreach (var channelRegions in NormalizedRegions)
                    {
                        if (channelRegions != null)
                        {
                            foreach (var region in channelRegions)
                            {
                                if (region.Data != null)
                                    totalBytes += region.Data.Length * sizeof(float);
                            }
                        }
                    }
                }
                return totalBytes / 1024.0 / 1024.0;
            }
        }

        public bool IsPreNormalized => true;

        /// <summary>
        /// 全データの注入用コンストラクタ（AudioProcessingServiceから呼ばれる）
        /// </summary>
        public PreNormalizedSoundData(
            string filePath,
            int sampleRate,
            int channels,
            int bitsPerSample,
            int totalSamples,
            long fileSize,
            List<ActiveRegion>[]? normalizedRegions,
            float totalRms,
            int startSilenceSamples,
            ulong[][]? signLsh,
            ulong[][]? signLshMask,
            Complex32[][]? fftSpectrum,
            ulong shiftInvariantLsh)
        {
            FilePath = filePath;
            SampleRate = sampleRate;
            Channels = channels;
            BitsPerSample = bitsPerSample;
            TotalSamples = totalSamples;
            FileSize = fileSize;
            NormalizedRegions = normalizedRegions;
            TotalRms = totalRms;
            StartSilenceSamples = startSilenceSamples;
            _signLsh = signLsh;
            _signLshMask = signLshMask;
            FftSpectrum = fftSpectrum;
            ShiftInvariantLsh = shiftInvariantLsh;
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

        public ReadOnlySpan<ulong> GetLsh(int channel)
        {
            if (channel < 0 || channel >= Channels) throw new ArgumentOutOfRangeException(nameof(channel));
            if (_signLsh == null) return [];
            return _signLsh[channel];
        }

        public ReadOnlySpan<ulong> GetLshMask(int channel)
        {
            if (channel < 0 || channel >= Channels) throw new ArgumentOutOfRangeException(nameof(channel));
            if (_signLshMask == null) return [];
            return _signLshMask[channel];
        }

        public void Dispose()
        {
            NormalizedRegions = null;
            GC.SuppressFinalize(this);
        }
    }
}
