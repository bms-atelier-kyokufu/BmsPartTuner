using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Processing
{
    /// <summary>
    /// 音声の特徴量（FFT、LSH、SimHash等）の抽出を担当する純粋なドメインサービス。
    /// </summary>
    public record AudioFeatures(
        ulong[][] SignLsh,
        ulong[][] SignLshMask,
        Complex32[][]? FftSpectrum,
        float[]? SpectralFeatures,
        ulong[]? SimHash256
    );

    [ADRAnchor("OPT-05", nameof(AudioFeatureExtractor))]
    [ADRAnchor("M-03", nameof(AudioFeatureExtractor))]
    [ADRAnchor("M-05", nameof(AudioFeatureExtractor))]
    internal static class AudioFeatureExtractor
    {
        public static AudioFeatures ExtractAllFeatures(float[][] samplesPerChannel, int lengthSamples, List<ActiveRegion>[] regions, int channels)
        {
            var (signLsh, signLshMask) = GenerateLsh(samplesPerChannel, lengthSamples, channels);
            var fftSpectrum = GenerateFftSpectrum(samplesPerChannel, regions, channels);
            var spectralFeatures = GenerateSpectralFeatures(fftSpectrum);
            var simHash256 = GenerateSimHash256(fftSpectrum);

            return new AudioFeatures(signLsh, signLshMask, fftSpectrum, spectralFeatures, simHash256);
        }

        public static (ulong[][] signLsh, ulong[][] signLshMask) GenerateLsh(float[][] samplesPerChannel, int lengthSamples, int channels)
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

        public static Complex32[][]? GenerateFftSpectrum(float[][] samplesPerChannel, List<ActiveRegion>[] regionsPerChannel, int channels)
        {
            const int fftLen = 4096;
            const int extractLen = 2048;

            var fftSpectrum = new Complex32[channels][];
            double[] hannWindow = MathNet.Numerics.Window.Hann(extractLen);

            for (int ch = 0; ch < channels; ch++)
            {
                var complexData = new Complex32[fftLen];
                var regions = regionsPerChannel[ch];

                if (regions?.Count > 0)
                {
                    int startOffset = regions[0].Offset;
                    var channelSamples = samplesPerChannel[ch];
                    int availableLength = channelSamples.Length - startOffset;
                    int copyLength = Math.Min(extractLen, availableLength);

                    if (copyLength > 0)
                    {
                        var span = new ReadOnlySpan<float>(channelSamples, startOffset, copyLength);
                        for (int i = 0; i < copyLength; i++)
                        {
                            complexData[i] = new Complex32((float)(span[i] * hannWindow[i]), 0);
                        }
                    }
                }

                Fourier.Forward(complexData, FourierOptions.Default);
                fftSpectrum[ch] = complexData;
            }

            return fftSpectrum;
        }

        public static ulong[]? GenerateSimHash256(Complex32[][]? fftSpectrum)
        {
            if (fftSpectrum == null || fftSpectrum.Length == 0 || fftSpectrum[0] == null) return null;

            var spectrum = fftSpectrum[0];
            ulong[] hash = new ulong[4];

            var random = new Random(42);
            const int features = 256;

            for (int i = 0; i < 4; i++)
            {
                ulong currentHash = 0;
                for (int bit = 0; bit < 64; bit++)
                {
                    double dotProduct = 0;
                    for (int f = 0; f < features; f++)
                    {
                        double val = spectrum[f].Magnitude;
                        double weight = (random.NextDouble() * 2.0) - 1.0;
                        dotProduct += val * weight;
                    }
                    if (dotProduct > 0)
                    {
                        currentHash |= (1UL << bit);
                    }
                }
                hash[i] = currentHash;
            }
            return hash;
        }

        public static float[]? GenerateSpectralFeatures(Complex32[][]? fftSpectrum)
        {
            if (fftSpectrum == null || fftSpectrum.Length == 0 || fftSpectrum[0] == null) return null;
            var spec = fftSpectrum[0];
            if (spec.Length < 17) return null;

            var vec = new float[16];
            double sumSq = 0;
            for (int i = 1; i <= 16; i++) // bins 1 to 16 (exclude DC offset at bin 0)
            {
                float mag = spec[i].Magnitude;
                vec[i - 1] = mag;
                sumSq += mag * mag;
            }
            if (sumSq > 0)
            {
                float norm = (float)Math.Sqrt(sumSq);
                for (int i = 0; i < 16; i++)
                {
                    vec[i] /= norm;
                }
            }
            return vec;
        }
    }
}
