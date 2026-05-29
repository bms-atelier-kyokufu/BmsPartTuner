using MathNet.Numerics;
namespace BmsAtelierKyokufu.BmsPartTuner.Models
{
    /// <summary>
    /// PreNormalizedSoundDataの初期化パラメータ
    /// </summary>
    public record PreNormalizedSoundDataParameters(
        AudioFileInfo FileInfo,
        AudioMetrics Metrics,
        AudioFeatures Features
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
    public class PreNormalizedSoundData(PreNormalizedSoundDataParameters p) : ICachedSoundData, IDisposable
    {
        /// <inheritdoc />
        public string FilePath { get; } = p.FileInfo.FilePath;

        /// <inheritdoc />
        public int SampleRate { get; } = p.FileInfo.SampleRate;

        /// <inheritdoc />
        public int Channels { get; } = p.FileInfo.Channels;

        /// <inheritdoc />
        public int BitsPerSample { get; } = p.FileInfo.BitsPerSample;

        /// <summary>事前正規化モードのため、元のサンプル配列への直接アクセスは非サポートです。</summary>
        public static float[]? Samples => null;

        /// <summary>チャンネルごとのサンプル配列。</summary>
        public float[][]? SamplesPerChannel { get; }

        /// <summary>チャンネルごとの有音区間リスト。</summary>
        public List<ActiveRegion>[]? NormalizedRegions { get; private set; } = p.Metrics.Regions;

        /// <inheritdoc />
        public int TotalSamples { get; } = p.FileInfo.TotalSamples;

        /// <inheritdoc />
        public float TotalRms { get; } = p.Metrics.TotalRms;

        /// <inheritdoc />
        public long FileSize { get; } = p.FileInfo.FileSize;

        /// <inheritdoc />
        public int StartSilenceSamples { get; } = p.Metrics.StartSilenceSamples;

        /// <inheritdoc />
        public int EffectiveLength => TotalSamples > StartSilenceSamples * Channels
            ? TotalSamples - (StartSilenceSamples * Channels)
            : 0;

        private readonly ulong[][]? _signLsh = p.Features.SignLsh;
        private readonly ulong[][]? _signLshMask = p.Features.SignLshMask;

        /// <inheritdoc />
        public Complex32[][]? FftSpectrum { get; } = p.Features.FftSpectrum;

        /// <inheritdoc />
        public float[]? SpectralFeatures { get; } = p.Features.SpectralFeatures;

        /// <inheritdoc />
        public ulong[]? SimHash256 { get; } = p.Features.SimHash256;

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
