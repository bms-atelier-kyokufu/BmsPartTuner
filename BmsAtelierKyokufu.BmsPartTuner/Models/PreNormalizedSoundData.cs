using System;
using System.Collections.Generic;
using BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;
using BmsAtelierKyokufu.BmsPartTuner.Core.Audio;
using MathNet.Numerics;

namespace BmsAtelierKyokufu.BmsPartTuner.Models
{
    /// <summary>
    /// PreNormalizedSoundDataの初期化パラメータ
    /// </summary>
    public record PreNormalizedSoundDataParameters(
        string FilePath,
        int SampleRate,
        int Channels,
        int BitsPerSample,
        int TotalSamples,
        long FileSize,
        List<ActiveRegion>[]? NormalizedRegions,
        float TotalRms,
        int StartSilenceSamples,
        ulong[][]? SignLsh,
        ulong[][]? SignLshMask,
        Complex32[][]? FftSpectrum,
        float[]? SpectralFeatures,
        ulong[]? SimHash256
    );

    /// <summary>
    /// 有音区間のメタデータと波形データを保持する構造体。
    /// </summary>
    public readonly struct ActiveRegion(int offset, int length, float[] data)
    {
        /// <summary>元の波形におけるオフセット。</summary>
        public readonly int Offset = offset;

        /// <summary>区間の長さ。</summary>
        public readonly int Length = length;

        /// <summary>有音区間の波形データ配列。</summary>
        public readonly float[] Data = data;
    }

    /// <summary>
    /// 波形正規化モード。
    /// </summary>
    public enum NormalizationMode
    {
        /// <summary>正規化なし。</summary>
        None,

        /// <summary>ピークレベルによる正規化。</summary>
        PeakNormalize,

        /// <summary>RMS（実効値）による正規化。</summary>
        RmsNormalize
    }

    /// <summary>
    /// オンメモリでキャッシュされた音声データ (SIMD最適化版) です。
    /// 処理されたデータのみを保持する純粋なデータモデルであり、I/Oロジックは持ちません。
    /// </summary>
    [ADRAnchor("OPT-05", nameof(PreNormalizedSoundData))]
    public class PreNormalizedSoundData : ICachedSoundData, IDisposable
    {
        /// <inheritdoc />
        public string FilePath { get; }

        /// <inheritdoc />
        public int SampleRate { get; }

        /// <inheritdoc />
        public int Channels { get; }

        /// <inheritdoc />
        public int BitsPerSample { get; }

        /// <summary>事前正規化モードのため、元のサンプル配列への直接アクセスは非サポートです。</summary>
        public float[]? Samples => null;

        /// <summary>チャンネルごとのサンプル配列。</summary>
        public float[][]? SamplesPerChannel { get; private set; }

        /// <summary>チャンネルごとの有音区間リスト。</summary>
        public List<ActiveRegion>[]? NormalizedRegions { get; private set; }

        /// <inheritdoc />
        public int TotalSamples { get; }

        /// <inheritdoc />
        public float TotalRms { get; }

        /// <inheritdoc />
        public long FileSize { get; }

        /// <inheritdoc />
        public int StartSilenceSamples { get; }

        /// <inheritdoc />
        public int EffectiveLength => TotalSamples > StartSilenceSamples * Channels
            ? TotalSamples - (StartSilenceSamples * Channels)
            : 0;

        private readonly ulong[][]? _signLsh;
        private readonly ulong[][]? _signLshMask;

        /// <inheritdoc />
        public Complex32[][]? FftSpectrum { get; }

        /// <inheritdoc />
        public float[]? SpectralFeatures { get; }

        /// <inheritdoc />
        public ulong[]? SimHash256 { get; }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public bool IsPreNormalized => true;

        /// <summary>
        /// 全データを注入してインスタンスを初期化します。
        /// </summary>
        public PreNormalizedSoundData(PreNormalizedSoundDataParameters p)
        {
            FilePath = p.FilePath;
            SampleRate = p.SampleRate;
            Channels = p.Channels;
            BitsPerSample = p.BitsPerSample;
            TotalSamples = p.TotalSamples;
            FileSize = p.FileSize;
            NormalizedRegions = p.NormalizedRegions;
            TotalRms = p.TotalRms;
            StartSilenceSamples = p.StartSilenceSamples;
            _signLsh = p.SignLsh;
            _signLshMask = p.SignLshMask;
            FftSpectrum = p.FftSpectrum;
            SpectralFeatures = p.SpectralFeatures;
            SimHash256 = p.SimHash256;
        }

        /// <inheritdoc />
        public IReadOnlyList<ActiveRegion>[] GetActiveRegions()
        {
            return NormalizedRegions ?? [[], []];
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public double GetChannelSum(int channel) => throw new NotSupportedException();
        
        /// <inheritdoc />
        public double GetChannelSumSq(int channel) => throw new NotSupportedException();
        
        /// <inheritdoc />
        public double GetRangeSum(int channel, int offset, int length) => throw new NotSupportedException();
        
        /// <inheritdoc />
        public double GetRangeSumSq(int channel, int offset, int length) => throw new NotSupportedException();

        /// <inheritdoc />
        public ReadOnlySpan<ulong> GetLsh(int channel)
        {
            if (channel < 0 || channel >= Channels) throw new ArgumentOutOfRangeException(nameof(channel));
            if (_signLsh == null) return [];
            return _signLsh[channel];
        }

        /// <inheritdoc />
        public ReadOnlySpan<ulong> GetLshMask(int channel)
        {
            if (channel < 0 || channel >= Channels) throw new ArgumentOutOfRangeException(nameof(channel));
            if (_signLshMask == null) return [];
            return _signLshMask[channel];
        }

        /// <inheritdoc />
        public void Dispose()
        {
            NormalizedRegions = null;
            GC.SuppressFinalize(this);
        }
    }
}
